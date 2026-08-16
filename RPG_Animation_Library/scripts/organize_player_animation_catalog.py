#!/usr/bin/env python3
"""Build and synchronize the browsable catalog for imported ER player clips.

The classifier is intentionally conservative. Curated semantic records keep their
reviewed category; otherwise only explicit TAE evidence is used. Clips without a
reliable gameplay identity are grouped by their numeric ID bank instead of being
assigned speculative names.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import shutil
import tempfile
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


CATALOG_FILE = "player_animation_catalog.json"
CATALOG_CSV = "player_animation_catalog.csv"
BEHAVIOR_EVIDENCE_CSV = "player_animation_behavior_evidence.csv"
DEATH_FLAGS = {"12", "40", "46", "47", "69"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument(
        "--apply-phase8",
        action="store_true",
        help="Move generated Phase 8 artifacts to the catalog paths and update contracts.",
    )
    parser.add_argument(
        "--sync-phase9",
        action="store_true",
        help="Rewrite the Phase 9 staging manifest from the organized Unity assets.",
    )
    return parser.parse_args()


def resolve_library_root(config_path: Path, config: dict) -> Path:
    return (config_path.parent.parent / config.get("project_root", ".")).resolve()


def resolve_from_library(library_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json_atomic(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, dir=path.parent) as target:
        json.dump(value, target, ensure_ascii=False, indent=2)
        target.write("\n")
        temporary = Path(target.name)
    os.replace(temporary, path)


def write_csv_atomic(path: Path, rows: list[dict[str, object]]) -> None:
    fields = [
        "LibraryID",
        "AnimationID",
        "RelativeFolder",
        "FileStem",
        "CatalogCategory",
        "CatalogType",
        "ClassificationSource",
        "Evidence",
        "ReviewStatus",
        "IDBank",
        "WeaponFamily",
        "WeaponClassificationSource",
        "WeaponEvidence",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8-sig", newline="", delete=False, dir=path.parent) as target:
        writer = csv.DictWriter(target, fieldnames=fields, extrasaction="ignore", lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
        temporary = Path(target.name)
    os.replace(temporary, path)


def safe_name(value: str, fallback: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_-]+", "_", (value or "").strip()).strip("_")
    return cleaned or fallback


def read_contracts(import_root: Path) -> list[tuple[Path, dict]]:
    records: list[tuple[Path, dict]] = []
    seen_library_ids: set[str] = set()
    for path in sorted(import_root.rglob("*.animlib.json")):
        contract = json.loads(path.read_text(encoding="utf-8-sig"))
        library_id = contract.get("LibraryID", "")
        animation_id = contract.get("AnimationID", "")
        if not library_id or not animation_id:
            raise RuntimeError(f"Incomplete animation contract: {path}")
        if library_id in seen_library_ids:
            raise RuntimeError(f"Duplicate imported LibraryID: {library_id}")
        seen_library_ids.add(library_id)
        records.append((path, contract))
    return records


def read_behavior_evidence(path: Path) -> dict[str, dict[str, str]]:
    if not path.is_file():
        raise FileNotFoundError(
            f"Behavior evidence is missing: {path}. Run "
            "extract_behavior_animation_evidence.ps1 first."
        )
    rows: dict[str, dict[str, str]] = {}
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        for row in csv.DictReader(source):
            animation_id = row.get("AnimationID", "")
            if not animation_id:
                raise RuntimeError(f"Behavior evidence contains a row without AnimationID: {path}")
            if animation_id in rows:
                raise RuntimeError(f"Duplicate behavior evidence for {animation_id}: {path}")
            rows[animation_id] = row
    return rows


def read_tae_evidence(animation_id: str, tae_folder: Path) -> tuple[set[str], set[str], bool]:
    numeric_id = animation_id.rsplit("_", 1)[-1]
    animation_path = tae_folder / f"anim-{numeric_id}.xml"
    if not animation_path.is_file():
        return set(), set(), False
    root = ET.parse(animation_path).getroot()
    event_types: set[str] = set()
    flags: set[str] = set()
    for event in root.findall("./events/event"):
        event_type = (event.findtext("type") or "").strip()
        if event_type:
            event_types.add(event_type)
        if event_type == "0":
            for parameter in event.findall("./params/param"):
                if parameter.get("name") == "FlagType" and parameter.get("value"):
                    flags.add(parameter.get("value", ""))
    return event_types, flags, True


def curated_classification(contract: dict) -> tuple[str, str, str] | None:
    action_type = contract.get("ActionType", "Unknown")
    confidence = contract.get("SemanticConfidence", "Unknown")
    if action_type == "Unknown" or confidence == "Unknown":
        return None
    mapping = {
        "Attack": ("01_Combat/Attacks/Reviewed", "Attack", "Attack"),
        "Dodge": ("02_Defense/Dodges/Reviewed", "Dodge", "Dodge"),
        "Locomotion": ("03_Movement/Locomotion/Reviewed", "Locomotion", "Locomotion"),
    }
    return mapping.get(
        action_type,
        (f"00_Reviewed/{safe_name(action_type, 'Other')}", action_type, safe_name(action_type, "Reviewed")),
    )


def classify(
    contract: dict,
    tae_folder: Path,
    behavior_evidence: dict[str, dict[str, str]],
) -> dict[str, object]:
    animation_id = contract["AnimationID"]
    numeric_id = animation_id.rsplit("_", 1)[-1]
    id_bank = f"{numeric_id[:3]}xxx"
    event_types, flags, has_tae = read_tae_evidence(animation_id, tae_folder)
    curated = curated_classification(contract)
    behavior = behavior_evidence.get(animation_id, {})
    behavior_names = behavior.get("BehaviorNames", "")
    if behavior.get("WeaponEvidenceConfidence") == "ExclusiveNameEvidence":
        weapon_family = behavior.get("WeaponFamily", "") or "Unknown"
        weapon_source = "Behavior:CustomManualSelectorGenerator"
        weapon_evidence = f"All mapped behavior names explicitly identify {weapon_family}: {behavior_names}"
    else:
        weapon_family = "Unknown"
        weapon_source = ""
        weapon_evidence = ""

    if curated:
        folder, catalog_type, token = curated
        variant = safe_name(contract.get("ActionVariant", ""), catalog_type)
        file_token = f"{token}_{variant}" if variant != token else token
        source = "CuratedSemantic"
        evidence = (
            f"{contract.get('SemanticConfidence')} semantic record: "
            f"{contract.get('ActionType')}/{contract.get('ActionVariant')}"
        )
        review_status = "Reviewed"
    elif "8" in flags:
        folder = "02_Defense/Dodges/TAE_DodgeFlag"
        catalog_type = "Dodge"
        file_token = "Dodge_TAE"
        source = "TAE:ChrActionFlag"
        evidence = "FlagType=8 (Flag As Dodging)"
        review_status = "EvidenceOnly"
    elif "1" in event_types:
        folder = "01_Combat/Attacks/TAE_AttackWindow"
        catalog_type = "AttackWindow"
        file_token = "AttackWindow_TAE"
        source = "TAE:AttackBehavior"
        evidence = "EventType=1 (AttackBehavior window)"
        review_status = "EvidenceOnly"
    elif "2" in event_types:
        folder = "01_Combat/Projectiles/TAE_ProjectileSpawn"
        catalog_type = "ProjectileSpawn"
        file_token = "Projectile_TAE"
        source = "TAE:BulletBehavior"
        evidence = "EventType=2 (BulletBehavior spawn)"
        review_status = "EvidenceOnly"
    elif "65" in event_types:
        folder = "04_Items/ConsumeGoods/TAE_ConsumeCurrentGoods"
        catalog_type = "ConsumeGoods"
        file_token = "ConsumeGoods_TAE"
        source = "TAE:ConsumeCurrentGoods"
        evidence = "EventType=65 (ConsumeCurrentGoods)"
        review_status = "EvidenceOnly"
    elif "900" in event_types or "901" in event_types:
        folder = "06_Interactions/Mounted/TAE_MountControl"
        catalog_type = "Mounted"
        file_token = "Mounted_TAE"
        source = "TAE:MountControl"
        evidence = "EventType=900/901 (mount attachment or camera transfer)"
        review_status = "EvidenceOnly"
    elif flags.intersection(DEATH_FLAGS):
        folder = "05_Reactions/Death_Despawn/TAE_DeathFlags"
        catalog_type = "DeathOrDespawn"
        file_token = "DeathDespawn_TAE"
        source = "TAE:ChrActionFlag"
        matched = ",".join(sorted(flags.intersection(DEATH_FLAGS), key=int))
        evidence = f"Death/despawn FlagType={matched}"
        review_status = "EvidenceOnly"
    elif "902" in event_types:
        folder = "06_Interactions/Mounted/TAE_RideTurning"
        catalog_type = "MountedRideTurning"
        file_token = "RideTurning_TAE"
        source = "TAE:RideTurning"
        evidence = "EventType=902 (ride turning event)"
        review_status = "EvidenceOnly"
    elif "74" in flags:
        folder = "06_Interactions/Ladder/TAE_LadderCollision"
        catalog_type = "LadderCollision"
        file_token = "LadderCollision_TAE"
        source = "TAE:ChrActionFlag"
        evidence = "FlagType=74 (Ladder Collision)"
        review_status = "EvidenceOnly"
    elif "32" in event_types or "33" in event_types:
        folder = "07_Equipment/WeaponStyleSwitch/TAE_WeaponEvents"
        catalog_type = "WeaponStyleSwitch"
        file_token = "WeaponSwitch_TAE"
        source = "TAE:WeaponConfiguration"
        evidence = "EventType=32/33 (SetWeaponStyle or SwitchWeapon)"
        review_status = "EvidenceOnly"
    elif "64" in event_types:
        folder = "08_Magic/Cast/TAE_CastHighlightedMagic"
        catalog_type = "MagicCast"
        file_token = "MagicCast_TAE"
        source = "TAE:CastHighlightedMagic"
        evidence = "EventType=64 (CastHighlightedMagic)"
        review_status = "EvidenceOnly"
    elif weapon_source:
        motion_kind = "InPlace" if contract.get("RootMotionStatus") == "InPlace" else "RootMotion"
        folder = (
            f"90_NeedsReview/01_ByWeapon/{safe_name(weapon_family, 'Unknown')}/"
            f"{motion_kind}/ID_Banks/{id_bank}"
        )
        catalog_type = "WeaponFamilyNeedsReview"
        file_token = f"{safe_name(weapon_family, 'Weapon')}_{motion_kind}_NeedsReview"
        source = weapon_source
        evidence = f"{weapon_evidence}; exact action remains unreviewed"
        review_status = "NeedsReview"
    elif "792" in event_types:
        folder = f"90_NeedsReview/02_FootstepCandidates/ID_Banks/{id_bank}"
        catalog_type = "FootstepCandidate"
        file_token = "FootstepCandidate_TAE"
        source = "TAE:FootSfxParam"
        evidence = "EventType=792 (footstep sound); exact motion remains unreviewed"
        review_status = "NeedsReview"
    elif not has_tae:
        folder = f"90_NeedsReview/05_MissingTAE/ID_Banks/{id_bank}"
        catalog_type = "MissingTAE"
        file_token = "MissingTAE_NeedsReview"
        source = "MissingTAE"
        evidence = "TAE entry missing"
        review_status = "NeedsReview"
    elif contract.get("RootMotionStatus") != "InPlace":
        folder = f"90_NeedsReview/03_RootMotion/ID_Banks/{id_bank}"
        catalog_type = "RootMotionNeedsReview"
        file_token = "RootMotion_NeedsReview"
        source = "TechnicalMotionContract"
        evidence = "Root translation is present; exact action remains unreviewed"
        review_status = "NeedsReview"
    else:
        folder = f"90_NeedsReview/04_InPlace/ID_Banks/{id_bank}"
        catalog_type = "InPlaceNeedsReview"
        file_token = "InPlace_NeedsReview"
        source = "TechnicalMotionContract"
        evidence = "No root translation; exact action remains unreviewed"
        review_status = "NeedsReview"

    library_id = contract["LibraryID"]
    return {
        "LibraryID": library_id,
        "AnimationID": animation_id,
        "RelativeFolder": folder,
        "FileStem": f"{animation_id}__{file_token}__{library_id}",
        "CatalogCategory": folder.split("/", 1)[0],
        "CatalogType": catalog_type,
        "ClassificationSource": source,
        "Evidence": evidence,
        "ReviewStatus": review_status,
        "IDBank": id_bank,
        "WeaponFamily": weapon_family,
        "WeaponClassificationSource": weapon_source,
        "WeaponEvidence": weapon_evidence,
    }


def update_contract(contract: dict, entry: dict[str, object]) -> dict:
    contract = dict(contract)
    for field in (
        "CatalogCategory",
        "CatalogType",
        "ClassificationSource",
        "Evidence",
        "ReviewStatus",
        "IDBank",
        "WeaponFamily",
        "WeaponClassificationSource",
        "WeaponEvidence",
    ):
        contract[field] = entry[field]
    labels = [
        label
        for label in contract.get("Labels", [])
        if not label.startswith(
            ("Catalog:", "CatalogType:", "ReviewStatus:", "IDBank:", "WeaponFamily:")
        )
    ]
    labels.extend(
        [
            f"Catalog:{entry['CatalogCategory']}",
            f"CatalogType:{entry['CatalogType']}",
            f"ReviewStatus:{entry['ReviewStatus']}",
            f"IDBank:{entry['IDBank']}",
        ]
    )
    if entry["WeaponFamily"] != "Unknown":
        labels.append(f"WeaponFamily:{entry['WeaponFamily']}")
    contract["Labels"] = labels
    return contract


def find_phase8_contracts(phase8_root: Path) -> dict[str, tuple[Path, dict]]:
    result: dict[str, tuple[Path, dict]] = {}
    for path in phase8_root.rglob("*.animlib.json"):
        contract = json.loads(path.read_text(encoding="utf-8-sig"))
        library_id = contract.get("LibraryID")
        if library_id and library_id not in result:
            result[library_id] = (path, contract)
    return result


def move_phase8_artifacts(phase8_root: Path, entries: list[dict[str, object]]) -> None:
    contracts = find_phase8_contracts(phase8_root)
    moved = 0
    for entry in entries:
        library_id = str(entry["LibraryID"])
        if library_id not in contracts:
            raise RuntimeError(f"Phase 8 contract is missing for {library_id}")
        contract_path, contract = contracts[library_id]
        old_stem = contract_path.name[: -len(".animlib.json")]
        destination_folder = phase8_root / str(entry["RelativeFolder"])
        destination_folder.mkdir(parents=True, exist_ok=True)
        new_stem = str(entry["FileStem"])
        suffixes = (".fbx", ".animlib.json", ".export.json", ".validation.json")
        for suffix in suffixes:
            source = contract_path.parent / f"{old_stem}{suffix}"
            destination = destination_folder / f"{new_stem}{suffix}"
            if source.resolve() == destination.resolve():
                continue
            if not source.is_file():
                if suffix in {".fbx", ".animlib.json"}:
                    raise FileNotFoundError(source)
                continue
            if destination.exists():
                raise RuntimeError(f"Phase 8 destination already exists: {destination}")
            shutil.move(str(source), str(destination))
        destination_contract = destination_folder / f"{new_stem}.animlib.json"
        write_json_atomic(destination_contract, update_contract(contract, entry))
        moved += 1

    for folder in sorted((path for path in phase8_root.rglob("*") if path.is_dir()), reverse=True):
        try:
            folder.rmdir()
        except OSError:
            pass
    print(f"PHASE8_ORGANIZED:{moved}")


def sync_phase9_manifest(
    config: dict,
    library_root: Path,
    entries: list[dict[str, object]],
) -> None:
    phase = config["unity_import"]
    phase8_root = resolve_from_library(library_root, phase["phase9_input"])
    phase9_root = resolve_from_library(library_root, phase["phase9_output"])
    unity_root = resolve_from_library(library_root, phase["unity_asset_root"])
    phase8_contracts = find_phase8_contracts(phase8_root)
    imports = []
    for entry in entries:
        library_id = str(entry["LibraryID"])
        source_contract, contract = phase8_contracts[library_id]
        source_fbx = source_contract.with_name(source_contract.name.replace(".animlib.json", ".fbx"))
        destination_folder = unity_root / str(entry["RelativeFolder"])
        destination_fbx = destination_folder / f"{entry['FileStem']}.fbx"
        destination_contract = destination_folder / f"{entry['FileStem']}.animlib.json"
        if not source_fbx.is_file() or not destination_fbx.is_file() or not destination_contract.is_file():
            raise RuntimeError(f"Organized asset set is incomplete for {library_id}")
        hashes = {sha256_for(source_fbx), sha256_for(destination_fbx), contract["FBXSHA256"]}
        if len(hashes) != 1:
            raise RuntimeError(f"FBX hash mismatch for {library_id}")
        imports.append(
            {
                "AnimationID": entry["AnimationID"],
                "LibraryID": library_id,
                "SourceFBX": source_fbx.as_posix(),
                "SourceMetadata": source_contract.as_posix(),
                "DestinationFBX": destination_fbx.as_posix(),
                "DestinationMetadata": destination_contract.as_posix(),
                "FBXSHA256": contract["FBXSHA256"],
                "MetadataSHA256": sha256_for(destination_contract),
            }
        )
    manifest = {
        "schemaVersion": 1,
        "status": "READY",
        "pipelineVersion": config["pipeline_version"],
        "unityAssetRoot": unity_root.as_posix(),
        "importCount": len(imports),
        "imports": imports,
    }
    write_json_atomic(phase9_root / "staging_manifest.json", manifest)
    print(f"PHASE9_MANIFEST_SYNCED:{len(imports)}")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    import_root = resolve_from_library(library_root, config["unity_import"]["unity_asset_root"])
    tae_folder = (library_root.parent / "Elden/.pipeline/work/phase4/a00-tae").resolve()
    behavior_evidence = read_behavior_evidence(database_root / BEHAVIOR_EVIDENCE_CSV)
    records = read_contracts(import_root)
    contract_ids = {str(contract["AnimationID"]) for _, contract in records}
    evidence_ids = set(behavior_evidence)
    if evidence_ids != contract_ids:
        missing = sorted(contract_ids - evidence_ids)
        extra = sorted(evidence_ids - contract_ids)
        raise RuntimeError(
            "Behavior evidence does not match the imported catalog: "
            f"missing={len(missing)}, extra={len(extra)}"
        )
    entries = [classify(contract, tae_folder, behavior_evidence) for _, contract in records]
    entries.sort(key=lambda value: str(value["AnimationID"]))
    if len(entries) != int(config["unity_import"]["expected_import_count"]):
        raise RuntimeError(
            f"Imported contract count {len(entries)} does not match expected "
            f"{config['unity_import']['expected_import_count']}"
        )
    destinations = {
        f"{entry['RelativeFolder']}/{entry['FileStem']}" for entry in entries
    }
    if len(destinations) != len(entries):
        raise RuntimeError("Catalog contains duplicate destinations")

    summary = {
        "total": len(entries),
        "byType": dict(sorted(Counter(str(entry["CatalogType"]) for entry in entries).items())),
        "byReviewStatus": dict(sorted(Counter(str(entry["ReviewStatus"]) for entry in entries).items())),
        "byWeaponFamily": dict(
            sorted(
                Counter(
                    str(entry["WeaponFamily"])
                    for entry in entries
                    if entry["WeaponFamily"] != "Unknown"
                ).items()
            )
        ),
    }
    payload = {
        "schemaVersion": 1,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "classificationPolicy": (
            "Curated semantics, explicit TAE evidence, exclusive behavior weapon-family evidence, "
            "then technical motion and ID-bank fallback"
        ),
        "summary": summary,
        "entries": entries,
    }
    write_json_atomic(database_root / CATALOG_FILE, payload)
    write_csv_atomic(database_root / CATALOG_CSV, entries)

    if args.apply_phase8:
        phase8_root = resolve_from_library(library_root, config["unity_import"]["phase9_input"])
        move_phase8_artifacts(phase8_root, entries)
    if args.sync_phase9:
        sync_phase9_manifest(config, library_root, entries)

    print(f"PLAYER_CATALOG_COMPLETE:{len(entries)}")
    for key, value in summary["byType"].items():
        print(f"  {key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
