#!/usr/bin/env python3
"""Convert, retarget, validate, export and stage the canonical ER player animation set."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
from pathlib import Path


CANONICAL_PATH_TOKEN = "c0000-a00-hi"
STAGES = ("convert", "retarget", "export", "stage")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--stage", choices=("all", *STAGES), default="all")
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--conversion-batch-size", type=int, default=100)
    parser.add_argument("--limit", type=int)
    return parser.parse_args()


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_csv(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        return list(reader.fieldnames or []), list(reader)


def write_csv_atomic(path: Path, fields: list[str], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", newline="", delete=False, dir=path.parent) as target:
        writer = csv.DictWriter(target, fieldnames=fields, extrasaction="ignore", lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
        temporary = Path(target.name)
    os.replace(temporary, path)


def write_json_atomic(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, dir=path.parent) as target:
        json.dump(value, target, ensure_ascii=False, indent=2)
        target.write("\n")
        temporary = Path(target.name)
    os.replace(temporary, path)


def resolve_library_root(config_path: Path, config: dict) -> Path:
    return (config_path.parent.parent / config.get("project_root", ".")).resolve()


def resolve_from_library(library_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def safe_name(value: str, fallback: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_-]+", "_", (value or "").strip()).strip("_")
    return cleaned or fallback


def canonical_rows(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    selected: dict[str, dict[str, str]] = {}
    for row in rows:
        if row.get("RecordKind") != "AnimationClip" or CANONICAL_PATH_TOKEN not in row.get("OriginalPath", ""):
            continue
        animation_id = row.get("AnimationID", "")
        if not animation_id:
            continue
        if animation_id in selected:
            raise RuntimeError(f"Duplicate canonical animation ID: {animation_id}")
        selected[animation_id] = row
    return [selected[key] for key in sorted(selected)]


def load_binding_scan(database_root: Path) -> dict[str, dict]:
    scan_path = database_root / "hkx_binding_scan.json"
    if not scan_path.is_file():
        raise FileNotFoundError(
            f"Binding scan is required before bulk conversion: {scan_path}. "
            "Run inspect_animation_binding_blender.py with --hkx-root first."
        )
    scan = json.loads(scan_path.read_text(encoding="utf-8"))
    return {item["animationId"]: item for item in scan}


def apply_catalog(rows: list[dict[str, str]], database_root: Path) -> None:
    catalog_path = database_root / "player_animation_catalog.json"
    if not catalog_path.is_file():
        return
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    entries = {entry["AnimationID"]: entry for entry in catalog.get("entries", [])}
    for row in rows:
        entry = entries.get(row.get("AnimationID", ""))
        if not entry:
            continue
        row["_CatalogRelativeFolder"] = entry["RelativeFolder"]
        row["_CatalogFileStem"] = entry["FileStem"]
        row["_CatalogCategory"] = entry["CatalogCategory"]
        row["_CatalogType"] = entry["CatalogType"]
        row["_ClassificationSource"] = entry["ClassificationSource"]
        row["_CatalogEvidence"] = entry["Evidence"]
        row["_ReviewStatus"] = entry["ReviewStatus"]
        row["_IDBank"] = entry["IDBank"]


def standalone_rows(rows: list[dict[str, str]], scan_by_id: dict[str, dict]) -> list[dict[str, str]]:
    result = []
    for row in canonical_rows(rows):
        binding = scan_by_id.get(row["AnimationID"])
        if binding and binding.get("status") == "PASS" and int(binding.get("blendHint", -1)) == 0:
            result.append(row)
    return result


def artifact_paths(library_root: Path, config: dict, row: dict[str, str]) -> dict[str, Path]:
    animation_id = row["AnimationID"]
    conversion_root = resolve_from_library(library_root, config["conversion"]["phase5_output"])
    retarget_root = resolve_from_library(library_root, config["retarget"]["phase7_output"])
    export_root = resolve_from_library(library_root, config["fbx_export"]["phase8_output"])
    relative_folder = row.get("_CatalogRelativeFolder", "")
    file_stem = row.get("_CatalogFileStem", "")
    if not relative_folder or not file_stem:
        relative_folder = safe_name(row.get("ActionType", ""), "Unknown")
        variant = safe_name(
            row.get("ActionVariant", ""),
            safe_name(row.get("FriendlyName", ""), animation_id),
        )
        file_stem = f"Player_{relative_folder}_{variant}__{row['LibraryID']}"
    export_folder = export_root / Path(relative_folder)
    return {
        "hkx": (library_root / row["OriginalPath"]).resolve(),
        "convertedBlend": conversion_root / animation_id / f"{animation_id}.blend",
        "conversionMetadata": conversion_root / animation_id / f"{animation_id}.conversion.json",
        "retargetedBlend": retarget_root / animation_id / f"{animation_id}.blend",
        "retargetMetadata": retarget_root / animation_id / f"{animation_id}.retarget.json",
        "fbx": export_folder / f"{file_stem}.fbx",
        "exportReceipt": export_folder / f"{file_stem}.export.json",
        "fbxValidation": export_folder / f"{file_stem}.validation.json",
        "contract": export_folder / f"{file_stem}.animlib.json",
    }


def read_json(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def conversion_pass(paths: dict[str, Path], config: dict) -> bool:
    metadata = read_json(paths["conversionMetadata"])
    phase = config["conversion"]
    return bool(
        paths["convertedBlend"].is_file()
        and metadata
        and metadata.get("sourceAnimationId") == paths["convertedBlend"].stem
        and metadata.get("blendHint") == 0
        and metadata.get("invalidTransformCount") == 0
        and not metadata.get("missingBodyBones")
        and float(metadata.get("maximumPoseExtentFromPelvis", 0.0)) >= float(phase["minimum_pose_extent"])
        and float(metadata.get("maximumPoseExtentFromPelvis", math.inf)) <= float(phase["maximum_pose_extent"])
        and metadata.get("coordinateSpace") == "BlenderZUp_SourceNative"
        and int(metadata.get("frameCount", 0)) > 1
    )


def root_only_motion(metadata: dict | None) -> bool:
    """Return true when the mapped pose only translates/rotates the humanoid root.

    Unity converts pelvis-only Humanoid motion into RootT/RootQ curves. Such tracks
    contain no relative body pose animation and must not be published as standalone
    gameplay clips (they otherwise look exactly like the original collapsed imports).
    """
    if not metadata:
        return False
    animated_bones = set(metadata.get("poseMotion", {}).get("animatedBones", []))
    return animated_bones == {"pelvis"}


def retarget_pass(paths: dict[str, Path], config: dict) -> bool:
    metadata = read_json(paths["retargetMetadata"])
    phase = config["retarget"]
    pose_motion = metadata.get("poseMotion", {}) if metadata else {}
    return bool(
        paths["retargetedBlend"].is_file()
        and metadata
        and metadata.get("invalidTransformCount") == 0
        and float(metadata.get("maximumPoseExtentFromPelvis", 0.0)) >= float(phase["minimum_pose_extent"])
        and float(metadata.get("maximumPoseExtentFromPelvis", math.inf)) <= float(phase["maximum_pose_extent"])
        and float(metadata.get("rootTranslationError", math.inf)) <= float(phase["root_translation_tolerance"])
        and int(metadata.get("frameCount", 0)) > 1
        and int(pose_motion.get("animatedBoneCount", 0)) > 0
        and not root_only_motion(metadata)
    )


def retarget_terminal(paths: dict[str, Path], config: dict) -> bool:
    if retarget_pass(paths, config):
        return True
    metadata = read_json(paths["retargetMetadata"])
    phase = config["retarget"]
    return bool(
        paths["retargetedBlend"].is_file()
        and metadata
        and metadata.get("invalidTransformCount") == 0
        and float(metadata.get("maximumPoseExtentFromPelvis", 0.0)) >= float(phase["minimum_pose_extent"])
        and float(metadata.get("maximumPoseExtentFromPelvis", math.inf)) <= float(phase["maximum_pose_extent"])
        and float(metadata.get("rootTranslationError", math.inf)) <= float(phase["root_translation_tolerance"])
        and int(metadata.get("frameCount", 0)) > 1
        and (
            int(metadata.get("poseMotion", {}).get("animatedBoneCount", -1)) == 0
            or root_only_motion(metadata)
        )
    )


def export_pass(paths: dict[str, Path]) -> bool:
    contract = read_json(paths["contract"])
    validation = read_json(paths["fbxValidation"])
    return bool(
        paths["fbx"].is_file()
        and contract
        and validation
        and validation.get("status") == "PASS"
        and contract.get("AnimationID") == validation.get("animationId")
        and contract.get("FBXSHA256") == sha256_for(paths["fbx"])
    )


def run_command(command: list[str], environment: dict[str, str] | None = None) -> tuple[int, str]:
    result = subprocess.run(
        command,
        cwd=Path(__file__).resolve().parents[2],
        env=environment,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=600,
        check=False,
    )
    output = (result.stdout or "") + (result.stderr or "")
    return result.returncode, output[-12000:]


def convert_batches(
    rows: list[dict[str, str]],
    paths_by_id: dict[str, dict[str, Path]],
    config: dict,
    library_root: Path,
    batch_size: int,
) -> dict[str, str]:
    pending = [row for row in rows if not conversion_pass(paths_by_id[row["AnimationID"]], config)]
    if not pending:
        return {}

    blender = resolve_from_library(library_root, config["tools"]["blender"])
    script = Path(__file__).with_name("convert_hkx_batch_blender.py").resolve()
    semantic = config["semantic_preview"]
    compendium_candidates = list(
        (library_root / "02_UNPACKED" / "EldenVins" / "mod" / "chr" / "c0000-a00-hi").rglob("*.compendium")
    )
    if len(compendium_candidates) != 1:
        raise RuntimeError(f"Expected one a00_hi compendium, found {len(compendium_candidates)}")

    batch_root = resolve_from_library(library_root, config["conversion"]["phase5_output"]) / "_batch"
    environment = os.environ.copy()
    environment["BLENDER_USER_SCRIPTS"] = str(
        resolve_from_library(library_root, config["tools"]["hkx_blender_bridge"]).parent.parent
    )
    errors: dict[str, str] = {}
    for offset in range(0, len(pending), batch_size):
        batch = pending[offset : offset + batch_size]
        manifest_path = batch_root / f"manifest_{offset:05d}.json"
        result_path = batch_root / f"results_{offset:05d}.json"
        manifest = {
            "flver": str(resolve_from_library(library_root, semantic["flver"])),
            "baseAnibnd": str(resolve_from_library(library_root, semantic["base_anibnd"])),
            "animationAnibnd": str(resolve_from_library(library_root, semantic["animation_anibnd"])),
            "compendium": str(compendium_candidates[0]),
            "animations": [
                {
                    "animationId": row["AnimationID"],
                    "hkxPath": str(paths_by_id[row["AnimationID"]]["hkx"]),
                    "outputBlend": str(paths_by_id[row["AnimationID"]]["convertedBlend"]),
                    "outputMetadata": str(paths_by_id[row["AnimationID"]]["conversionMetadata"]),
                }
                for row in batch
            ],
        }
        write_json_atomic(manifest_path, manifest)
        print(f"[convert] batch {offset // batch_size + 1}/{math.ceil(len(pending) / batch_size)} ({len(batch)} clips)")
        code, output = run_command(
            [
                str(blender),
                "--background",
                "--factory-startup",
                "--python",
                str(script),
                "--",
                "--manifest",
                str(manifest_path),
                "--results",
                str(result_path),
            ],
            environment,
        )
        results = read_json(result_path) or {"results": []}
        returned = {item["animationId"]: item for item in results.get("results", [])}
        for row in batch:
            animation_id = row["AnimationID"]
            if conversion_pass(paths_by_id[animation_id], config):
                continue
            item = returned.get(animation_id)
            errors[animation_id] = item.get("error", output) if item else f"Blender exit {code}: {output}"
    return errors


def retarget_one(
    row: dict[str, str], paths: dict[str, Path], config: dict, library_root: Path
) -> tuple[str, str | None]:
    animation_id = row["AnimationID"]
    blender = resolve_from_library(library_root, config["tools"]["blender"])
    phase = config["retarget"]
    command = [
        str(blender), "--background", "--factory-startup", "--python", str(Path(__file__).with_name("retarget_blender.py")), "--",
        "--source-blend", str(paths["convertedBlend"]),
        "--target-fbx", str(resolve_from_library(library_root, phase["target_fbx"])),
        "--source-profile", str(resolve_from_library(library_root, phase["source_profile"])),
        "--target-profile", str(resolve_from_library(library_root, phase["target_profile"])),
        "--retarget-map", str(resolve_from_library(library_root, phase["retarget_map"])),
        "--animation-id", animation_id,
        "--library-id", row["LibraryID"],
        "--output-blend", str(paths["retargetedBlend"]),
        "--output-metadata", str(paths["retargetMetadata"]),
    ]
    code, output = run_command(command)
    if code == 0 and retarget_pass(paths, config):
        return animation_id, None
    return animation_id, f"Blender exit {code}: {output}"


def build_contract(
    row: dict[str, str], paths: dict[str, Path], config: dict, library_root: Path, timestamp: str
) -> None:
    retarget = read_json(paths["retargetMetadata"])
    receipt = read_json(paths["exportReceipt"])
    validation = read_json(paths["fbxValidation"])
    if not retarget or not receipt or not validation:
        raise RuntimeError("Export evidence is incomplete")
    expected_pose = retarget["poseMotion"]
    root_magnitude = math.sqrt(sum(float(value) * float(value) for value in retarget["targetRootTranslation"]))
    import_profile_path = resolve_from_library(
        library_root, config["fbx_export"]["unity_import_profile"]
    )
    import_profile = json.loads(import_profile_path.read_text(encoding="utf-8"))
    avatar_asset = config["fbx_export"]["target_avatar_asset"]
    avatar_path = (library_root.parent / avatar_asset).resolve()
    contract = {
        "SchemaVersion": 1,
        "LibraryID": row["LibraryID"],
        "AnimationID": row["AnimationID"],
        "SourceHash": row["SourceHash"],
        "SourceBlendSHA256": sha256_for(paths["retargetedBlend"]),
        "FBXSHA256": sha256_for(paths["fbx"]),
        "PipelineVersion": config["pipeline_version"],
        "SkeletonProfile": retarget["targetProfile"],
        "SourceSkeletonProfile": retarget["sourceProfile"],
        "RetargetProfile": retarget["retargetProfile"],
        "RootMotionStatus": "RootMotionPreservedScaled" if root_magnitude > 0.001 else "InPlace",
        "RootTranslation": retarget["targetRootTranslation"],
        "PoseAnimatedBoneCount": expected_pose["animatedBoneCount"],
        "PoseTotalRotationDegrees": expected_pose["totalMaxRotationDegrees"],
        "MinimumNonConstantPoseCurves": max(1, math.ceil(int(expected_pose["animatedBoneCount"]) * 0.25)),
        "ExportTimestamp": timestamp,
        "FriendlyName": row.get("FriendlyName", "Unknown"),
        "SemanticConfidence": row.get("SemanticConfidence", "Unknown"),
        "CharacterType": row.get("CharacterType", "Player"),
        "WeaponFamily": row.get("WeaponFamily", "Unknown"),
        "ActionType": row.get("ActionType", "Unknown"),
        "ActionVariant": row.get("ActionVariant", "Unknown"),
        "Direction": row.get("Direction", "Unknown"),
        "CatalogCategory": row.get("_CatalogCategory", "Unsorted"),
        "CatalogType": row.get("_CatalogType", "Unknown"),
        "ClassificationSource": row.get("_ClassificationSource", "LegacyFallback"),
        "Evidence": row.get("_CatalogEvidence", "Catalog entry unavailable"),
        "ReviewStatus": row.get("_ReviewStatus", "NeedsReview"),
        "IDBank": row.get("_IDBank", "Unknown"),
        "Loop": row.get("Loop", "").lower() in {"yes", "true", "1"},
        "FrameCount": retarget["frameCount"],
        "FrameRate": retarget["frameRate"],
        "DurationSeconds": retarget["durationSeconds"],
        "BindPoseFrame": receipt["bindPoseFrame"],
        "ClipFrameStart": receipt["frameStart"],
        "ClipFrameEnd": receipt["frameEnd"],
        "UnityImportProfile": import_profile["id"],
        "TargetAvatarAsset": avatar_asset,
        "TargetAvatarSHA256": sha256_for(avatar_path),
        "Labels": [
            "AnimationLibrary",
            f"LibraryID:{row['LibraryID']}",
            f"Action:{safe_name(row.get('ActionType', ''), 'Unknown')}",
            f"WeaponFamily:{safe_name(row.get('WeaponFamily', ''), 'Unknown')}",
            f"CharacterType:{safe_name(row.get('CharacterType', ''), 'Unknown')}",
            f"SemanticConfidence:{safe_name(row.get('SemanticConfidence', ''), 'Unknown')}",
            f"Source:{row['SourceGame']}",
            f"Catalog:{safe_name(row.get('_CatalogCategory', ''), 'Unsorted')}",
            f"CatalogType:{safe_name(row.get('_CatalogType', ''), 'Unknown')}",
            f"ReviewStatus:{safe_name(row.get('_ReviewStatus', ''), 'NeedsReview')}",
            f"IDBank:{safe_name(row.get('_IDBank', ''), 'Unknown')}",
        ],
    }
    write_json_atomic(paths["contract"], contract)


def export_one(
    row: dict[str, str], paths: dict[str, Path], config: dict, library_root: Path, timestamp: str
) -> tuple[str, str | None]:
    animation_id = row["AnimationID"]
    blender = resolve_from_library(library_root, config["tools"]["blender"])
    paths["fbx"].parent.mkdir(parents=True, exist_ok=True)
    export_command = [
        str(blender), "--background", "--factory-startup", str(paths["retargetedBlend"]),
        "--python", str(Path(__file__).with_name("export_fbx_blender.py")), "--",
        "--retarget-metadata", str(paths["retargetMetadata"]),
        "--output-fbx", str(paths["fbx"]),
        "--output-receipt", str(paths["exportReceipt"]),
    ]
    code, output = run_command(export_command)
    if code != 0:
        return animation_id, f"FBX export exit {code}: {output}"
    validate_command = [
        str(blender), "--background", "--factory-startup", "--python", str(Path(__file__).with_name("validate_fbx_blender.py")), "--",
        "--fbx", str(paths["fbx"]),
        "--retarget-metadata", str(paths["retargetMetadata"]),
        "--output", str(paths["fbxValidation"]),
    ]
    code, output = run_command(validate_command)
    if code != 0:
        return animation_id, f"FBX validation exit {code}: {output}"
    try:
        build_contract(row, paths, config, library_root, timestamp)
    except Exception as exception:
        return animation_id, f"Contract error: {type(exception).__name__}: {exception}"
    if export_pass(paths):
        return animation_id, None
    return animation_id, "Export artifacts did not pass their persisted validation contract"


def run_parallel(
    label: str,
    rows: list[dict[str, str]],
    worker,
    workers: int,
) -> dict[str, str]:
    errors: dict[str, str] = {}
    if not rows:
        return errors
    print(f"[{label}] processing {len(rows)} clips with {workers} workers")
    with ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {executor.submit(worker, row): row["AnimationID"] for row in rows}
        completed = 0
        for future in as_completed(futures):
            animation_id = futures[future]
            completed += 1
            try:
                _, error = future.result()
            except Exception as exception:
                error = f"{type(exception).__name__}: {exception}"
            if error:
                errors[animation_id] = error
                print(f"[{label}] FAIL {animation_id}: {error[-600:]}")
            if completed % 25 == 0 or completed == len(rows):
                print(f"[{label}] {completed}/{len(rows)} complete; failures={len(errors)}")
    return errors


def stage_unity(
    rows: list[dict[str, str]], paths_by_id: dict[str, dict[str, Path]], config: dict, library_root: Path
) -> dict:
    phase = config["unity_import"]
    export_root = resolve_from_library(library_root, phase["phase9_input"])
    unity_root = resolve_from_library(library_root, phase["unity_asset_root"])
    imports = []
    for row in rows:
        paths = paths_by_id[row["AnimationID"]]
        if not conversion_pass(paths, config) or not retarget_pass(paths, config) or not export_pass(paths):
            continue
        relative_fbx = paths["fbx"].relative_to(export_root)
        destination_fbx = unity_root / relative_fbx
        destination_contract = destination_fbx.with_suffix(".animlib.json")
        destination_fbx.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(paths["contract"], destination_contract)
        if not destination_fbx.is_file() or sha256_for(destination_fbx) != sha256_for(paths["fbx"]):
            shutil.copy2(paths["fbx"], destination_fbx)
        contract = read_json(paths["contract"])
        imports.append(
            {
                "AnimationID": row["AnimationID"],
                "LibraryID": row["LibraryID"],
                "SourceFBX": paths["fbx"].as_posix(),
                "SourceMetadata": paths["contract"].as_posix(),
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
    manifest_path = resolve_from_library(library_root, phase["phase9_output"]) / "staging_manifest.json"
    write_json_atomic(manifest_path, manifest)
    return manifest


def write_report(
    path: Path,
    rows: list[dict[str, str]],
    paths_by_id: dict[str, dict[str, Path]],
    config: dict,
    errors: dict[str, dict[str, str]],
    staged_count: int,
    inventory: dict[str, int],
) -> None:
    converted = sum(conversion_pass(paths_by_id[row["AnimationID"]], config) for row in rows)
    retargeted = sum(retarget_pass(paths_by_id[row["AnimationID"]], config) for row in rows)
    exported = sum(
        retarget_pass(paths_by_id[row["AnimationID"]], config)
        and export_pass(paths_by_id[row["AnimationID"]])
        for row in rows
    )
    static = sum(
        1
        for row in rows
        if (metadata := read_json(paths_by_id[row["AnimationID"]]["retargetMetadata"]))
        and int(metadata.get("poseMotion", {}).get("animatedBoneCount", 0)) == 0
    )
    root_only = sum(
        1
        for row in rows
        if root_only_motion(read_json(paths_by_id[row["AnimationID"]]["retargetMetadata"]))
    )
    lines = [
        "# Canonical ER player animation batch",
        "",
        f"Updated: `{datetime.now(timezone.utc).isoformat()}`",
        "",
        f"- Canonical HKX entries: **{inventory['canonical']}**",
        f"- Standalone normal animations: **{inventory['normal']}**",
        f"- Additive animations deferred for reference-pose processing: **{inventory['additive']}**",
        f"- Non-animation/unsupported Havok resources excluded: **{inventory['unsupported']}**",
        f"- Conversion PASS: **{converted}**",
        f"- Retarget PASS: **{retargeted}**",
        f"- FBX reimport PASS: **{exported}**",
        f"- Static pose/reference entries excluded after retarget: **{static}**",
        f"- Root-only technical tracks excluded after retarget: **{root_only}**",
        f"- Staged for Unity: **{staged_count}**",
        "- Semantic names remain `Unknown` until separately reviewed; mechanical PASS does not assert gameplay meaning.",
        "",
        "## Failures",
        "",
    ]
    failure_ids = sorted({animation_id for stage in errors.values() for animation_id in stage})
    if not failure_ids:
        lines.append("None.")
    else:
        lines.extend(["| Animation | Stage | Error |", "|---|---|---|"])
        for animation_id in failure_ids:
            for stage, stage_errors in errors.items():
                if animation_id in stage_errors:
                    error = stage_errors[animation_id].replace("\n", " ").replace("|", "\\|")[-800:]
                    lines.append(f"| `{animation_id}` | {stage} | {error} |")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    if args.workers < 1 or args.conversion_batch_size < 1:
        raise ValueError("workers and conversion-batch-size must be positive")
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    animations_path = database_root / "animations.csv"
    fields, all_rows = load_csv(animations_path)
    apply_catalog(all_rows, database_root)
    scan_by_id = load_binding_scan(database_root)
    canonical = canonical_rows(all_rows)
    rows = standalone_rows(all_rows, scan_by_id)
    inventory = {
        "canonical": len(canonical),
        "normal": len(rows),
        "additive": sum(
            1
            for row in canonical
            if scan_by_id.get(row["AnimationID"], {}).get("status") == "PASS"
            and int(scan_by_id[row["AnimationID"]].get("blendHint", -1)) == 1
        ),
        "unsupported": sum(
            1
            for row in canonical
            if scan_by_id.get(row["AnimationID"], {}).get("status") != "PASS"
        ),
    }
    if args.limit is not None:
        rows = rows[: args.limit]
    paths_by_id = {
        row["AnimationID"]: artifact_paths(library_root, config, row)
        for row in rows
    }
    errors: dict[str, dict[str, str]] = {stage: {} for stage in STAGES}
    stages = STAGES if args.stage == "all" else (args.stage,)
    timestamp = datetime.now(timezone.utc).isoformat()

    if "convert" in stages:
        errors["convert"] = convert_batches(
            rows, paths_by_id, config, library_root, args.conversion_batch_size
        )

    if "retarget" in stages:
        pending = [
            row for row in rows
            if conversion_pass(paths_by_id[row["AnimationID"]], config)
            and not retarget_terminal(paths_by_id[row["AnimationID"]], config)
        ]
        errors["retarget"] = run_parallel(
            "retarget",
            pending,
            lambda row: retarget_one(row, paths_by_id[row["AnimationID"]], config, library_root),
            args.workers,
        )

    if "export" in stages:
        pending = [
            row for row in rows
            if retarget_pass(paths_by_id[row["AnimationID"]], config)
            and not export_pass(paths_by_id[row["AnimationID"]])
        ]
        errors["export"] = run_parallel(
            "export",
            pending,
            lambda row: export_one(row, paths_by_id[row["AnimationID"]], config, library_root, timestamp),
            args.workers,
        )

    staged_count = 0
    if "stage" in stages:
        manifest = stage_unity(rows, paths_by_id, config, library_root)
        staged_count = manifest["importCount"]

    now = datetime.now(timezone.utc).isoformat()
    selected_ids = {row["AnimationID"] for row in rows}
    canonical_ids = {row["AnimationID"] for row in canonical}
    for row in all_rows:
        animation_id = row.get("AnimationID")
        if animation_id not in canonical_ids or CANONICAL_PATH_TOKEN not in row.get("OriginalPath", ""):
            continue
        binding = scan_by_id.get(animation_id, {})
        if binding.get("status") != "PASS":
            row["ConversionStatus"] = "UnsupportedHKXResource"
            row["RetargetStatus"] = "NotApplicable"
            row["QAStatus"] = "Rejected"
            row["Notes"] = (
                f"Excluded by Havok binding scan: {binding.get('error', 'unknown parse failure')}"
            )
            row["UpdatedAt"] = now
            continue
        if int(binding.get("blendHint", -1)) == 1:
            row["ConversionStatus"] = "AdditiveDeferred"
            row["RetargetStatus"] = "RequiresReferencePose"
            row["QAStatus"] = "NeedsReview"
            row["Notes"] = (
                "Havok blendHint=1. Additive layer requires an explicit reference/base pose and is "
                "excluded from the standalone full-body import path."
            )
            row["UpdatedAt"] = now
            continue
        if animation_id not in selected_ids:
            continue
        paths = paths_by_id[animation_id]
        if conversion_pass(paths, config):
            row["ConversionStatus"] = "BatchPassed"
        retarget_metadata = read_json(paths["retargetMetadata"])
        if (
            retarget_metadata
            and int(retarget_metadata.get("poseMotion", {}).get("animatedBoneCount", 0)) == 0
        ):
            row["RetargetStatus"] = "StaticPoseExcluded"
            row["UnityImportStatus"] = "NotApplicable"
            row["QAStatus"] = "Rejected"
            row["Notes"] = (
                "Retargeted pose is finite and structurally valid but contains no changing mapped bone curves; "
                "excluded from the AnimationClip library as a static pose/reference entry."
            )
        elif root_only_motion(retarget_metadata):
            row["RetargetStatus"] = "RootOnlyMotionExcluded"
            row["UnityImportStatus"] = "NotApplicable"
            row["QAStatus"] = "Rejected"
            row["Notes"] = (
                "Retargeted track changes only the humanoid pelvis/root and contains no relative body pose "
                "animation; excluded from the standalone AnimationClip library."
            )
        if retarget_pass(paths, config):
            metadata = read_json(paths["retargetMetadata"])
            row["SkeletonProfile"] = metadata["sourceProfile"]
            row["RetargetProfile"] = metadata["retargetProfile"]
            row["RetargetStatus"] = "BatchPassed"
            root = metadata["targetRootTranslation"]
            row["RootTranslation"] = ",".join(f"{float(value):.6f}" for value in root)
            row["RootMotionStatus"] = (
                "RootMotionPreservedScaled"
                if math.sqrt(sum(float(value) * float(value) for value in root)) > 0.001
                else "InPlace"
            )
        row["UpdatedAt"] = now
    write_csv_atomic(animations_path, fields, all_rows)

    report_path = resolve_from_library(library_root, config["paths"]["catalog"]) / "reports" / "player_animation_batch.md"
    write_report(report_path, rows, paths_by_id, config, errors, staged_count, inventory)
    converted_count = sum(conversion_pass(paths_by_id[row["AnimationID"]], config) for row in rows)
    retargeted_count = sum(retarget_pass(paths_by_id[row["AnimationID"]], config) for row in rows)
    exported_count = sum(
        retarget_pass(paths_by_id[row["AnimationID"]], config)
        and export_pass(paths_by_id[row["AnimationID"]])
        for row in rows
    )
    print(
        f"PLAYER_BATCH_COMPLETE:total={len(rows)};converted={converted_count};"
        f"retargeted={retargeted_count};exported={exported_count};staged={staged_count}"
    )
    return 0 if exported_count == retargeted_count or args.stage in {"convert", "retarget", "stage"} else 2


if __name__ == "__main__":
    raise SystemExit(main())
