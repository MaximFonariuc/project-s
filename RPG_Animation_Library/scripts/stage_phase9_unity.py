#!/usr/bin/env python3
"""Stage Phase 8 contracts beside their future Unity FBX asset paths."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
from pathlib import Path


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


def resolve_library_root(config_path: Path, config: dict) -> Path:
    return (config_path.parent.parent / config.get("project_root", ".")).resolve()


def resolve_from_library(library_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def load_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        return list(csv.DictReader(source))


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    phase = config["unity_import"]
    library_root = resolve_library_root(config_path, config)
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    phase8_root = resolve_from_library(library_root, phase["phase9_input"])
    phase9_root = resolve_from_library(library_root, phase["phase9_output"])
    unity_root = resolve_from_library(library_root, phase["unity_asset_root"])
    operations = load_csv(database_root / "fbx_export_operations.csv")

    latest_by_animation: dict[str, dict[str, str]] = {}
    for operation in operations:
        if operation.get("Status") == "PASS":
            latest_by_animation[operation["AnimationID"]] = operation
    expected_ids = config["fbx_export"]["phase8_pilots"]
    if sorted(latest_by_animation) != sorted(expected_ids):
        raise RuntimeError("Phase 8 PASS set does not match the Phase 9 pilot")

    imports = []
    for animation_id in expected_ids:
        operation = latest_by_animation[animation_id]
        source_fbx = resolve_from_library(library_root, operation["FBXPath"])
        source_metadata = resolve_from_library(library_root, operation["MetadataPath"])
        relative_fbx = source_fbx.relative_to(phase8_root)
        destination_fbx = unity_root / relative_fbx
        destination_metadata = destination_fbx.with_suffix(".animlib.json")
        if not destination_metadata.parent.is_dir():
            raise RuntimeError(f"Unity destination folder was not created through the Editor: {destination_metadata.parent}")
        contract = json.loads(source_metadata.read_text(encoding="utf-8"))
        if sha256_for(source_fbx) != contract["FBXSHA256"]:
            raise RuntimeError(f"Phase 8 FBX hash mismatch: {source_fbx}")
        shutil.copyfile(source_metadata, destination_metadata)
        imports.append(
            {
                "AnimationID": animation_id,
                "LibraryID": contract["LibraryID"],
                "SourceFBX": source_fbx.as_posix(),
                "SourceMetadata": source_metadata.as_posix(),
                "DestinationFolder": destination_fbx.parent.as_posix(),
                "DestinationFBX": destination_fbx.as_posix(),
                "DestinationMetadata": destination_metadata.as_posix(),
                "FBXSHA256": contract["FBXSHA256"],
                "MetadataSHA256": sha256_for(destination_metadata),
            }
        )

    phase9_root.mkdir(parents=True, exist_ok=True)
    manifest = {
        "schemaVersion": 1,
        "status": "READY",
        "pipelineVersion": config["pipeline_version"],
        "unityAssetRoot": unity_root.as_posix(),
        "importCount": len(imports),
        "imports": imports,
    }
    manifest_path = phase9_root / "staging_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(manifest_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
