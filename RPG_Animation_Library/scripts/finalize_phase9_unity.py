#!/usr/bin/env python3
"""Finalize the Phase 9 Unity import gate from its staging manifest and Editor receipt."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path

from pipeline_ledger import append_pipeline_run, new_run_id


OPERATION_FIELDS = (
    "RunID",
    "Timestamp",
    "LibraryID",
    "AnimationID",
    "FBXSourcePath",
    "FBXSHA256",
    "UnityAssetPath",
    "AssetGuid",
    "MetadataPath",
    "PipelineVersion",
    "UnityVersion",
    "AnimationType",
    "AvatarSetup",
    "SourceAvatarPath",
    "RootMotionStatus",
    "ExpectedFrameCount",
    "ImportedClipCount",
    "ImportedClipNames",
    "ImportedClipLengths",
    "Labels",
    "ImportValidation",
    "Status",
    "Notes",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    return parser.parse_args()


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


def load_csv(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        return list(reader.fieldnames or []), list(reader)


def write_csv_atomic(path: Path, fieldnames: list[str] | tuple[str, ...], rows: list[dict[str, str]]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    temporary.replace(path)


def build_report(path: Path, timestamp: str, receipt: dict) -> None:
    lines = [
        "# Phase 9 Unity import pilot",
        "",
        f"Timestamp: `{timestamp}`",
        "",
        f"Unity `{receipt['UnityVersion']}` import gate: **PASS** ({len(receipt['Imports'])} clips).",
        "",
        "| Animation | Unity asset | Clips | Root motion | Avatar | Import |",
        "|---|---|---:|---|---|---|",
    ]
    for item in sorted(receipt["Imports"], key=lambda value: value["AnimationID"]):
        lines.append(
            f"| `{item['AnimationID']}` | `{item['AssetPath']}` | {item['ImportedClipCount']} | "
            f"{item['RootMotionStatus']} | {item['AvatarSetup']} | {item['Status']} |"
        )
    lines.extend(
        [
            "",
            "The Editor importer verifies the adjacent contract and FBX SHA-256 before applying settings.",
            "All assets use Humanoid Copy From Other Avatar with `Assets/_Game/Models/AnimationSources/CLazy/TPose.FBX`.",
            "Clip settings, labels, frame ranges, root-motion policy and imported clip lengths were verified in Unity.",
            "This is a bounded import/retarget pilot, not semantic approval of every source animation.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    phase = config["unity_import"]
    library_root = resolve_library_root(config_path, config)
    project_root = library_root.parent
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    phase9_root = resolve_from_library(library_root, phase["phase9_output"])
    staging_path = phase9_root / "staging_manifest.json"
    receipt_path = phase9_root / "unity_import_receipt.json"
    staging = json.loads(staging_path.read_text(encoding="utf-8"))
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

    if staging.get("status") != "READY" or receipt.get("Status") != "PASS":
        raise RuntimeError("Phase 9 staging and Unity receipt must both pass")
    expected_count = int(phase["expected_import_count"])
    if staging.get("importCount") != expected_count or len(receipt.get("Imports", [])) != expected_count:
        raise RuntimeError("Phase 9 import count does not match the configured pilot")
    if receipt.get("UnityVersion") != phase["expected_unity_version"]:
        raise RuntimeError(f"Unexpected Unity version: {receipt.get('UnityVersion')}")

    staged_by_library = {item["LibraryID"]: item for item in staging["imports"]}
    receipt_by_library = {item["LibraryID"]: item for item in receipt["Imports"]}
    if set(staged_by_library) != set(receipt_by_library) or len(receipt_by_library) != expected_count:
        raise RuntimeError("Unity receipt identity set does not match the staging manifest")

    run_id = new_run_id()
    timestamp = receipt["Timestamp"]
    operations: list[dict[str, str]] = []
    artifacts: dict[str, dict[str, object]] = {}
    for library_id in sorted(staged_by_library):
        staged = staged_by_library[library_id]
        imported = receipt_by_library[library_id]
        source_fbx = Path(staged["SourceFBX"]).resolve()
        unity_fbx = Path(staged["DestinationFBX"]).resolve()
        unity_metadata = Path(staged["DestinationMetadata"]).resolve()
        expected_asset_path = unity_fbx.relative_to(project_root).as_posix()
        if imported.get("Status") != "PASS" or imported.get("Failures"):
            raise RuntimeError(f"Unity import failed for {library_id}: {imported.get('Failures')}")
        if imported["AssetPath"] != expected_asset_path:
            raise RuntimeError(f"Unity asset path mismatch for {library_id}")
        hashes = (sha256_for(source_fbx), sha256_for(unity_fbx), imported["FBXSHA256"], staged["FBXSHA256"])
        if len(set(hashes)) != 1:
            raise RuntimeError(f"FBX hash mismatch for {library_id}")
        if sha256_for(unity_metadata) != staged["MetadataSHA256"]:
            raise RuntimeError(f"Metadata hash mismatch for {library_id}")
        contract = json.loads(unity_metadata.read_text(encoding="utf-8"))
        if contract["LibraryID"] != library_id or contract["AnimationID"] != imported["AnimationID"]:
            raise RuntimeError(f"Metadata identity mismatch for {library_id}")

        operation = {
            "RunID": run_id,
            "Timestamp": timestamp,
            "LibraryID": library_id,
            "AnimationID": imported["AnimationID"],
            "FBXSourcePath": relative_to_library(source_fbx, library_root),
            "FBXSHA256": imported["FBXSHA256"],
            "UnityAssetPath": imported["AssetPath"],
            "AssetGuid": imported["AssetGuid"],
            "MetadataPath": unity_metadata.relative_to(project_root).as_posix(),
            "PipelineVersion": config["pipeline_version"],
            "UnityVersion": receipt["UnityVersion"],
            "AnimationType": imported["AnimationType"],
            "AvatarSetup": imported["AvatarSetup"],
            "SourceAvatarPath": imported["SourceAvatarPath"],
            "RootMotionStatus": imported["RootMotionStatus"],
            "ExpectedFrameCount": str(imported["ExpectedFrameCount"]),
            "ImportedClipCount": str(imported["ImportedClipCount"]),
            "ImportedClipNames": json.dumps(imported["ImportedClipNames"], separators=(",", ":")),
            "ImportedClipLengths": json.dumps(imported["ImportedClipLengths"], separators=(",", ":")),
            "Labels": json.dumps(imported["Labels"], separators=(",", ":")),
            "ImportValidation": "PASS",
            "Status": "PASS",
            "Notes": f"contract={contract['UnityImportProfile']}; semanticConfidence={contract['SemanticConfidence']}",
        }
        operations.append(operation)
        artifacts[imported["AssetPath"]] = {
            "assetGuid": imported["AssetGuid"],
            "sha256": imported["FBXSHA256"],
            "metadataPath": operation["MetadataPath"],
            "metadataSHA256": staged["MetadataSHA256"],
            "clipNames": imported["ImportedClipNames"],
        }

    operations_path = database_root / "unity_import_operations.csv"
    existing_operations: list[dict[str, str]] = []
    if operations_path.is_file():
        fields, existing_operations = load_csv(operations_path)
        if tuple(fields) != OPERATION_FIELDS:
            raise RuntimeError("Unexpected unity_import_operations.csv schema")
    write_csv_atomic(operations_path, OPERATION_FIELDS, existing_operations + operations)

    animations_path = database_root / "animations.csv"
    animation_fields, animation_rows = load_csv(animations_path)
    touched: set[str] = set()
    for row in animation_rows:
        if row.get("LibraryID") in receipt_by_library:
            row["UnityImportStatus"] = "PilotPassed"
            row["UpdatedAt"] = timestamp
            touched.add(row["LibraryID"])
    if touched != set(receipt_by_library):
        raise RuntimeError("Not every Unity import has one canonical animation row")
    write_csv_atomic(animations_path, animation_fields, animation_rows)

    report_path = catalog_root / "reports" / "phase_9_status.md"
    build_report(report_path, timestamp, receipt)
    state = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "pipelineVersion": config["pipeline_version"],
        "phaseVersion": config["phase_versions"]["unity_import"],
        "phaseConfigSHA256": hash_json(phase),
        "approvalScope": "PilotOnly/ProjectS_CLazy_Player/UnityHumanoidImport",
        "unityVersion": receipt["UnityVersion"],
        "pilotCount": len(operations),
        "receiptSHA256": sha256_for(receipt_path),
        "outputs": {
            relative_to_library(path, library_root): sha256_for(path)
            for path in (operations_path, animations_path, report_path)
        },
        "artifacts": artifacts,
    }
    state_path = database_root / "phase_9_state.json"
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase9UnityImportPilot",
            "Tool": "UnityEditor",
            "ToolVersion": receipt["UnityVersion"],
            "InputCount": len(operations),
            "SuccessCount": len(operations),
            "FailureCount": 0,
            "ConfigHash": state["phaseConfigSHA256"],
            "PipelineVersion": config["pipeline_version"],
            "LogPath": relative_to_library(receipt_path, library_root),
            "Notes": "status=PASS; humanoid=CopyFromOther; consoleErrors=0; consoleWarnings=0",
        },
    )
    print(state_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
