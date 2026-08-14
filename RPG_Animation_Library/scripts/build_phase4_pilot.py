#!/usr/bin/env python3
"""Build the small Phase 4 semantic-preview pilot and preserve its raw TAE evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import math
import os
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

from pipeline_ledger import append_pipeline_run, new_run_id


PILOT_FIELDS = (
    "LibraryID",
    "AnimationID",
    "OriginalPath",
    "TimelinePath",
    "TimelineAnimationID",
    "FriendlyName",
    "SemanticConfidence",
    "CharacterType",
    "WeaponFamily",
    "ActionType",
    "ActionVariant",
    "Direction",
    "Loop",
    "FrameCount",
    "FrameRate",
    "DurationSeconds",
    "RootTranslationX",
    "RootTranslationY",
    "RootTranslationZ",
    "RootTranslationMagnitude",
    "PreviewMethod",
    "PreviewPath",
    "TAEEventCount",
    "NormalizedEventClasses",
    "QAStatus",
    "Notes",
)

TAE_EVENT_FIELDS = (
    "PilotLibraryID",
    "AnimationID",
    "TimelinePath",
    "TimelineAnimationID",
    "EventIndex",
    "RawEventID",
    "RawEventName",
    "RawEnumContext",
    "StartTime",
    "EndTime",
    "Duration",
    "GroupIndex",
    "NormalizedStartClass",
    "NormalizedEndClass",
    "ParametersJson",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    return parser.parse_args()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def hash_json(payload: object) -> str:
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def resolve_library_root(config_path: Path, config: dict) -> Path:
    configured = Path(config.get("project_root", "."))
    return (config_path.parent.parent / configured).resolve()


def resolve_from_library(library_root: Path, configured_path: str) -> Path:
    path = Path(configured_path)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def relative_to_library(path: Path, library_root: Path) -> str:
    return path.resolve().relative_to(library_root).as_posix()


def write_csv_atomic(path: Path, fieldnames: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(path)


def run_checked(
    command: list[str],
    cwd: Path,
    timeout_seconds: int,
    logger: logging.Logger,
    environment: dict[str, str] | None = None,
    accepted_return_codes: set[int] | None = None,
) -> int:
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
    accepted = accepted_return_codes or {0}
    if result.returncode not in accepted:
        raise RuntimeError(f"Command failed with exit code {result.returncode}: {subprocess.list2cmdline(command)}")
    return result.returncode


def unpack_tae(witchy: Path, source_tae: Path, work_root: Path, logger: logging.Logger) -> Path:
    work_root.mkdir(parents=True, exist_ok=True)
    work_tae = work_root / source_tae.name
    shutil.copy2(source_tae, work_tae)
    (work_root / "project.json").write_text(
        json.dumps({"GameType": 8, "ProjectName": "ProjectS_Phase4_TAE_ReadOnly"}, indent=2) + "\n",
        encoding="utf-8",
    )
    run_checked([str(witchy), "-s", "-u", str(work_tae)], work_root, 300, logger)
    tae_folder = work_root / f"{source_tae.stem}-tae"
    if not (tae_folder / "_witchy-tae.xml").is_file():
        raise RuntimeError(f"WitchyBND did not create a TAE folder at {tae_folder}")
    return tae_folder


def parse_template(path: Path) -> tuple[dict[str, str], dict[tuple[str, str, str], str]]:
    root = ET.parse(path).getroot()
    event_names: dict[str, str] = {}
    enum_names: dict[tuple[str, str, str], str] = {}
    for event in root.findall(".//event"):
        event_id = event.get("id")
        if event_id is None:
            continue
        event_names[event_id] = event.get("name", "Unknown")
        for parameter in list(event):
            parameter_name = parameter.get("name")
            if not parameter_name:
                continue
            for entry in parameter.findall("entry"):
                value = entry.get("value")
                label = entry.get("name")
                if value is not None and label:
                    enum_names[(event_id, parameter_name, value)] = label
    return event_names, enum_names


def normalize_event(event_id: str, event_name: str, enum_context: list[str]) -> tuple[str, str]:
    if event_id == "1":
        return "AttackWindowStart", "AttackWindowEnd"
    if event_id == "2":
        return "ProjectileSpawn", ""
    if event_id == "118":
        return "WeaponTrailStart", "WeaponTrailEnd"
    if event_id in {"96", "104", "110", "112", "114", "115", "116", "117", "119", "120", "121", "122", "123"}:
        return "VFX", ""
    if event_id in {"128", "129", "132", "133", "134"}:
        return "SFX", ""
    if event_id == "792":
        return "Footstep", ""
    if event_id == "0" and any("Cancel -" in context for context in enum_context):
        return "CancelWindowStart", "CancelWindowEnd"
    return "Unknown", "Unknown"


def parse_tae_animation(
    animation_id: str,
    tae_folder: Path,
    timeline_path: str,
    event_names: dict[str, str],
    enum_names: dict[tuple[str, str, str], str],
) -> tuple[bool, list[dict[str, str]]]:
    numeric_id = animation_id.rsplit("_", 1)[-1]
    animation_path = tae_folder / f"anim-{numeric_id}.xml"
    if not animation_path.is_file():
        raise FileNotFoundError(f"TAE animation entry was not found: {animation_path}")
    root = ET.parse(animation_path).getroot()
    loop = root.findtext("./header/loopByDefault", default="false").casefold() == "true"
    rows: list[dict[str, str]] = []
    for index, event in enumerate(root.findall("./events/event")):
        event_id = event.findtext("type", default="Unknown")
        event_name = event_names.get(event_id, "Unknown")
        parameters = {
            parameter.get("name", "Unknown"): parameter.get("value", "")
            for parameter in event.findall("./params/param")
        }
        enum_context = [
            f"{name}={value}: {enum_names[(event_id, name, value)]}"
            for name, value in parameters.items()
            if (event_id, name, value) in enum_names
        ]
        normalized_start, normalized_end = normalize_event(event_id, event_name, enum_context)
        start_text = event.findtext("startTime", default="0")
        end_text = event.findtext("endTime", default="0")
        rows.append(
            {
                "AnimationID": animation_id,
                "TimelinePath": timeline_path,
                "TimelineAnimationID": numeric_id,
                "EventIndex": str(index),
                "RawEventID": event_id,
                "RawEventName": event_name,
                "RawEnumContext": " | ".join(enum_context),
                "StartTime": start_text,
                "EndTime": end_text,
                "Duration": f"{float(end_text) - float(start_text):.9f}",
                "GroupIndex": event.findtext("group", default=""),
                "NormalizedStartClass": normalized_start,
                "NormalizedEndClass": normalized_end,
                "ParametersJson": json.dumps(parameters, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
            }
        )
    return loop, rows


def extract_pose(
    blender: Path,
    extractor: Path,
    flver: Path,
    base_anibnd: Path,
    animation_anibnd: Path,
    blender_user_scripts: Path,
    animation_id: str,
    output: Path,
    samples: int,
    logger: logging.Logger,
) -> dict:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(extractor),
        "--",
        "--flver",
        str(flver),
        "--base-anibnd",
        str(base_anibnd),
        "--animation-anibnd",
        str(animation_anibnd),
        "--animation",
        animation_id,
        "--output",
        str(output),
        "--samples",
        str(samples),
    ]
    environment = os.environ.copy()
    environment["BLENDER_USER_SCRIPTS"] = str(blender_user_scripts)
    return_code = run_checked(
        command,
        output.parent,
        300,
        logger,
        environment,
        {0, 11, -1073741819, 3221225477},
    )
    if not output.is_file():
        raise RuntimeError(f"Blender did not create pose metadata for {animation_id}")
    if return_code != 0:
        logger.warning(
            "Blender returned %d after writing %s; accepting the validated metadata output because this host crashes in igxelpicd64.dll during shutdown",
            return_code,
            output,
        )
    return json.loads(output.read_text(encoding="utf-8"))


def render_pose_contact_sheet(metadata: dict, output: Path) -> None:
    samples = metadata["samplePositions"]
    panel_count = len(samples)
    width, height = 1560, 520
    margin_x, margin_top, margin_bottom = 24, 55, 52
    panel_width = (width - margin_x * 2) / max(panel_count, 1)
    image = Image.new("RGB", (width, height), (10, 15, 28))
    draw = ImageDraw.Draw(image)

    all_points = [position for sample in samples for position in sample.values()]
    min_z = min(position[2] for position in all_points)
    max_z = max(position[2] for position in all_points)
    max_half_width = max(abs(position[0]) for position in all_points)
    scale = min(
        (height - margin_top - margin_bottom) / max(max_z - min_z, 0.001),
        panel_width * 0.76 / max(max_half_width * 2, 0.001),
    )

    def screen(position: list[float], sample_index: int) -> tuple[float, float]:
        panel_center = margin_x + panel_width * (sample_index + 0.5)
        return (
            panel_center + position[0] * scale,
            height - margin_bottom - (position[2] - min_z) * scale,
        )

    for sample_index, sample in enumerate(samples):
        for bone, parent in metadata["bodyParents"].items():
            if bone in sample and parent in sample:
                draw.line(
                    [screen(sample[parent], sample_index), screen(sample[bone], sample_index)],
                    fill=(65, 197, 240),
                    width=5,
                )
        for position in sample.values():
            x, y = screen(position, sample_index)
            draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(255, 132, 87))
        frame = metadata["sampleFrames"][sample_index]
        label_x = margin_x + panel_width * (sample_index + 0.5) - 18
        draw.text((label_x, height - 31), f"f{frame}", fill=(220, 230, 244))

    draw.text(
        (20, 15),
        f"{metadata['sourceAnimationId']} | {metadata['frameCount']} frames @ {metadata['frameRate']} fps",
        fill=(235, 241, 255),
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output)


def load_animation_records(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        return list(reader.fieldnames or []), list(reader)


def find_pilot_record(records: list[dict[str, str]], animation_id: str) -> dict[str, str]:
    matches = [
        record
        for record in records
        if record["RecordKind"] == "AnimationClip"
        and record["SourceStage"] == "UNPACKED"
        and record["Source"] == "EldenVins"
        and record["SourceCharacter"] == "c0000"
        and record["AnimationID"] == animation_id
        and "c0000-a00-hi" in record["OriginalPath"]
    ]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one canonical row for {animation_id}, found {len(matches)}")
    return matches[0]


def format_float(value: float) -> str:
    return f"{value:.6f}"


def build_report(
    path: Path,
    timestamp: str,
    pilot_rows: list[dict[str, str]],
    event_rows: list[dict[str, str]],
    rejected: list[dict],
    log_path: str,
) -> None:
    verified = sum(row["SemanticConfidence"] == "Verified" for row in pilot_rows)
    probable = sum(row["SemanticConfidence"] == "Probable" for row in pilot_rows)
    unknown = sum(row["SemanticConfidence"] == "Unknown" for row in pilot_rows)
    lines = [
        "# Phase 4 semantic preview pilot",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        "Result: **PASS**.",
        "",
        "## Scope and evidence policy",
        "",
        "Only six `c0000/a00_hi` clips were sampled. Semantic labels are based on a Blender/Soulstruct skeleton pose contact sheet plus the associated raw TAE entry; they are not extrapolated to the full library.",
        "",
        "`Verified` is used only when visible motion and an explicit named TAE flag agree. Pose-only interpretations remain `Probable`; ambiguous clips remain `Unknown`.",
        "",
        "DSAnimStudio remains the preferred interactive viewer. Its normal startup requires a game executable/project selection, so it was not used as a headless batch renderer. All non-Verified labels still require an interactive DSAnimStudio review before gameplay use.",
        "",
        "## Pilot results",
        "",
        "| Animation | Classification | Confidence | Events | QA |",
        "|---|---|---:|---:|---|",
    ]
    for row in pilot_rows:
        classification = f"{row['ActionType']} / {row['ActionVariant']}"
        lines.append(
            f"| `{row['AnimationID']}` | {classification} | {row['SemanticConfidence']} | {row['TAEEventCount']} | {row['QAStatus']} |"
        )
    lines.extend(
        [
            "",
            "## Counts",
            "",
            f"- Pilot clips: **{len(pilot_rows)}**",
            f"- Verified: **{verified}**",
            f"- Probable: **{probable}**",
            f"- Unknown: **{unknown}**",
            f"- Raw TAE events preserved: **{len(event_rows)}**",
            "",
            "## Rejected candidates",
            "",
            "These HKX entries looked like animation clips by filename but could not be parsed as animations by the current Soulstruct/Havok bridge. They remain in the canonical identity catalog and must not be silently treated as conversion-ready clips.",
            "",
            "| Animation | Import diagnostic |",
            "|---|---|",
        ]
    )
    for item in rejected:
        lines.append(f"| `{item['animation_id']}` | {item['reason']} |")
    lines.extend(
        [
            "",
            "## Outputs",
            "",
            "- `Database/pilot_clips.csv` — pilot-level semantic and pose metrics.",
            "- `Database/tae_events.csv` — lossless raw event identity/timing/parameters plus normalized boundary classes.",
            "- `Database/animations.csv` — curated fields updated only for the six selected canonical rows.",
            "- `03_CATALOG/previews/phase_4/*.png` — sampled skeleton pose contact sheets.",
            "- `Database/phase_4_state.json` — hashes of versioned outputs.",
            f"- `{log_path}` — execution log (ignored by Git).",
            "",
            "## Known limitations",
            "",
            "- Contact sheets show sampled skeleton poses, not final mesh deformation, weapon alignment, additive-layer composition, or camera-relative intent.",
            "- On this host Blender 5.2 writes valid pose metadata and then exits with an Intel `igxelpicd64.dll` access violation. The pipeline accepts that non-zero exit only when the expected JSON was already written and can be parsed.",
            "- `a000_010000` produces a collapsed full-body projection and stays unclassified.",
            "- `a000_080000` is visibly animated but has no sufficiently reliable action identity and stays unclassified.",
            "- `a000_020000` looks locomotion-like, but its TAE contains a `BulletBehavior` event; it remains only Probable.",
            "- Phase 3 currently classifies HKX primarily by identity/path conventions. The rejected entries demonstrate that Phase 5 needs a parseability gate before conversion status changes.",
            "",
            "## Next safe action",
            "",
            "Review the Probable/Unknown pilot clips interactively in DSAnimStudio, then use only an approved subset for the first HKX-to-Blender conversion gate. Do not batch-convert the library yet.",
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
    phase_config = config["semantic_preview"]
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    preview_root = resolve_from_library(library_root, phase_config["preview_output"])
    for directory in (database_root, catalog_root / "reports", logs_root, preview_root):
        directory.mkdir(parents=True, exist_ok=True)

    timestamp = utc_now()
    run_id = new_run_id()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    log_path = logs_root / f"semantic_preview_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.semantic-preview")
    log_relative_path = relative_to_library(log_path, library_root)

    witchy = resolve_from_library(library_root, config["tools"]["witchybnd"])
    blender = resolve_from_library(library_root, config["tools"]["blender"])
    source_tae = resolve_from_library(library_root, phase_config["source_tae"])
    template = resolve_from_library(library_root, phase_config["tae_template"])
    flver = resolve_from_library(library_root, phase_config["flver"])
    base_anibnd = resolve_from_library(library_root, phase_config["base_anibnd"])
    animation_anibnd = resolve_from_library(library_root, phase_config["animation_anibnd"])
    blender_bridge = resolve_from_library(library_root, config["tools"]["hkx_blender_bridge"])
    blender_user_scripts = blender_bridge.parent.parent
    extractor = Path(__file__).with_name("extract_pose_samples_blender.py").resolve()
    required = (witchy, blender, source_tae, template, flver, base_anibnd, animation_anibnd, extractor)
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing Phase 4 prerequisites: " + "; ".join(missing))

    animations_path = database_root / "animations.csv"
    fieldnames, animation_records = load_animation_records(animations_path)
    timeline_path = relative_to_library(source_tae, library_root)
    event_names, enum_names = parse_template(template)
    pilot_rows: list[dict[str, str]] = []
    all_event_rows: list[dict[str, str]] = []

    with tempfile.TemporaryDirectory(prefix="project-s-phase4-") as temporary:
        temporary_root = Path(temporary)
        tae_folder = unpack_tae(witchy, source_tae, temporary_root / "tae", logger)
        for pilot in phase_config["pilots"]:
            animation_id = pilot["animation_id"]
            logger.info("Processing pilot %s", animation_id)
            canonical = find_pilot_record(animation_records, animation_id)
            pose_path = temporary_root / "poses" / f"{animation_id}.json"
            pose_path.parent.mkdir(parents=True, exist_ok=True)
            pose = extract_pose(
                blender,
                extractor,
                flver,
                base_anibnd,
                animation_anibnd,
                blender_user_scripts,
                animation_id,
                pose_path,
                int(phase_config.get("sample_count", 6)),
                logger,
            )
            preview_path = preview_root / f"{animation_id}.png"
            render_pose_contact_sheet(pose, preview_path)
            loop, event_rows = parse_tae_animation(animation_id, tae_folder, timeline_path, event_names, enum_names)
            for event_row in event_rows:
                event_row["PilotLibraryID"] = canonical["LibraryID"]
            all_event_rows.extend(event_rows)

            first_root = pose["rootPositions"][0]
            last_root = pose["rootPositions"][-1]
            translation = [last_root[index] - first_root[index] for index in range(3)]
            magnitude = math.sqrt(sum(value * value for value in translation))
            duration = pose["frameCount"] / pose["frameRate"]
            normalized_classes = sorted(
                {
                    value
                    for event in event_rows
                    for value in (event["NormalizedStartClass"], event["NormalizedEndClass"])
                    if value and value != "Unknown"
                }
            )
            preview_relative = relative_to_library(preview_path, library_root)
            root_translation = ",".join(format_float(value) for value in translation)

            canonical.update(
                {
                    "SourceGame": "EldenRing",
                    "SourceCharacter": "c0000",
                    "CharacterType": pilot["character_type"],
                    "FriendlyName": pilot["friendly_name"],
                    "SemanticConfidence": pilot["confidence"],
                    "WeaponFamily": pilot["weapon_family"],
                    "ActionType": pilot["action_type"],
                    "ActionVariant": pilot["action_variant"],
                    "Direction": pilot["direction"],
                    "Loop": "Yes" if loop else "No",
                    "DurationSeconds": format_float(duration),
                    "FrameRate": str(pose["frameRate"]),
                    "RootMotionStatus": "Present" if magnitude > 0.05 else "InPlace",
                    "RootTranslation": root_translation,
                    "SkeletonProfile": "EldenRing_c0000",
                    "TAEReference": timeline_path,
                    "PreviewPath": preview_relative,
                    "QAStatus": pilot["qa_status"],
                    "Notes": pilot["notes"],
                    "UpdatedAt": timestamp,
                }
            )
            pilot_rows.append(
                {
                    "LibraryID": canonical["LibraryID"],
                    "AnimationID": animation_id,
                    "OriginalPath": canonical["OriginalPath"],
                    "TimelinePath": timeline_path,
                    "TimelineAnimationID": animation_id.rsplit("_", 1)[-1],
                    "FriendlyName": pilot["friendly_name"],
                    "SemanticConfidence": pilot["confidence"],
                    "CharacterType": pilot["character_type"],
                    "WeaponFamily": pilot["weapon_family"],
                    "ActionType": pilot["action_type"],
                    "ActionVariant": pilot["action_variant"],
                    "Direction": pilot["direction"],
                    "Loop": "Yes" if loop else "No",
                    "FrameCount": str(pose["frameCount"]),
                    "FrameRate": str(pose["frameRate"]),
                    "DurationSeconds": format_float(duration),
                    "RootTranslationX": format_float(translation[0]),
                    "RootTranslationY": format_float(translation[1]),
                    "RootTranslationZ": format_float(translation[2]),
                    "RootTranslationMagnitude": format_float(magnitude),
                    "PreviewMethod": "BlenderSoulstructPoseContactSheet",
                    "PreviewPath": preview_relative,
                    "TAEEventCount": str(len(event_rows)),
                    "NormalizedEventClasses": "|".join(normalized_classes),
                    "QAStatus": pilot["qa_status"],
                    "Notes": pilot["notes"],
                }
            )

    pilot_rows.sort(key=lambda row: row["AnimationID"])
    all_event_rows.sort(key=lambda row: (row["AnimationID"], int(row["EventIndex"])))
    write_csv_atomic(database_root / "pilot_clips.csv", PILOT_FIELDS, pilot_rows)
    write_csv_atomic(database_root / "tae_events.csv", TAE_EVENT_FIELDS, all_event_rows)
    write_csv_atomic(animations_path, tuple(fieldnames), animation_records)

    report_path = catalog_root / "reports" / "phase_4_status.md"
    build_report(report_path, timestamp, pilot_rows, all_event_rows, phase_config["rejected_candidates"], log_relative_path)
    outputs = [database_root / "pilot_clips.csv", database_root / "tae_events.csv", animations_path, report_path]
    outputs.extend(preview_root / f"{pilot['animation_id']}.png" for pilot in phase_config["pilots"])
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["semantic_preview"],
        "phaseConfigSHA256": hash_json(phase_config),
        "pilotCount": len(pilot_rows),
        "eventCount": len(all_event_rows),
        "outputs": {relative_to_library(path, library_root): sha256_for(path) for path in outputs},
    }
    state_path = database_root / "phase_4_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase4SemanticPreview",
            "Tool": "WitchyBND+Blender+Soulstruct",
            "ToolVersion": config["phase_versions"]["semantic_preview"],
            "InputCount": len(pilot_rows),
            "SuccessCount": len(pilot_rows),
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": log_relative_path,
            "Notes": f"status=PASS; events={len(all_event_rows)}; rejected_candidates={len(phase_config['rejected_candidates'])}",
        },
    )
    logger.info("Phase 4 complete: pilots=%d events=%d", len(pilot_rows), len(all_event_rows))
    return 0


if __name__ == "__main__":
    sys.exit(main())
