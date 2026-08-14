#!/usr/bin/env python3
"""Build the canonical Phase 3 HKX/TAE/container identity catalog."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import re
import sys
import time
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath

from pipeline_ledger import append_pipeline_run, new_run_id


ANIMATION_FIELDS = (
    "LibraryID",
    "RecordKind",
    "SourceStage",
    "Source",
    "SourcePackageVersion",
    "SourceGame",
    "SourceCharacter",
    "CharacterType",
    "OriginalPath",
    "SourceHash",
    "SizeBytes",
    "ProvenanceID",
    "AnimationID",
    "OriginalFileName",
    "FriendlyName",
    "SemanticConfidence",
    "WeaponFamily",
    "ActionType",
    "ActionVariant",
    "Direction",
    "Loop",
    "DurationSeconds",
    "FrameRate",
    "RootMotionStatus",
    "RootTranslation",
    "RootYaw",
    "SkeletonProfile",
    "RetargetProfile",
    "ConversionStatus",
    "RetargetStatus",
    "UnityImportStatus",
    "QAStatus",
    "PairGroupID",
    "TAEReference",
    "PreviewPath",
    "Notes",
    "CreatedAt",
    "UpdatedAt",
)

CURATED_FIELDS = (
    "SourceGame",
    "SourceCharacter",
    "CharacterType",
    "FriendlyName",
    "SemanticConfidence",
    "WeaponFamily",
    "ActionType",
    "ActionVariant",
    "Direction",
    "Loop",
    "DurationSeconds",
    "FrameRate",
    "RootMotionStatus",
    "RootTranslation",
    "RootYaw",
    "SkeletonProfile",
    "RetargetProfile",
    "ConversionStatus",
    "RetargetStatus",
    "UnityImportStatus",
    "QAStatus",
    "PairGroupID",
    "TAEReference",
    "PreviewPath",
    "Notes",
    "CreatedAt",
)

IDENTITY_FIELDS = (
    "RecordKind",
    "SourceStage",
    "Source",
    "SourcePackageVersion",
    "OriginalPath",
    "SourceHash",
    "SizeBytes",
    "ProvenanceID",
    "AnimationID",
    "OriginalFileName",
)


@dataclass(frozen=True)
class Candidate:
    record_kind: str
    source_stage: str
    source: str
    source_package_version: str
    source_character: str
    original_path: str
    source_hash: str
    size_bytes: int
    provenance_id: str
    animation_id: str
    original_file_name: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--force", action="store_true", help="Rebuild the CSV even when inputs are unchanged.")
    return parser.parse_args()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def iso_utc(timestamp: float) -> str:
    return datetime.fromtimestamp(timestamp, timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def hash_json(payload: object) -> str:
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def resolve_library_root(config_path: Path, config: dict) -> Path:
    configured = Path(config.get("project_root", "."))
    return (config_path.parent.parent / configured).resolve()


def resolve_from_library(library_root: Path, configured_path: str) -> Path:
    path = Path(configured_path)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def compound_extension(path: Path, configured_extensions: list[str]) -> str:
    lower_name = path.name.casefold()
    for extension in sorted(configured_extensions, key=len, reverse=True):
        if lower_name.endswith(extension.casefold()):
            return extension.casefold()
    return path.suffix.casefold()


def stem_for(path: Path, extension: str) -> str:
    return path.name[: -len(extension)] if extension and path.name.casefold().endswith(extension) else path.stem


def source_character_for(source: str, relative_path: str) -> str:
    parts = PurePosixPath(relative_path).parts
    if source == "PlayerRepo" and len(parts) >= 2 and parts[0].casefold() == "all characters":
        return parts[1]
    match = re.search(r"(?i)(c\d{4})", relative_path)
    return match.group(1).lower() if match else "Unknown"


def record_kind_for(extension: str, path: Path, source_character: str) -> str:
    if extension == ".tae":
        return "Timeline"
    if extension in {".anibnd", ".anibnd.dcx"}:
        return "AnimationContainer"
    if extension == ".hkx":
        stem = path.stem.casefold().lstrip("!")
        path_text = path.as_posix().casefold()
        if stem == "skeleton" or ("chrbnd" in path_text and stem == source_character.casefold()):
            return "Skeleton"
        return "AnimationClip"
    return "Unknown"


def library_id_for(prefix: str, source_stage: str, source: str, original_path: str) -> str:
    identity = f"{source_stage}\0{source}\0{original_path}".encode("utf-8")
    return f"{prefix}_{hashlib.sha256(identity).hexdigest()[:24].upper()}"


def index_phase_config_hash(config: dict) -> str:
    payload = {
        "phaseVersion": config["phase_versions"]["index"],
        "rawManifestPath": f"{config['paths']['catalog']}/manifests/raw_files.json",
        "unpackedPath": config["paths"]["unpacked"],
        "databasePath": config["paths"]["database"],
        "index": config["index"],
    }
    return hash_json(payload)


def load_raw_manifest(path: Path) -> tuple[dict, dict[tuple[str, str], dict]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    records = {
        (record["Source"], record["RelativePath"]): record
        for record in payload.get("records", [])
    }
    return payload, records


def collect_raw_candidates(
    config: dict,
    library_root: Path,
    manifest_records: dict[tuple[str, str], dict],
    errors: list[str],
) -> list[Candidate]:
    candidates: list[Candidate] = []
    allowed = [extension.casefold() for extension in config["index"]["raw_extensions"]]
    visited: set[tuple[str, Path]] = set()

    for source in sorted(config.get("sources", []), key=lambda item: item["name"].casefold()):
        source_name = source["name"]
        package_version = source.get("package_version") or ""
        for root_config in sorted(source.get("roots", []), key=lambda item: item["path"].casefold()):
            physical_root = resolve_from_library(library_root, root_config["path"])
            if not physical_root.is_dir():
                continue
            prefix = PurePosixPath(root_config.get("relative_prefix", physical_root.name))
            files = (path for path in physical_root.rglob("*") if path.is_file())
            for path in sorted(files, key=lambda item: item.relative_to(physical_root).as_posix().casefold()):
                extension = compound_extension(path, allowed)
                if extension not in allowed:
                    continue
                identity = (source_name, path.resolve())
                if identity in visited:
                    continue
                visited.add(identity)

                relative = (prefix / PurePosixPath(path.relative_to(physical_root).as_posix())).as_posix()
                manifest = manifest_records.get((source_name, relative))
                if manifest is None:
                    errors.append(f"RAW file missing from Phase 1 manifest: {source_name}/{relative}")
                    continue
                stat = path.stat()
                if stat.st_size != int(manifest["SizeBytes"]) or iso_utc(stat.st_mtime) != manifest["ModifiedTime"]:
                    errors.append(f"RAW metadata changed since Phase 1: {source_name}/{relative}")
                    continue

                original_path = f"01_RAW/{source_name}/{relative}"
                character = source_character_for(source_name, relative)
                candidates.append(
                    Candidate(
                        record_kind_for(extension, path, character),
                        "RAW",
                        source_name,
                        manifest.get("SourcePackageVersion") or package_version,
                        character,
                        original_path,
                        manifest["SHA256"],
                        stat.st_size,
                        "Phase1RawManifest",
                        stem_for(path, extension),
                        path.name,
                    )
                )
    return candidates


def collect_unpacked_candidates(
    config: dict,
    library_root: Path,
    unpacked_root: Path,
    errors: list[str],
) -> list[Candidate]:
    candidates: list[Candidate] = []
    allowed = [extension.casefold() for extension in config["index"]["unpacked_extensions"]]
    package_versions = {
        source["name"]: source.get("package_version") or ""
        for source in config.get("sources", [])
    }

    receipts = sorted(unpacked_root.rglob(".pipeline-unpack-receipt.json"), key=lambda path: path.as_posix().casefold())
    for receipt_path in receipts:
        try:
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            if receipt.get("status") != "PASS":
                errors.append(f"Unpack receipt is not PASS: {receipt_path}")
                continue
            source_name = receipt["source"]
            catalog_path = receipt["catalogRelativePath"]
            character = source_character_for(source_name, catalog_path)
            for output in receipt.get("outputs", []):
                relative_output = output["relativePath"]
                path = receipt_path.parent / Path(relative_output)
                extension = compound_extension(path, allowed)
                if extension not in allowed:
                    continue
                if not path.is_file():
                    errors.append(f"Receipt output missing: {path}")
                    continue
                stat = path.stat()
                if stat.st_size != int(output["sizeBytes"]):
                    errors.append(f"Receipt output size mismatch: {path}")
                    continue
                actual_hash = sha256_for(path)
                if actual_hash != output["sha256"]:
                    errors.append(f"Receipt output SHA-256 mismatch: {path}")
                    continue

                library_relative = path.relative_to(library_root).as_posix()
                candidates.append(
                    Candidate(
                        record_kind_for(extension, path, character),
                        "UNPACKED",
                        source_name,
                        package_versions.get(source_name, ""),
                        character,
                        library_relative,
                        actual_hash,
                        stat.st_size,
                        receipt["jobID"],
                        stem_for(path, extension),
                        path.name,
                    )
                )
        except Exception as exception:
            errors.append(f"Failed to read unpack receipt {receipt_path}: {type(exception).__name__}: {exception}")
    return candidates


def candidate_fingerprint(candidates: list[Candidate]) -> str:
    digest = hashlib.sha256()
    for candidate in sorted(candidates, key=lambda item: (item.source_stage, item.source, item.original_path.casefold())):
        digest.update(candidate.source_stage.encode("utf-8"))
        digest.update(b"\0")
        digest.update(candidate.source.encode("utf-8"))
        digest.update(b"\0")
        digest.update(candidate.original_path.encode("utf-8"))
        digest.update(b"\0")
        digest.update(candidate.source_hash.encode("ascii"))
        digest.update(b"\n")
    return digest.hexdigest()


def load_existing_records(path: Path) -> dict[str, dict[str, str]]:
    if not path.is_file():
        return {}
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        return {row["LibraryID"]: row for row in csv.DictReader(source)}


def build_record(
    candidate: Candidate,
    prefix: str,
    timestamp: str,
    existing: dict[str, str] | None,
) -> dict[str, str]:
    library_id = library_id_for(prefix, candidate.source_stage, candidate.source, candidate.original_path)
    record = {
        "LibraryID": library_id,
        "RecordKind": candidate.record_kind,
        "SourceStage": candidate.source_stage,
        "Source": candidate.source,
        "SourcePackageVersion": candidate.source_package_version,
        "SourceGame": "Unknown",
        "SourceCharacter": candidate.source_character,
        "CharacterType": "Unknown",
        "OriginalPath": candidate.original_path,
        "SourceHash": candidate.source_hash,
        "SizeBytes": str(candidate.size_bytes),
        "ProvenanceID": candidate.provenance_id,
        "AnimationID": candidate.animation_id,
        "OriginalFileName": candidate.original_file_name,
        "FriendlyName": "Unknown",
        "SemanticConfidence": "Unknown",
        "WeaponFamily": "Unknown",
        "ActionType": "Unknown",
        "ActionVariant": "Unknown",
        "Direction": "Unknown",
        "Loop": "Unknown",
        "DurationSeconds": "",
        "FrameRate": "",
        "RootMotionStatus": "Unknown",
        "RootTranslation": "Unknown",
        "RootYaw": "Unknown",
        "SkeletonProfile": "Unknown",
        "RetargetProfile": "Unknown",
        "ConversionStatus": "NotStarted",
        "RetargetStatus": "NotStarted",
        "UnityImportStatus": "NotStarted",
        "QAStatus": "Unreviewed",
        "PairGroupID": "",
        "TAEReference": candidate.original_path if candidate.record_kind == "Timeline" else "",
        "PreviewPath": "",
        "Notes": "",
        "CreatedAt": timestamp,
        "UpdatedAt": timestamp,
    }
    if existing is not None:
        for field in CURATED_FIELDS:
            if field in existing:
                record[field] = existing[field]
        identity_changed = any(record[field] != existing.get(field, "") for field in IDENTITY_FIELDS)
        record["UpdatedAt"] = timestamp if identity_changed else existing.get("UpdatedAt", timestamp)
    return record


def write_csv_atomic(path: Path, records: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=ANIMATION_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(records)
    temporary.replace(path)


def load_state(path: Path) -> dict:
    if not path.is_file():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def write_state(
    path: Path,
    timestamp: str,
    phase_config_sha256: str,
    input_fingerprint: str,
    record_count: int,
    output_sha256: str,
    pipeline_version: str,
    phase_version: str,
) -> None:
    payload = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": pipeline_version,
        "phaseVersion": phase_version,
        "phaseConfigSHA256": phase_config_sha256,
        "inputFingerprint": input_fingerprint,
        "recordCount": record_count,
        "outputSHA256": output_sha256,
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_report(
    path: Path,
    timestamp: str,
    status: str,
    candidates: list[Candidate],
    errors: list[str],
    skipped: bool,
    log_relative_path: str,
) -> None:
    by_stage = Counter(candidate.source_stage for candidate in candidates)
    by_source = Counter(candidate.source for candidate in candidates)
    by_kind = Counter(candidate.record_kind for candidate in candidates)
    hash_counts = Counter(candidate.source_hash for candidate in candidates)
    duplicate_hash_groups = sum(count > 1 for count in hash_counts.values())
    lines = [
        "# Phase 3 animation index",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        f"Result: **{status}**.",
        "",
        "## What was attempted",
        "",
        "Index Phase 1 RAW identities and verified Phase 2 unpack outputs into one canonical animation catalog.",
        "",
        "## What changed",
        "",
        "- Stable `LibraryID` values were assigned from source stage, source, and immutable original path.",
        "- RAW manifest records and hash-verified unpack receipts were merged without semantic guessing.",
        "- Existing curated fields are preserved when the index is rebuilt.",
        "",
        "## Counts",
        "",
        f"- Records: **{len(candidates):,}**",
        f"- Duplicate SourceHash groups: **{duplicate_hash_groups:,}**",
        f"- Existing CSV reused unchanged: **{'Yes' if skipped else 'No'}**",
        "",
        "### By stage",
        "",
    ]
    lines.extend(f"- {name}: **{count:,}**" for name, count in sorted(by_stage.items()))
    lines.extend(["", "### By source", ""])
    lines.extend(f"- {name}: **{count:,}**" for name, count in sorted(by_source.items()))
    lines.extend(["", "### By record kind", ""])
    lines.extend(f"- {name}: **{count:,}**" for name, count in sorted(by_kind.items()))
    lines.extend(
        [
            "",
            "## Files created or updated",
            "",
            "- `Database/animations.csv`",
            "- `Database/animation_index_state.json`",
            "- `Database/pipeline_runs.csv`",
            "- `03_CATALOG/reports/phase_3_status.md`",
            f"- `{log_relative_path}`",
            "",
            "## Known blockers",
            "",
            "- Semantic fields remain `Unknown` until a reliable source map or DSAnimStudio preview exists.",
            "- HKX duration, frame rate, pose integrity, and root motion were not parsed in Phase 3.",
            "- TAE records are preserved, but clip-to-TAE event relationships are not inferred yet.",
            "- `All Characters` combines multiple games, so `SourceGame` is intentionally not guessed.",
            "",
            "## Next safe action",
            "",
            "Phase 4: select a 5–10 clip pilot, preview it in DSAnimStudio, then add verified semantics and TAE links.",
            "",
            "## Failures",
            "",
        ]
    )
    if errors:
        lines.extend(f"- {error}" for error in errors)
    else:
        lines.append("None.")
    lines.append("")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    library_root = resolve_library_root(config_path, config)
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    unpacked_root = resolve_from_library(library_root, config["paths"]["unpacked"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    for directory in (database_root, logs_root, catalog_root / "reports"):
        directory.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"index_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.index")
    log_relative_path = log_path.relative_to(library_root).as_posix()

    phase_config_sha256 = index_phase_config_hash(config)
    raw_manifest_path = catalog_root / "manifests" / "raw_files.json"
    _, manifest_records = load_raw_manifest(raw_manifest_path)
    errors: list[str] = []
    candidates = collect_raw_candidates(config, library_root, manifest_records, errors)
    candidates.extend(collect_unpacked_candidates(config, library_root, unpacked_root, errors))
    candidates.sort(key=lambda item: (item.source, item.source_character, item.record_kind, item.animation_id, item.original_path))
    fingerprint = candidate_fingerprint(candidates)
    logger.info("Phase 3 discovered %d records with %d validation failures", len(candidates), len(errors))

    animations_path = database_root / "animations.csv"
    state_path = database_root / "animation_index_state.json"
    report_path = catalog_root / "reports" / "phase_3_status.md"
    state = load_state(state_path)
    cache_valid = (
        not args.force
        and not errors
        and animations_path.is_file()
        and state.get("status") == "PASS"
        and state.get("phaseConfigSHA256") == phase_config_sha256
        and state.get("inputFingerprint") == fingerprint
        and state.get("recordCount") == len(candidates)
        and state.get("outputSHA256") == sha256_for(animations_path)
    )

    if errors:
        logger.error("Phase 3 failed validation; existing animation catalog was not replaced")
        status = "FAIL"
    elif cache_valid:
        logger.info("Phase 3 inputs unchanged; existing PASS animation catalog kept")
        status = "SKIPPED_UNCHANGED"
    else:
        existing = load_existing_records(animations_path)
        prefix = config["index"]["library_id_prefix"]
        records = [
            build_record(
                candidate,
                prefix,
                timestamp,
                existing.get(library_id_for(prefix, candidate.source_stage, candidate.source, candidate.original_path)),
            )
            for candidate in candidates
        ]
        library_ids = [record["LibraryID"] for record in records]
        if len(set(library_ids)) != len(library_ids):
            raise RuntimeError("Deterministic LibraryID collision detected")
        write_csv_atomic(animations_path, records)
        output_sha256 = sha256_for(animations_path)
        write_state(
            state_path,
            timestamp,
            phase_config_sha256,
            fingerprint,
            len(records),
            output_sha256,
            config["pipeline_version"],
            config["phase_versions"]["index"],
        )
        logger.info("Phase 3 wrote %d canonical records", len(records))
        status = "PASS"

    write_report(
        report_path,
        timestamp,
        status,
        candidates,
        errors,
        status == "SKIPPED_UNCHANGED",
        log_relative_path,
    )
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase3AnimationIndex",
            "Tool": "PythonIndexer",
            "ToolVersion": config["phase_versions"]["index"],
            "InputCount": len(candidates) + len(errors),
            "SuccessCount": len(candidates) if not errors else 0,
            "FailureCount": len(errors),
            "ConfigHash": phase_config_sha256,
            "PipelineVersion": config["pipeline_version"],
            "LogPath": log_relative_path,
            "Notes": f"status={status}; records={len(candidates)}",
        },
    )
    logger.info("Phase 3 complete: status=%s records=%d failures=%d", status, len(candidates), len(errors))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
