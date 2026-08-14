#!/usr/bin/env python3
"""Validate measured source/target skeleton profiles and the family retarget map."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
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
    "ProfileID",
    "Role",
    "SourcePath",
    "SourceSHA256",
    "ArmatureName",
    "BoneCount",
    "RestSkeletonHeightMeters",
    "CoordinateSpace",
    "UnityRigType",
    "RequiredBoneValidation",
    "HierarchyValidation",
    "MeasurementValidation",
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
    try:
        return path.resolve().relative_to(library_root).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        return list(csv.DictReader(source))


def write_csv_atomic(path: Path, rows: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=OPERATION_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(path)


def inspect(
    blender: Path,
    inspector: Path,
    source_flag: str,
    source_path: Path,
    output_path: Path,
    logger: logging.Logger,
) -> dict:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(inspector),
        "--",
        source_flag,
        str(source_path),
        "--output",
        str(output_path),
    ]
    logger.info("Run: %s", subprocess.list2cmdline(command))
    result = subprocess.run(command, text=True, capture_output=True, timeout=180, check=False)
    if result.stdout:
        logger.info(result.stdout.rstrip())
    if result.stderr:
        logger.warning(result.stderr.rstrip())
    if result.returncode != 0 or not output_path.is_file():
        raise RuntimeError(f"Skeleton inspection failed for {source_path} (exit={result.returncode})")
    return json.loads(output_path.read_text(encoding="utf-8"))


def validate_profile(profile: dict, inspection: dict, tolerance: float) -> tuple[bool, bool, bool, list[str]]:
    inspected_bones = {bone["name"]: bone for bone in inspection["bones"]}
    required = list(profile["bones"].values())
    root_motion = profile["rootMotion"]
    required.extend(value for key, value in root_motion.items() if key.endswith("Bone") and isinstance(value, str))
    missing = sorted(set(required) - inspected_bones.keys())
    required_pass = not missing

    hierarchy_pass = True
    notes: list[str] = []
    if profile["id"] == "ER_Player":
        hierarchy_pass = (
            inspected_bones["Spine"]["parent"] == "RootRotY"
            and inspected_bones["Spine1"]["parent"] == "Spine"
            and inspected_bones["L_Calf"]["parent"] == "L_Thigh"
            and inspected_bones["Pelvis"]["parent"] is None
        )
    elif profile["id"] == "ProjectS_CLazy_Player":
        hierarchy_pass = (
            inspected_bones["pelvis"]["parent"] is None
            and inspected_bones["spine_01"]["parent"] == "pelvis"
            and inspected_bones["lowerarm_l"]["parent"] == "upperarm_l"
            and inspected_bones["calf_l"]["parent"] == "thigh_l"
        )

    measured_height = float(inspection["boundsWorld"]["dimensions"][2])
    measurement_pass = (
        inspection["armature"] == profile["armatureName"]
        and int(inspection["boneCount"]) == int(profile["boneCount"])
        and abs(measured_height - float(profile["restSkeletonHeightMeters"])) <= tolerance
    )
    if missing:
        notes.append("missing=" + "|".join(missing))
    if not hierarchy_pass:
        notes.append("expected hierarchy mismatch")
    if not measurement_pass:
        notes.append(f"measuredHeight={measured_height:.9f}")
    return required_pass, hierarchy_pass, measurement_pass, notes


def build_report(path: Path, timestamp: str, operations: list[dict[str, str]], mapping_count: int) -> None:
    lines = [
        "# Phase 6 skeleton profiles",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        "Mechanical gate result: **PASS**.",
        "",
        "| Profile | Role | Bones | Height (m) | Required bones | Hierarchy | Measurement |",
        "|---|---|---:|---:|---|---|---|",
    ]
    for row in operations:
        lines.append(
            f"| `{row['ProfileID']}` | {row['Role']} | {row['BoneCount']} | "
            f"{row['RestSkeletonHeightMeters']} | {row['RequiredBoneValidation']} | "
            f"{row['HierarchyValidation']} | {row['MeasurementValidation']} |"
        )
    lines.extend(
        [
            "",
            f"The family retarget map contains `{mapping_count}` explicit deform-bone mappings.",
            "",
            "Root motion ownership is explicit: armature object translation/rotation is transferred once, while Pelvis supplies local pose height and body rotation.",
            "Service, control, cloth, SFX and weapon bones are outside this pilot boundary.",
            "The target profile points at the same Humanoid Avatar used by the current Player prefab.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    phase = config["skeleton_profile"]
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    database_root.mkdir(parents=True, exist_ok=True)
    logs_root.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"skeleton_profiles_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.skeleton-profile")

    blender = resolve_from_library(library_root, config["tools"]["blender"])
    inspector = Path(__file__).with_name("inspect_skeleton_blender.py").resolve()
    source_blend = resolve_from_library(library_root, phase["source_blend"])
    target_fbx = resolve_from_library(library_root, phase["target_fbx"])
    source_profile_path = resolve_from_library(library_root, phase["source_profile"])
    target_profile_path = resolve_from_library(library_root, phase["target_profile"])
    retarget_map_path = resolve_from_library(library_root, phase["retarget_map"])
    required_paths = (blender, inspector, source_blend, target_fbx, source_profile_path, target_profile_path, retarget_map_path)
    missing_paths = [str(path) for path in required_paths if not path.exists()]
    if missing_paths:
        raise FileNotFoundError("Missing Phase 6 prerequisites: " + "; ".join(missing_paths))

    source_profile = json.loads(source_profile_path.read_text(encoding="utf-8"))
    target_profile = json.loads(target_profile_path.read_text(encoding="utf-8"))
    retarget_map = json.loads(retarget_map_path.read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="project-s-phase6-") as temporary:
        temporary_root = Path(temporary)
        source_inspection = inspect(blender, inspector, "--blend", source_blend, temporary_root / "source.json", logger)
        target_inspection = inspect(blender, inspector, "--fbx", target_fbx, temporary_root / "target.json", logger)

    tolerance = float(phase["height_tolerance_meters"])
    operations = []
    for role, profile, profile_path, source_path, inspection in (
        ("Source", source_profile, source_profile_path, source_blend, source_inspection),
        ("Target", target_profile, target_profile_path, target_fbx, target_inspection),
    ):
        required_pass, hierarchy_pass, measurement_pass, notes = validate_profile(profile, inspection, tolerance)
        status = required_pass and hierarchy_pass and measurement_pass
        operations.append(
            {
                "RunID": run_id,
                "Timestamp": timestamp,
                "ProfileID": profile["id"],
                "Role": role,
                "SourcePath": relative_to_library(source_path, library_root),
                "SourceSHA256": sha256_for(source_path),
                "ArmatureName": inspection["armature"],
                "BoneCount": str(inspection["boneCount"]),
                "RestSkeletonHeightMeters": f"{inspection['boundsWorld']['dimensions'][2]:.9f}",
                "CoordinateSpace": profile["coordinateSpace"],
                "UnityRigType": profile["unityRigType"],
                "RequiredBoneValidation": "PASS" if required_pass else "FAIL",
                "HierarchyValidation": "PASS" if hierarchy_pass else "FAIL",
                "MeasurementValidation": "PASS" if measurement_pass else "FAIL",
                "Status": "PASS" if status else "FAIL",
                "Notes": "; ".join(notes),
            }
        )

    mappings = retarget_map["boneMappings"]
    source_names = {bone["name"] for bone in source_inspection["bones"]}
    target_names = {bone["name"] for bone in target_inspection["bones"]}
    mapping_valid = (
        retarget_map["sourceProfile"] == source_profile["id"]
        and retarget_map["targetProfile"] == target_profile["id"]
        and len(mappings) >= int(phase["minimum_mapped_bones"])
        and len({item["source"] for item in mappings}) == len(mappings)
        and len({item["target"] for item in mappings}) == len(mappings)
        and all(item["source"] in source_names and item["target"] in target_names for item in mappings)
    )
    if not mapping_valid or any(row["Status"] != "PASS" for row in operations):
        raise RuntimeError("Phase 6 skeleton-profile hard gate failed")

    operation_path = database_root / "skeleton_profile_operations.csv"
    write_csv_atomic(operation_path, load_csv(operation_path) + operations)
    report_path = catalog_root / "reports" / "phase_6_status.md"
    build_report(report_path, timestamp, operations, len(mappings))
    tracked_outputs = (source_profile_path, target_profile_path, retarget_map_path, operation_path, report_path)
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["skeleton_profile"],
        "phaseConfigSHA256": hash_json(phase),
        "sourceProfile": source_profile["id"],
        "targetProfile": target_profile["id"],
        "retargetMap": retarget_map["id"],
        "mappedBoneCount": len(mappings),
        "heightScale": target_profile["restSkeletonHeightMeters"] / source_profile["restSkeletonHeightMeters"],
        "outputs": {relative_to_library(path, library_root): sha256_for(path) for path in tracked_outputs},
    }
    state_path = database_root / "phase_6_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase6SkeletonProfiles",
            "Tool": "Blender+Python",
            "ToolVersion": config["phase_versions"]["skeleton_profile"],
            "InputCount": 2,
            "SuccessCount": 2,
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": relative_to_library(log_path, library_root),
            "Notes": f"status=PASS; mappings={len(mappings)}; targetAvatar={target_profile['sourceAvatarGuid']}",
        },
    )
    logger.info("Phase 6 complete: mappings=%d", len(mappings))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
