#!/usr/bin/env python3
"""Run the bounded Phase 5 HKX-to-Blender hard gate for the c0000 pilot."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import math
import os
import subprocess
import sys
import tempfile
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
    "BlendPath",
    "BlendSHA256",
    "BlendSizeBytes",
    "BlenderVersion",
    "FrameCount",
    "FrameRate",
    "DurationSeconds",
    "RestSkeletonHeight",
    "MinimumPoseHeight",
    "MaximumPoseExtentFromPelvis",
    "RootTranslation",
    "RootTranslationMagnitude",
    "RootYawDeltaDegrees",
    "MotionFingerprintSHA256",
    "PoseIntegrity",
    "DurationValidation",
    "FrameTimingValidation",
    "CoordinateValidation",
    "ScaleValidation",
    "RootTranslationValidation",
    "RootYawValidation",
    "ReopenValidation",
    "RepeatabilityValidation",
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
    configured = Path(config.get("project_root", "."))
    return (config_path.parent.parent / configured).resolve()


def resolve_from_library(library_root: Path, configured_path: str) -> Path:
    path = Path(configured_path)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def relative_to_library(path: Path, library_root: Path) -> str:
    return path.resolve().relative_to(library_root).as_posix()


def ensure_below(path: Path, parent: Path) -> None:
    path.resolve().relative_to(parent.resolve())


def write_csv_atomic(path: Path, fieldnames: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(path)


def load_csv(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    if not path.is_file():
        return [], []
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        return list(reader.fieldnames or []), list(reader)


def run_command(
    command: list[str],
    cwd: Path,
    logger: logging.Logger,
    environment: dict[str, str] | None = None,
    timeout_seconds: int = 300,
) -> subprocess.CompletedProcess[str]:
    logger.info("Running: %s", subprocess.list2cmdline(command))
    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=environment,
        timeout=timeout_seconds,
        check=False,
    )
    if result.stdout:
        logger.info("stdout:\n%s", result.stdout.rstrip().encode("ascii", "backslashreplace").decode("ascii"))
    if result.stderr:
        logger.info("stderr:\n%s", result.stderr.rstrip().encode("ascii", "backslashreplace").decode("ascii"))
    return result


def blender_environment(blender_user_scripts: Path) -> dict[str, str]:
    environment = os.environ.copy()
    environment["BLENDER_USER_SCRIPTS"] = str(blender_user_scripts)
    return environment


def convert_animation(
    blender: Path,
    converter: Path,
    flver: Path,
    base_anibnd: Path,
    animation_anibnd: Path,
    blender_user_scripts: Path,
    animation_id: str,
    blend_path: Path,
    metadata_path: Path,
    logger: logging.Logger,
) -> dict:
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(converter),
        "--",
        "--flver",
        str(flver),
        "--base-anibnd",
        str(base_anibnd),
        "--animation-anibnd",
        str(animation_anibnd),
        "--animation",
        animation_id,
        "--output-blend",
        str(blend_path),
        "--metadata",
        str(metadata_path),
    ]
    result = run_command(command, blend_path.parent, logger, blender_environment(blender_user_scripts))
    if not blend_path.is_file() or not metadata_path.is_file():
        raise RuntimeError(f"Blender did not create complete conversion outputs for {animation_id}")
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if result.returncode != 0:
        known_shutdown_codes = {11, -1073741819, 3221225477}
        if result.returncode not in known_shutdown_codes:
            raise RuntimeError(f"Blender conversion failed with exit code {result.returncode}: {animation_id}")
        logger.warning(
            "Blender returned %d after writing complete outputs for %s; accepting the known Intel driver shutdown fault",
            result.returncode,
            animation_id,
        )
    return metadata


def validate_blend(
    blender: Path,
    validator: Path,
    blend_path: Path,
    metadata_path: Path,
    output_path: Path,
    logger: logging.Logger,
) -> dict:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        str(blend_path),
        "--python",
        str(validator),
        "--",
        "--expected-metadata",
        str(metadata_path),
        "--output",
        str(output_path),
    ]
    result = run_command(command, blend_path.parent, logger)
    if result.returncode != 0:
        raise RuntimeError(f"Blender reopen validation failed with exit code {result.returncode}: {blend_path}")
    if not output_path.is_file():
        raise RuntimeError(f"Blender reopen validation did not create {output_path}")
    validation = json.loads(output_path.read_text(encoding="utf-8"))
    if validation.get("status") != "PASS":
        raise RuntimeError(f"Blender reopen validation failed: {validation.get('failures', [])}")
    return validation


def vector_distance(left: list[float], right: list[float]) -> float:
    return math.sqrt(sum((left[index] - right[index]) ** 2 for index in range(3)))


def format_vector(values: list[float]) -> str:
    return ",".join(f"{value:.7f}" for value in values)


def validation_status(passed: bool) -> str:
    return "PASS" if passed else "FAIL"


def find_row(rows: list[dict[str, str]], animation_id: str, source_hint: str | None = None) -> dict[str, str]:
    matches = [row for row in rows if row.get("AnimationID") == animation_id]
    if source_hint is not None:
        matches = [row for row in matches if source_hint in row.get("OriginalPath", "")]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one row for {animation_id}, found {len(matches)}")
    return matches[0]


def build_report(
    path: Path,
    timestamp: str,
    operations: list[dict[str, str]],
    repeatability_animation: str,
    rest_height_spread: float,
    artifact_root: str,
    log_path: str,
) -> None:
    all_passed = all(row["Status"] == "PASS" for row in operations)
    lines = [
        "# Phase 5 HKX to Blender pilot",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        f"Mechanical gate result: **{'PASS' if all_passed else 'FAIL'}**.",
        "",
        "Bridge approval: **limited to the tested Elden Ring `c0000/a00_hi` standard animation variant**. Full-library conversion remains prohibited.",
        "",
        "## Pilot",
        "",
        "| Animation | Frames | Pose | Duration | Root translation | Reopen | Repeatability |",
        "|---|---:|---|---|---|---|---|",
    ]
    for row in operations:
        lines.append(
            f"| `{row['AnimationID']}` | {row['FrameCount']} | {row['PoseIntegrity']} | "
            f"{row['DurationValidation']} | {row['RootTranslationValidation']} | "
            f"{row['ReopenValidation']} | {row['RepeatabilityValidation']} |"
        )
    lines.extend(
        [
            "",
            "## Gate evidence",
            "",
            "- Pose integrity: finite transforms, required body bones, minimum vertical span, and bounded distance from pelvis.",
            "- Duration and frame timing: frame count/FPS match the Phase 4 evidence within one source frame.",
            "- Coordinate system: artifacts reopen as `BlenderZUp_SourceNative`; no hidden Unity-axis transform is applied in Phase 5.",
            f"- Scale: the rest-skeleton height spread across all clips is `{rest_height_spread:.9f}` Blender units.",
            "- Root translation: end-to-start root displacement matches the Phase 4 sampled evidence.",
            "- Root yaw: finite normalized yaw delta is preserved and recorded per clip.",
            "- Serialization: every `.blend` was reopened in a fresh Blender process and produced the same motion fingerprint.",
            f"- Repeatability: `{repeatability_animation}` was imported twice and produced an identical motion fingerprint.",
            "",
            "## Generated artifacts",
            "",
            f"Native `.blend`, conversion JSON, and reopen-validation JSON files are under ignored `{artifact_root}`.",
            "Their paths, sizes, SHA-256 hashes, fingerprints, and gate results are versioned in `Database/conversion_operations.csv` and `Database/phase_5_state.json`.",
            "",
            "## Coverage limitations",
            "",
            "- The pilot covers locomotion-like motion, dodge/root motion, two attacks, and one long unclassified motion.",
            "- It does not yet cover a confirmed idle, turn-in-place, cast, boss rig, creature rig, or paired animation.",
            "- Semantic confidence is independent from mechanical conversion success. Probable/Unknown clips are not approved for gameplay merely because their HKX imported correctly.",
            "- Phase 4 already found two HKX entries that the active Havok registry cannot parse. This gate does not generalize to those variants.",
            "- On this host Blender may fault in Intel `igxelpicd64.dll` during shutdown after writing complete outputs. A non-zero exit is accepted only after both `.blend` and metadata exist; the `.blend` must then pass a clean reopen validation.",
            "",
            "## Next safe action",
            "",
            "Create the explicit `ER_Player` skeleton profile from the measured source rig, then retarget only this pilot subset to the Project S target skeleton. Do not export the full library or mark the bridge globally approved.",
            "",
            f"Execution log: `{log_path}` (ignored by Git).",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    library_root = resolve_library_root(config_path, config)
    phase = config["conversion"]
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    converted_root = resolve_from_library(library_root, config["paths"]["converted"])
    artifact_root = resolve_from_library(library_root, phase["phase5_output"])
    ensure_below(artifact_root, converted_root)
    for directory in (database_root, catalog_root / "reports", logs_root, artifact_root):
        directory.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"conversion_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.conversion")
    log_relative_path = relative_to_library(log_path, library_root)

    blender = resolve_from_library(library_root, config["tools"]["blender"])
    blender_bridge = resolve_from_library(library_root, config["tools"]["hkx_blender_bridge"])
    blender_user_scripts = blender_bridge.parent.parent
    semantic = config["semantic_preview"]
    flver = resolve_from_library(library_root, semantic["flver"])
    base_anibnd = resolve_from_library(library_root, semantic["base_anibnd"])
    animation_anibnd = resolve_from_library(library_root, semantic["animation_anibnd"])
    converter = Path(__file__).with_name("convert_hkx_blender.py").resolve()
    validator = Path(__file__).with_name("validate_blend_animation.py").resolve()
    required = (blender, blender_bridge, flver, base_anibnd, animation_anibnd, converter, validator)
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise FileNotFoundError("Missing Phase 5 prerequisites: " + "; ".join(missing))

    animations_path = database_root / "animations.csv"
    animation_fields, animation_rows = load_csv(animations_path)
    _, pilot_rows = load_csv(database_root / "pilot_clips.csv")
    operations: list[dict[str, str]] = []
    artifact_state: dict[str, dict[str, str | int]] = {}
    metadata_by_animation: dict[str, dict] = {}
    operation_by_animation: dict[str, dict[str, str]] = {}

    for animation_id in phase["phase5_pilots"]:
        logger.info("Converting Phase 5 pilot %s", animation_id)
        canonical = find_row(animation_rows, animation_id, "c0000-a00-hi")
        pilot = find_row(pilot_rows, animation_id)
        output_directory = artifact_root / animation_id
        ensure_below(output_directory, converted_root)
        blend_path = output_directory / f"{animation_id}.blend"
        metadata_path = output_directory / f"{animation_id}.conversion.json"
        reopen_path = output_directory / f"{animation_id}.reopen.json"
        metadata = convert_animation(
            blender,
            converter,
            flver,
            base_anibnd,
            animation_anibnd,
            blender_user_scripts,
            animation_id,
            blend_path,
            metadata_path,
            logger,
        )
        reopen = validate_blend(blender, validator, blend_path, metadata_path, reopen_path, logger)

        phase4_translation = [
            float(pilot["RootTranslationX"]),
            float(pilot["RootTranslationY"]),
            float(pilot["RootTranslationZ"]),
        ]
        duration_delta = abs(float(pilot["DurationSeconds"]) - float(metadata["durationSeconds"]))
        pose_pass = (
            metadata["invalidTransformCount"] == 0
            and not metadata["missingBodyBones"]
            and metadata["minimumPoseHeight"] >= float(phase["minimum_pose_height"])
            and metadata["maximumPoseExtentFromPelvis"] <= float(phase["maximum_pose_extent"])
        )
        duration_pass = duration_delta <= 1.0 / float(metadata["frameRate"]) + 1e-6
        timing_pass = (
            metadata["frameCount"] == int(pilot["FrameCount"])
            and metadata["frameRate"] == int(pilot["FrameRate"])
        )
        coordinate_pass = metadata["coordinateSpace"] == "BlenderZUp_SourceNative"
        translation_pass = vector_distance(metadata["rootTranslation"], phase4_translation) <= float(
            phase["root_translation_tolerance"]
        )
        yaw_pass = math.isfinite(metadata["rootYawDeltaDegrees"]) and abs(metadata["rootYawDeltaDegrees"]) <= 180.0
        reopen_pass = reopen["status"] == "PASS" and reopen["motionFingerprintSHA256"] == metadata[
            "motionFingerprintSHA256"
        ]
        checks = (pose_pass, duration_pass, timing_pass, coordinate_pass, translation_pass, yaw_pass, reopen_pass)
        operation = {
            "RunID": run_id,
            "Timestamp": timestamp,
            "LibraryID": canonical["LibraryID"],
            "AnimationID": animation_id,
            "SourceHash": canonical["SourceHash"],
            "BlendPath": relative_to_library(blend_path, library_root),
            "BlendSHA256": sha256_for(blend_path),
            "BlendSizeBytes": str(blend_path.stat().st_size),
            "BlenderVersion": metadata["blenderVersion"],
            "FrameCount": str(metadata["frameCount"]),
            "FrameRate": str(metadata["frameRate"]),
            "DurationSeconds": f"{metadata['durationSeconds']:.9f}",
            "RestSkeletonHeight": f"{metadata['restSkeletonHeight']:.9f}",
            "MinimumPoseHeight": f"{metadata['minimumPoseHeight']:.9f}",
            "MaximumPoseExtentFromPelvis": f"{metadata['maximumPoseExtentFromPelvis']:.9f}",
            "RootTranslation": format_vector(metadata["rootTranslation"]),
            "RootTranslationMagnitude": f"{metadata['rootTranslationMagnitude']:.9f}",
            "RootYawDeltaDegrees": f"{metadata['rootYawDeltaDegrees']:.9f}",
            "MotionFingerprintSHA256": metadata["motionFingerprintSHA256"],
            "PoseIntegrity": validation_status(pose_pass),
            "DurationValidation": validation_status(duration_pass),
            "FrameTimingValidation": validation_status(timing_pass),
            "CoordinateValidation": validation_status(coordinate_pass),
            "ScaleValidation": "PENDING_BATCH",
            "RootTranslationValidation": validation_status(translation_pass),
            "RootYawValidation": validation_status(yaw_pass),
            "ReopenValidation": validation_status(reopen_pass),
            "RepeatabilityValidation": "NOT_SAMPLED",
            "Status": validation_status(all(checks)),
            "Notes": f"SemanticConfidence={pilot['SemanticConfidence']}; Action={pilot['ActionType']}/{pilot['ActionVariant']}",
        }
        operations.append(operation)
        operation_by_animation[animation_id] = operation
        metadata_by_animation[animation_id] = metadata
        artifact_state[relative_to_library(blend_path, library_root)] = {
            "sha256": operation["BlendSHA256"],
            "sizeBytes": blend_path.stat().st_size,
            "motionFingerprintSHA256": metadata["motionFingerprintSHA256"],
            "metadataSHA256": sha256_for(metadata_path),
            "reopenValidationSHA256": sha256_for(reopen_path),
        }

    rest_heights = [metadata["restSkeletonHeight"] for metadata in metadata_by_animation.values()]
    rest_height_spread = max(rest_heights) - min(rest_heights)
    scale_pass = rest_height_spread <= float(phase["rest_height_tolerance"])
    for operation in operations:
        operation["ScaleValidation"] = validation_status(scale_pass)
        if not scale_pass:
            operation["Status"] = "FAIL"

    repeat_animation = phase["repeatability_animation"]
    if repeat_animation not in metadata_by_animation:
        raise RuntimeError(f"Repeatability animation is not in the Phase 5 pilot: {repeat_animation}")
    with tempfile.TemporaryDirectory(prefix="project-s-phase5-repeat-") as temporary:
        repeat_root = Path(temporary)
        repeat_metadata = convert_animation(
            blender,
            converter,
            flver,
            base_anibnd,
            animation_anibnd,
            blender_user_scripts,
            repeat_animation,
            repeat_root / f"{repeat_animation}.blend",
            repeat_root / f"{repeat_animation}.json",
            logger,
        )
        repeatability_pass = (
            repeat_metadata["motionFingerprintSHA256"]
            == metadata_by_animation[repeat_animation]["motionFingerprintSHA256"]
        )
    repeat_operation = operation_by_animation[repeat_animation]
    repeat_operation["RepeatabilityValidation"] = validation_status(repeatability_pass)
    if not repeatability_pass:
        repeat_operation["Status"] = "FAIL"

    if any(operation["Status"] != "PASS" for operation in operations):
        raise RuntimeError(
            "Phase 5 hard gate failed: "
            + ", ".join(operation["AnimationID"] for operation in operations if operation["Status"] != "PASS")
        )

    for animation_id in phase["phase5_pilots"]:
        canonical = find_row(animation_rows, animation_id, "c0000-a00-hi")
        canonical["ConversionStatus"] = "PilotPassed"
        canonical["UpdatedAt"] = timestamp

    operation_path = database_root / "conversion_operations.csv"
    _, existing_operations = load_csv(operation_path)
    write_csv_atomic(operation_path, OPERATION_FIELDS, existing_operations + operations)
    write_csv_atomic(animations_path, tuple(animation_fields), animation_rows)

    report_path = catalog_root / "reports" / "phase_5_status.md"
    build_report(
        report_path,
        timestamp,
        operations,
        repeat_animation,
        rest_height_spread,
        relative_to_library(artifact_root, library_root),
        log_relative_path,
    )
    tracked_outputs = (operation_path, animations_path, report_path)
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["conversion"],
        "phaseConfigSHA256": hash_json(phase),
        "bridgeApproval": "PilotOnly",
        "approvalScope": "EldenRing/c0000/a00_hi/recognized-standard-animation-HKX",
        "pilotCount": len(operations),
        "restSkeletonHeightSpread": rest_height_spread,
        "repeatabilityAnimation": repeat_animation,
        "outputs": {relative_to_library(path, library_root): sha256_for(path) for path in tracked_outputs},
        "artifacts": artifact_state,
    }
    state_path = database_root / "phase_5_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase5HKXBlenderPilot",
            "Tool": "Blender+Soulstruct",
            "ToolVersion": config["phase_versions"]["conversion"],
            "InputCount": len(operations),
            "SuccessCount": len(operations),
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": log_relative_path,
            "Notes": f"status=PASS; approval=PilotOnly; repeatability={repeat_animation}",
        },
    )
    logger.info("Phase 5 complete: pilots=%d approval=PilotOnly", len(operations))
    return 0


if __name__ == "__main__":
    sys.exit(main())
