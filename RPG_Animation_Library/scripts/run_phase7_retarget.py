#!/usr/bin/env python3
"""Retarget and mechanically validate the bounded Phase 7 player pilot."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import math
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

from pipeline_ledger import append_pipeline_run, new_run_id


OPERATION_FIELDS = (
    "RunID",
    "Timestamp",
    "LibraryID",
    "AnimationID",
    "SourceBlendPath",
    "SourceBlendSHA256",
    "TargetBlendPath",
    "TargetBlendSHA256",
    "TargetBlendSizeBytes",
    "SourceProfile",
    "TargetProfile",
    "RetargetProfile",
    "Algorithm",
    "MappedBoneCount",
    "HeightScale",
    "FrameCount",
    "FrameRate",
    "DurationSeconds",
    "RootMotionStatus",
    "SourceRootTranslation",
    "TargetRootTranslation",
    "RootTranslationError",
    "MinimumPoseHeight",
    "MaximumPoseExtentFromPelvis",
    "MotionFingerprintSHA256",
    "PoseIntegrity",
    "TimingValidation",
    "RootMotionValidation",
    "ReopenValidation",
    "RepeatabilityValidation",
    "Status",
    "PreviewPath",
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


def format_vector(values: list[float]) -> str:
    return ",".join(f"{value:.7f}" for value in values)


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


def retarget(
    blender: Path,
    script: Path,
    source_blend: Path,
    target_fbx: Path,
    source_profile: Path,
    target_profile: Path,
    retarget_map: Path,
    animation_id: str,
    library_id: str,
    target_blend: Path,
    metadata_path: Path,
    logger: logging.Logger,
) -> dict:
    target_blend.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(blender), "--background", "--factory-startup", "--python", str(script), "--",
        "--source-blend", str(source_blend),
        "--target-fbx", str(target_fbx),
        "--source-profile", str(source_profile),
        "--target-profile", str(target_profile),
        "--retarget-map", str(retarget_map),
        "--animation-id", animation_id,
        "--library-id", library_id,
        "--output-blend", str(target_blend),
        "--output-metadata", str(metadata_path),
    ]
    result = run_command(command, target_blend.parent, logger)
    if (result.returncode != 0 and not (target_blend.is_file() and metadata_path.is_file())) or not metadata_path.is_file():
        raise RuntimeError(f"Retarget failed for {animation_id} (exit={result.returncode})")
    return json.loads(metadata_path.read_text(encoding="utf-8"))


def reopen_validate(
    blender: Path,
    validator: Path,
    blend_path: Path,
    metadata_path: Path,
    output_path: Path,
    logger: logging.Logger,
) -> dict:
    command = [
        str(blender), "--background", "--factory-startup", str(blend_path), "--python", str(validator), "--",
        "--expected-metadata", str(metadata_path), "--output", str(output_path),
    ]
    result = run_command(command, blend_path.parent, logger)
    if result.returncode != 0 or not output_path.is_file():
        raise RuntimeError(f"Retarget reopen validation failed for {blend_path} (exit={result.returncode})")
    validation = json.loads(output_path.read_text(encoding="utf-8"))
    if validation["status"] != "PASS":
        raise RuntimeError(f"Retarget reopen validation failed: {validation['failures']}")
    return validation


def render_contact_sheet(metadata: dict, output: Path) -> None:
    samples = metadata["poseSamples"]
    width = 260 * len(samples)
    height = 520
    margin = 32
    image = Image.new("RGB", (width, height), (10, 15, 28))
    draw = ImageDraw.Draw(image)
    palette = (91, 190, 255)
    joint = (255, 148, 91)
    parents = metadata["bodyParents"]

    all_x = [values[0] for sample in samples for values in sample["positions"].values()]
    all_z = [values[2] for sample in samples for values in sample["positions"].values()]
    x_extent = max(max(abs(value) for value in all_x), 0.5)
    z_min = min(all_z)
    z_extent = max(max(all_z) - z_min, 1.0)
    scale = min((260 - 2 * margin) / (2 * x_extent), (height - 2 * margin - 28) / z_extent)

    for index, sample in enumerate(samples):
        origin_x = index * 260 + 130
        positions = sample["positions"]

        def project(values: list[float]) -> tuple[float, float]:
            return origin_x + values[0] * scale, height - margin - (values[2] - z_min) * scale

        for child, parent in parents.items():
            if child in positions and parent in positions:
                draw.line((*project(positions[parent]), *project(positions[child])), fill=palette, width=3)
        for values in positions.values():
            x, y = project(values)
            draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=joint)
        draw.text((index * 260 + 12, 12), f"f{sample['frame']}", fill=(220, 230, 244))
    draw.text((12, height - 20), f"{metadata['animationId']} -> {metadata['targetProfile']}", fill=(220, 230, 244))
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output)


def build_report(path: Path, timestamp: str, operations: list[dict[str, str]], artifact_root: str) -> None:
    lines = [
        "# Phase 7 retarget pilot",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        "Mechanical gate result: **PASS**.",
        "",
        "| Animation | Frames | Mapped bones | Pose | Root motion | Reopen | Repeatability |",
        "|---|---:|---:|---|---|---|---|",
    ]
    for row in operations:
        lines.append(
            f"| `{row['AnimationID']}` | {row['FrameCount']} | {row['MappedBoneCount']} | "
            f"{row['PoseIntegrity']} | {row['RootMotionValidation']} | {row['ReopenValidation']} | "
            f"{row['RepeatabilityValidation']} |"
        )
    lines.extend(
        [
            "",
            "The same skeleton-family retarget profile was used for every pilot clip.",
            "Planar armature-object root motion is transferred once and scaled by measured skeleton height; vertical pose remains on pelvis/body animation.",
            f"Native target `.blend` files and validation metadata are under ignored `{artifact_root}`.",
            "Contact sheets under `03_CATALOG/previews/phase_7` are visual review evidence, not semantic approval.",
            "Unknown/Probable source semantics remain Unknown/Probable after a mechanical PASS.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    phase = config["retarget"]
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    retargeted_root = resolve_from_library(library_root, config["paths"]["retargeted"])
    input_root = resolve_from_library(library_root, phase["phase7_input"])
    artifact_root = resolve_from_library(library_root, phase["phase7_output"])
    ensure_below(artifact_root, retargeted_root)
    for directory in (database_root, catalog_root / "reports", logs_root, artifact_root):
        directory.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"retarget_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.retarget")

    blender = resolve_from_library(library_root, config["tools"]["blender"])
    target_fbx = resolve_from_library(library_root, phase["target_fbx"])
    source_profile = resolve_from_library(library_root, phase["source_profile"])
    target_profile = resolve_from_library(library_root, phase["target_profile"])
    retarget_map = resolve_from_library(library_root, phase["retarget_map"])
    script = Path(__file__).with_name("retarget_blender.py").resolve()
    validator = Path(__file__).with_name("validate_retargeted_blend.py").resolve()
    required = (blender, target_fbx, source_profile, target_profile, retarget_map, script, validator)
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise FileNotFoundError("Missing Phase 7 prerequisites: " + "; ".join(missing))

    animation_fields, animation_rows = load_csv(database_root / "animations.csv")
    _, pilot_rows = load_csv(database_root / "pilot_clips.csv")
    pilot_by_id = {row["AnimationID"]: row for row in pilot_rows}
    operations = []
    metadata_by_id = {}
    artifacts = {}

    for animation_id in phase["phase7_pilots"]:
        canonical = find_row(animation_rows, animation_id)
        source_blend = input_root / animation_id / f"{animation_id}.blend"
        output_directory = artifact_root / animation_id
        target_blend = output_directory / f"{animation_id}.blend"
        metadata_path = output_directory / f"{animation_id}.retarget.json"
        reopen_path = output_directory / f"{animation_id}.reopen.json"
        preview_path = catalog_root / "previews" / "phase_7" / f"{animation_id}.png"
        metadata = retarget(
            blender, script, source_blend, target_fbx, source_profile, target_profile, retarget_map,
            animation_id, canonical["LibraryID"], target_blend, metadata_path, logger,
        )
        reopen = reopen_validate(blender, validator, target_blend, metadata_path, reopen_path, logger)
        render_contact_sheet(metadata, preview_path)

        pilot = pilot_by_id[animation_id]
        timing_pass = (
            metadata["frameCount"] == int(pilot["FrameCount"])
            and metadata["frameRate"] == int(pilot["FrameRate"])
        )
        pose_pass = (
            metadata["invalidTransformCount"] == 0
            and metadata["minimumPoseHeight"] >= float(phase["minimum_pose_height"])
            and metadata["maximumPoseExtentFromPelvis"] <= float(phase["maximum_pose_extent"])
        )
        root_pass = metadata["rootTranslationError"] <= float(phase["root_translation_tolerance"])
        reopen_pass = reopen["status"] == "PASS"
        status = all((timing_pass, pose_pass, root_pass, reopen_pass))
        operation = {
            "RunID": run_id,
            "Timestamp": timestamp,
            "LibraryID": canonical["LibraryID"],
            "AnimationID": animation_id,
            "SourceBlendPath": relative_to_library(source_blend, library_root),
            "SourceBlendSHA256": sha256_for(source_blend),
            "TargetBlendPath": relative_to_library(target_blend, library_root),
            "TargetBlendSHA256": sha256_for(target_blend),
            "TargetBlendSizeBytes": str(target_blend.stat().st_size),
            "SourceProfile": metadata["sourceProfile"],
            "TargetProfile": metadata["targetProfile"],
            "RetargetProfile": metadata["retargetProfile"],
            "Algorithm": metadata["algorithm"],
            "MappedBoneCount": str(metadata["mappedBoneCount"]),
            "HeightScale": f"{metadata['heightScale']:.9f}",
            "FrameCount": str(metadata["frameCount"]),
            "FrameRate": str(metadata["frameRate"]),
            "DurationSeconds": f"{metadata['durationSeconds']:.9f}",
            "RootMotionStatus": metadata["rootMotionStatus"],
            "SourceRootTranslation": format_vector(metadata["sourceRootTranslation"]),
            "TargetRootTranslation": format_vector(metadata["targetRootTranslation"]),
            "RootTranslationError": f"{metadata['rootTranslationError']:.9f}",
            "MinimumPoseHeight": f"{metadata['minimumPoseHeight']:.9f}",
            "MaximumPoseExtentFromPelvis": f"{metadata['maximumPoseExtentFromPelvis']:.9f}",
            "MotionFingerprintSHA256": metadata["motionFingerprintSHA256"],
            "PoseIntegrity": "PASS" if pose_pass else "FAIL",
            "TimingValidation": "PASS" if timing_pass else "FAIL",
            "RootMotionValidation": "PASS" if root_pass else "FAIL",
            "ReopenValidation": "PASS" if reopen_pass else "FAIL",
            "RepeatabilityValidation": "NOT_SAMPLED",
            "Status": "PASS" if status else "FAIL",
            "PreviewPath": relative_to_library(preview_path, library_root),
            "Notes": f"SemanticConfidence={pilot['SemanticConfidence']}; target={metadata['targetProfile']}",
        }
        operations.append(operation)
        metadata_by_id[animation_id] = metadata
        artifacts[relative_to_library(target_blend, library_root)] = {
            "sha256": operation["TargetBlendSHA256"],
            "sizeBytes": target_blend.stat().st_size,
            "motionFingerprintSHA256": metadata["motionFingerprintSHA256"],
            "metadataSHA256": sha256_for(metadata_path),
            "reopenValidationSHA256": sha256_for(reopen_path),
        }

    repeat_id = phase["repeatability_animation"]
    with tempfile.TemporaryDirectory(prefix="project-s-phase7-repeat-") as temporary:
        temporary_root = Path(temporary)
        repeat_canonical = find_row(animation_rows, repeat_id)
        repeat_metadata = retarget(
            blender, script, input_root / repeat_id / f"{repeat_id}.blend", target_fbx,
            source_profile, target_profile, retarget_map, repeat_id, repeat_canonical["LibraryID"],
            temporary_root / f"{repeat_id}.blend", temporary_root / f"{repeat_id}.json", logger,
        )
    repeat_pass = repeat_metadata["motionFingerprintSHA256"] == metadata_by_id[repeat_id]["motionFingerprintSHA256"]
    repeat_operation = next(row for row in operations if row["AnimationID"] == repeat_id)
    repeat_operation["RepeatabilityValidation"] = "PASS" if repeat_pass else "FAIL"
    if not repeat_pass:
        repeat_operation["Status"] = "FAIL"
    if any(row["Status"] != "PASS" for row in operations):
        raise RuntimeError("Phase 7 hard gate failed: " + ", ".join(row["AnimationID"] for row in operations if row["Status"] != "PASS"))

    animations_path = database_root / "animations.csv"
    for animation_id in phase["phase7_pilots"]:
        canonical = find_row(animation_rows, animation_id)
        metadata = metadata_by_id[animation_id]
        root_magnitude = math.sqrt(sum(value * value for value in metadata["targetRootTranslation"]))
        canonical["SkeletonProfile"] = metadata["sourceProfile"]
        canonical["RetargetProfile"] = metadata["retargetProfile"]
        canonical["RetargetStatus"] = "PilotPassed"
        canonical["RootMotionStatus"] = "RootMotionPreservedScaled" if root_magnitude > 0.001 else "InPlace"
        canonical["RootTranslation"] = format_vector(metadata["targetRootTranslation"])
        canonical["PreviewPath"] = next(row["PreviewPath"] for row in operations if row["AnimationID"] == animation_id)
        canonical["UpdatedAt"] = timestamp
    write_csv_atomic(animations_path, tuple(animation_fields), animation_rows)

    operations_path = database_root / "retarget_operations.csv"
    _, existing_operations = load_csv(operations_path)
    write_csv_atomic(operations_path, OPERATION_FIELDS, existing_operations + operations)
    report_path = catalog_root / "reports" / "phase_7_status.md"
    build_report(report_path, timestamp, operations, relative_to_library(artifact_root, library_root))
    tracked_outputs = [operations_path, animations_path, report_path]
    tracked_outputs.extend(catalog_root / "previews" / "phase_7" / f"{animation_id}.png" for animation_id in phase["phase7_pilots"])
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["retarget"],
        "phaseConfigSHA256": hash_json(phase),
        "approvalScope": "PilotOnly/ER_Player_to_ProjectS_CLazy",
        "pilotCount": len(operations),
        "mappedBoneCount": int(operations[0]["MappedBoneCount"]),
        "repeatabilityAnimation": repeat_id,
        "outputs": {relative_to_library(path, library_root): sha256_for(path) for path in tracked_outputs},
        "artifacts": artifacts,
    }
    state_path = database_root / "phase_7_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase7RetargetPilot",
            "Tool": "Blender+Python",
            "ToolVersion": config["phase_versions"]["retarget"],
            "InputCount": len(operations),
            "SuccessCount": len(operations),
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": relative_to_library(log_path, library_root),
            "Notes": f"status=PASS; target=ProjectS_CLazy_Player; repeatability={repeat_id}",
        },
    )
    logger.info("Phase 7 complete: pilots=%d", len(operations))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
