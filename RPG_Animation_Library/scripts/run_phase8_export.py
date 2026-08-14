#!/usr/bin/env python3
"""Export the bounded Phase 8 pilot to contract-backed Unity FBX files."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import math
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from pipeline_ledger import append_pipeline_run, new_run_id


OPERATION_FIELDS = (
    "RunID",
    "Timestamp",
    "LibraryID",
    "AnimationID",
    "SourceHash",
    "RetargetedBlendPath",
    "RetargetedBlendSHA256",
    "FBXPath",
    "FBXSHA256",
    "FBXSizeBytes",
    "MetadataPath",
    "MetadataSHA256",
    "PipelineVersion",
    "SkeletonProfile",
    "RetargetProfile",
    "RootMotionStatus",
    "FrameCount",
    "FrameRate",
    "DurationSeconds",
    "ExportValidation",
    "ReimportValidation",
    "Status",
    "Notes",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    return parser.parse_args()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


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
    return (config_path.parent.parent / config.get("project_root", ".")).resolve()


def resolve_from_library(library_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def relative_to_library(path: Path, library_root: Path) -> str:
    return path.resolve().relative_to(library_root).as_posix()


def ensure_below(path: Path, parent: Path) -> None:
    path.resolve().relative_to(parent.resolve())


def load_csv(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    if not path.is_file():
        return [], []
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        return list(reader.fieldnames or []), list(reader)


def write_csv_atomic(path: Path, fieldnames: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(path)


def find_row(rows: list[dict[str, str]], animation_id: str) -> dict[str, str]:
    matches = [
        row for row in rows
        if row.get("AnimationID") == animation_id and "c0000-a00-hi" in row.get("OriginalPath", "")
    ]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one canonical row for {animation_id}, found {len(matches)}")
    return matches[0]


def safe_name(value: str, fallback: str) -> str:
    normalized = re.sub(r"[^A-Za-z0-9]+", "_", value or "").strip("_")
    return normalized or fallback


def run_command(command: list[str], cwd: Path, logger: logging.Logger, timeout: int = 300) -> subprocess.CompletedProcess[str]:
    logger.info("Run: %s", subprocess.list2cmdline(command))
    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        check=False,
    )
    if result.stdout:
        logger.info("stdout:\n%s", result.stdout.rstrip().encode("ascii", "backslashreplace").decode("ascii"))
    if result.stderr:
        logger.info("stderr:\n%s", result.stderr.rstrip().encode("ascii", "backslashreplace").decode("ascii"))
    return result


def build_report(path: Path, timestamp: str, operations: list[dict[str, str]], artifact_root: str) -> None:
    lines = [
        "# Phase 8 Unity FBX export pilot",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        "Mechanical gate result: **PASS**.",
        "",
        "| Animation | FBX bytes | Frames | Root motion | Export | Blender reimport |",
        "|---|---:|---:|---|---|---|",
    ]
    for row in operations:
        lines.append(
            f"| `{row['AnimationID']}` | {row['FBXSizeBytes']} | {row['FrameCount']} | "
            f"{row['RootMotionStatus']} | {row['ExportValidation']} | {row['ReimportValidation']} |"
        )
    lines.extend(
        [
            "",
            f"Animation-only FBX files and adjacent `.animlib.json` contracts are under ignored `{artifact_root}`.",
            "Every contract carries LibraryID, source hash, pinned pipeline version, skeleton/retarget profiles, root-motion policy and export timestamp.",
            "The export contains only the target armature/action; source rigs, meshes and materials are excluded.",
            "Blender reimport validates armature count, mapped bones, frame timing and root trajectory before Unity receives the files.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    phase = config["fbx_export"]
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    fbx_root = resolve_from_library(library_root, config["paths"]["fbx"])
    input_root = resolve_from_library(library_root, phase["phase8_input"])
    artifact_root = resolve_from_library(library_root, phase["phase8_output"])
    ensure_below(artifact_root, fbx_root)
    for directory in (database_root, catalog_root / "reports", logs_root, artifact_root):
        directory.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"fbx_export_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.fbx-export")

    blender = resolve_from_library(library_root, config["tools"]["blender"])
    exporter = Path(__file__).with_name("export_fbx_blender.py").resolve()
    validator = Path(__file__).with_name("validate_fbx_blender.py").resolve()
    import_profile_path = resolve_from_library(library_root, phase["unity_import_profile"])
    required = (blender, exporter, validator, import_profile_path)
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise FileNotFoundError("Missing Phase 8 prerequisites: " + "; ".join(missing))
    import_profile = json.loads(import_profile_path.read_text(encoding="utf-8"))

    animation_fields, animation_rows = load_csv(database_root / "animations.csv")
    operations = []
    artifacts = {}
    for animation_id in phase["phase8_pilots"]:
        canonical = find_row(animation_rows, animation_id)
        source_directory = input_root / animation_id
        source_blend = source_directory / f"{animation_id}.blend"
        retarget_metadata_path = source_directory / f"{animation_id}.retarget.json"
        retarget_metadata = json.loads(retarget_metadata_path.read_text(encoding="utf-8"))
        action_folder = safe_name(canonical["ActionType"], "Unclassified")
        variant = safe_name(canonical["ActionVariant"], safe_name(canonical["FriendlyName"], animation_id))
        file_stem = f"Player_{action_folder}_{variant}__{canonical['LibraryID']}"
        output_directory = artifact_root / action_folder
        fbx_path = output_directory / f"{file_stem}.fbx"
        receipt_path = output_directory / f"{file_stem}.export.json"
        validation_path = output_directory / f"{file_stem}.validation.json"
        contract_path = output_directory / f"{file_stem}.animlib.json"
        output_directory.mkdir(parents=True, exist_ok=True)

        export_command = [
            str(blender), "--background", "--factory-startup", str(source_blend), "--python", str(exporter), "--",
            "--retarget-metadata", str(retarget_metadata_path),
            "--output-fbx", str(fbx_path),
            "--output-receipt", str(receipt_path),
        ]
        export_result = run_command(export_command, output_directory, logger)
        if export_result.returncode != 0 or not (fbx_path.is_file() and receipt_path.is_file()):
            raise RuntimeError(f"FBX export failed for {animation_id} (exit={export_result.returncode})")
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))

        validate_command = [
            str(blender), "--background", "--factory-startup", "--python", str(validator), "--",
            "--fbx", str(fbx_path),
            "--retarget-metadata", str(retarget_metadata_path),
            "--output", str(validation_path),
        ]
        validate_result = run_command(validate_command, output_directory, logger)
        if validate_result.returncode != 0 or not validation_path.is_file():
            raise RuntimeError(f"FBX reimport validation failed for {animation_id} (exit={validate_result.returncode})")
        validation = json.loads(validation_path.read_text(encoding="utf-8"))
        if validation["status"] != "PASS":
            raise RuntimeError(f"FBX reimport validation failed for {animation_id}: {validation['failures']}")

        root_magnitude = math.sqrt(sum(value * value for value in retarget_metadata["targetRootTranslation"]))
        contract = {
            "SchemaVersion": 1,
            "LibraryID": canonical["LibraryID"],
            "AnimationID": animation_id,
            "SourceHash": canonical["SourceHash"],
            "SourceBlendSHA256": sha256_for(source_blend),
            "FBXSHA256": sha256_for(fbx_path),
            "PipelineVersion": config["pipeline_version"],
            "SkeletonProfile": retarget_metadata["targetProfile"],
            "SourceSkeletonProfile": retarget_metadata["sourceProfile"],
            "RetargetProfile": retarget_metadata["retargetProfile"],
            "RootMotionStatus": "RootMotionPreservedScaled" if root_magnitude > 0.001 else "InPlace",
            "RootTranslation": retarget_metadata["targetRootTranslation"],
            "ExportTimestamp": timestamp,
            "FriendlyName": canonical["FriendlyName"],
            "SemanticConfidence": canonical["SemanticConfidence"],
            "CharacterType": canonical["CharacterType"],
            "WeaponFamily": canonical["WeaponFamily"],
            "ActionType": canonical["ActionType"],
            "ActionVariant": canonical["ActionVariant"],
            "Direction": canonical["Direction"],
            "Loop": canonical["Loop"].lower() in {"yes", "true", "1"},
            "FrameCount": retarget_metadata["frameCount"],
            "FrameRate": retarget_metadata["frameRate"],
            "DurationSeconds": retarget_metadata["durationSeconds"],
            "UnityImportProfile": import_profile["id"],
            "TargetAvatarAsset": phase["target_avatar_asset"],
            "Labels": [
                "AnimationLibrary",
                f"LibraryID:{canonical['LibraryID']}",
                f"Action:{safe_name(canonical['ActionType'], 'Unknown')}",
                f"Source:{canonical['SourceGame']}",
            ],
        }
        contract_path.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

        operation = {
            "RunID": run_id,
            "Timestamp": timestamp,
            "LibraryID": canonical["LibraryID"],
            "AnimationID": animation_id,
            "SourceHash": canonical["SourceHash"],
            "RetargetedBlendPath": relative_to_library(source_blend, library_root),
            "RetargetedBlendSHA256": contract["SourceBlendSHA256"],
            "FBXPath": relative_to_library(fbx_path, library_root),
            "FBXSHA256": contract["FBXSHA256"],
            "FBXSizeBytes": str(fbx_path.stat().st_size),
            "MetadataPath": relative_to_library(contract_path, library_root),
            "MetadataSHA256": sha256_for(contract_path),
            "PipelineVersion": config["pipeline_version"],
            "SkeletonProfile": contract["SkeletonProfile"],
            "RetargetProfile": contract["RetargetProfile"],
            "RootMotionStatus": contract["RootMotionStatus"],
            "FrameCount": str(contract["FrameCount"]),
            "FrameRate": str(contract["FrameRate"]),
            "DurationSeconds": f"{contract['DurationSeconds']:.9f}",
            "ExportValidation": receipt["status"],
            "ReimportValidation": validation["status"],
            "Status": "PASS",
            "Notes": f"UnityImportProfile={contract['UnityImportProfile']}; avatar={contract['TargetAvatarAsset']}",
        }
        operations.append(operation)
        artifacts[relative_to_library(fbx_path, library_root)] = {
            "sha256": contract["FBXSHA256"],
            "sizeBytes": fbx_path.stat().st_size,
            "metadataPath": relative_to_library(contract_path, library_root),
            "metadataSHA256": operation["MetadataSHA256"],
            "exportReceiptSHA256": sha256_for(receipt_path),
            "reimportValidationSHA256": sha256_for(validation_path),
        }

    operations_path = database_root / "fbx_export_operations.csv"
    _, existing_operations = load_csv(operations_path)
    write_csv_atomic(operations_path, OPERATION_FIELDS, existing_operations + operations)
    report_path = catalog_root / "reports" / "phase_8_status.md"
    build_report(report_path, timestamp, operations, relative_to_library(artifact_root, library_root))
    tracked_outputs = (operations_path, report_path, import_profile_path)
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["fbx_export"],
        "phaseConfigSHA256": hash_json(phase),
        "approvalScope": "PilotOnly/ProjectS_CLazy_Player/UnityFBX",
        "pilotCount": len(operations),
        "outputs": {relative_to_library(path, library_root): sha256_for(path) for path in tracked_outputs},
        "artifacts": artifacts,
    }
    state_path = database_root / "phase_8_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase8UnityFBXExportPilot",
            "Tool": "BlenderFBX",
            "ToolVersion": config["phase_versions"]["fbx_export"],
            "InputCount": len(operations),
            "SuccessCount": len(operations),
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": relative_to_library(log_path, library_root),
            "Notes": "status=PASS; animationOnly=true; reimport=PASS",
        },
    )
    logger.info("Phase 8 complete: exports=%d", len(operations))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
