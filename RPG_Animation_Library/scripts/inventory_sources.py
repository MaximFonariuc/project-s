#!/usr/bin/env python3
"""Build a deterministic, read-only inventory of configured animation RAW sources."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import platform
import sys
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Iterable


COMPOUND_EXTENSIONS = (
    ".anibnd.dcx",
    ".chrbnd.dcx",
    ".behbnd.dcx",
    ".geombnd.dcx",
    ".geomhkxbnd.dcx",
    ".hkxbnd.dcx",
    ".objbnd.dcx",
    ".partsbnd.dcx",
)


@dataclass(frozen=True)
class RawFileRecord:
    Source: str
    SourcePackageVersion: str | None
    RelativePath: str
    Extension: str
    SizeBytes: int
    SHA256: str
    ModifiedTime: str
    ScanTimestamp: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument(
        "--force",
        action="store_true",
        help="Recalculate every SHA-256 even when source metadata matches the previous manifest.",
    )
    return parser.parse_args()


def iso_utc(timestamp: float) -> str:
    return datetime.fromtimestamp(timestamp, timezone.utc).isoformat().replace("+00:00", "Z")


def extension_for(path: Path) -> str:
    lower_name = path.name.lower()
    for extension in COMPOUND_EXTENSIONS:
        if lower_name.endswith(extension):
            return extension
    return path.suffix.lower()


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def enumerate_files(root: Path) -> Iterable[tuple[Path, Path]]:
    if root.is_file():
        yield root, Path(root.name)
        return
    if not root.is_dir():
        raise FileNotFoundError(root)
    files = (path for path in root.rglob("*") if path.is_file())
    for path in sorted(files, key=lambda item: item.relative_to(root).as_posix().casefold()):
        yield path, path.relative_to(root)


def resolve_library_root(config_path: Path, config: dict) -> Path:
    configured = Path(config.get("project_root", "."))
    return (config_path.parent.parent / configured).resolve()


def resolve_from_library(library_root: Path, configured_path: str) -> Path:
    path = Path(configured_path)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def load_previous_manifest(path: Path) -> tuple[dict, dict[tuple[str, str], RawFileRecord]]:
    if not path.is_file():
        return {}, {}

    payload = json.loads(path.read_text(encoding="utf-8"))
    records = {
        (record["Source"], record["RelativePath"]): RawFileRecord(**record)
        for record in payload.get("records", [])
    }
    return payload, records


def collect_records(
    config: dict,
    library_root: Path,
    scan_timestamp: str,
    previous_records: dict[tuple[str, str], RawFileRecord],
    force: bool,
) -> tuple[list[RawFileRecord], int, int]:
    records: list[RawFileRecord] = []
    visited: set[tuple[str, Path]] = set()
    hashed_files = 0
    reused_hashes = 0

    for source in sorted(config.get("sources", []), key=lambda item: item["name"].casefold()):
        source_name = source["name"]
        package_version = source.get("package_version")
        roots = sorted(source.get("roots", []), key=lambda item: item["path"].casefold())

        for root_config in roots:
            physical_root = resolve_from_library(library_root, root_config["path"])
            prefix = PurePosixPath(root_config.get("relative_prefix", physical_root.name))

            for path, relative_path in enumerate_files(physical_root):
                resolved_path = path.resolve()
                identity = (source_name, resolved_path)
                if identity in visited:
                    continue
                visited.add(identity)

                before = path.stat()
                logical_path = (prefix / PurePosixPath(relative_path.as_posix())).as_posix()
                modified_time = iso_utc(before.st_mtime)
                previous = previous_records.get((source_name, logical_path))
                can_reuse = (
                    not force
                    and previous is not None
                    and previous.SizeBytes == before.st_size
                    and previous.ModifiedTime == modified_time
                )
                if can_reuse:
                    digest = previous.SHA256
                    reused_hashes += 1
                else:
                    digest = sha256_for(path)
                    hashed_files += 1
                after = path.stat()
                if before.st_size != after.st_size or before.st_mtime_ns != after.st_mtime_ns:
                    raise RuntimeError(f"RAW source changed while being inventoried: {path}")

                records.append(
                    RawFileRecord(
                        Source=source_name,
                        SourcePackageVersion=package_version,
                        RelativePath=logical_path,
                        Extension=extension_for(path),
                        SizeBytes=before.st_size,
                        SHA256=digest,
                        ModifiedTime=modified_time,
                        ScanTimestamp=scan_timestamp,
                    )
                )

    sorted_records = sorted(records, key=lambda item: (item.Source.casefold(), item.RelativePath.casefold()))
    return sorted_records, hashed_files, reused_hashes


def source_fingerprint(records: list[RawFileRecord]) -> str:
    digest = hashlib.sha256()
    for record in records:
        digest.update(record.Source.encode("utf-8"))
        digest.update(b"\0")
        digest.update(record.RelativePath.encode("utf-8"))
        digest.update(b"\0")
        digest.update(record.SHA256.encode("ascii"))
        digest.update(b"\n")
    return digest.hexdigest()


def write_json(
    path: Path,
    records: list[RawFileRecord],
    scan_timestamp: str,
    pipeline_version: str,
    config_sha256: str,
    fingerprint: str,
) -> None:
    payload = {
        "schemaVersion": 1,
        "pipelineVersion": pipeline_version,
        "configSHA256": config_sha256,
        "sourceFingerprint": fingerprint,
        "scanTimestamp": scan_timestamp,
        "recordCount": len(records),
        "records": [asdict(record) for record in records],
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, records: list[RawFileRecord]) -> None:
    fieldnames = list(RawFileRecord.__dataclass_fields__.keys())
    with path.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(asdict(record) for record in records)


def write_status(
    path: Path,
    records: list[RawFileRecord],
    scan_timestamp: str,
    configured_sources: list[dict],
    hashed_files: int,
    reused_hashes: int,
) -> None:
    by_source: dict[str, Counter[str]] = defaultdict(Counter)
    bytes_by_source_and_extension: dict[str, Counter[str]] = defaultdict(Counter)
    hashes: dict[str, list[RawFileRecord]] = defaultdict(list)
    for record in records:
        by_source[record.Source][record.Extension or "<none>"] += 1
        bytes_by_source_and_extension[record.Source][record.Extension or "<none>"] += record.SizeBytes
        hashes[record.SHA256].append(record)

    duplicate_groups = [group for group in hashes.values() if len(group) > 1]
    lines = [
        "# Phase 1 source inventory",
        "",
        f"Scan timestamp: `{scan_timestamp}`",
        "",
        "Result: **PASS**. Every file in every configured RAW root is represented by a read-only SHA-256 inventory in deterministic source/path order. This run reused hashes whose size and modification time still match; use `--force` for a complete re-hash.",
        "",
        "## Totals",
        "",
        f"- Files: **{len(records):,}**",
        f"- Bytes: **{sum(record.SizeBytes for record in records):,}**",
        f"- Unique SHA-256 values: **{len(hashes):,}**",
        f"- Duplicate hash groups: **{len(duplicate_groups):,}**",
        f"- SHA-256 calculated this run: **{hashed_files:,}**",
        f"- SHA-256 reused from unchanged records: **{reused_hashes:,}**",
        "",
        "## Counts by source and type",
        "",
        "| Source | Extension | Files | Bytes |",
        "|---|---:|---:|---:|",
    ]
    for source in sorted(by_source, key=str.casefold):
        first = True
        for extension, count in sorted(by_source[source].items(), key=lambda item: (-item[1], item[0])):
            lines.append(
                f"| {source if first else ''} | `{extension}` | {count:,} | "
                f"{bytes_by_source_and_extension[source][extension]:,} |"
            )
            first = False

    missing_sources = [source["name"] for source in configured_sources if not source.get("roots")]
    lines.extend(["", "## Missing configured sources", ""])
    if missing_sources:
        lines.extend(f"- {source}" for source in missing_sources)
    else:
        lines.append("None.")

    lines.extend(
        [
            "",
            "## Outputs",
            "",
            "- `03_CATALOG/manifests/raw_files.csv`",
            "- `03_CATALOG/manifests/raw_files.json`",
            "",
            "Duplicate files can be queried by grouping the JSON records on `SHA256`.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def append_run_ledger(
    path: Path,
    records: list[RawFileRecord],
    scan_timestamp: str,
    status: str,
    manifest_sha256: str,
    config_sha256: str,
    pipeline_version: str,
) -> None:
    fieldnames = (
        "RunTimestamp",
        "Phase",
        "Status",
        "RecordCount",
        "ManifestSHA256",
        "ConfigSHA256",
        "PipelineVersion",
        "Python",
        "HostOS",
    )
    row = {
        "RunTimestamp": scan_timestamp,
        "Phase": "Phase1Inventory",
        "Status": status,
        "RecordCount": str(len(records)),
        "ManifestSHA256": manifest_sha256,
        "ConfigSHA256": config_sha256,
        "PipelineVersion": pipeline_version,
        "Python": platform.python_version(),
        "HostOS": platform.platform(),
    }
    existing_rows: list[dict[str, str]] = []
    if path.is_file():
        with path.open("r", encoding="utf-8-sig", newline="") as source:
            existing_rows = list(csv.DictReader(source))
    existing_rows.append(row)
    with path.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(existing_rows)


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    library_root = resolve_library_root(config_path, config)
    scan_timestamp = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    pipeline_version = config["pipeline_version"]
    config_sha256 = sha256_for(config_path)

    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    manifest_root = catalog_root / "manifests"
    reports_root = catalog_root / "reports"
    for directory in (manifest_root, reports_root, database_root):
        directory.mkdir(parents=True, exist_ok=True)

    json_path = manifest_root / "raw_files.json"
    csv_path = manifest_root / "raw_files.csv"
    previous_payload, previous_records = load_previous_manifest(json_path)
    records, hashed_files, reused_hashes = collect_records(
        config,
        library_root,
        scan_timestamp,
        previous_records,
        args.force,
    )
    fingerprint = source_fingerprint(records)
    unchanged = (
        not args.force
        and hashed_files == 0
        and len(previous_records) == len(records)
        and previous_payload.get("pipelineVersion") == pipeline_version
        and previous_payload.get("configSHA256") == config_sha256
        and previous_payload.get("sourceFingerprint") == fingerprint
        and (reports_root / "phase_1_status.md").is_file()
    )
    if unchanged:
        manifest_sha256 = sha256_for(json_path)
        append_run_ledger(
            database_root / "pipeline_runs.csv",
            records,
            scan_timestamp,
            "SKIPPED_UNCHANGED",
            manifest_sha256,
            config_sha256,
            pipeline_version,
        )
        print(f"Inventory unchanged: {len(records)} files; existing PASS outputs kept")
        print(f"Manifest SHA-256: {manifest_sha256}")
        return 0

    write_json(
        json_path,
        records,
        scan_timestamp,
        pipeline_version,
        config_sha256,
        fingerprint,
    )
    write_csv(csv_path, records)
    write_status(
        reports_root / "phase_1_status.md",
        records,
        scan_timestamp,
        config.get("sources", []),
        hashed_files,
        reused_hashes,
    )
    manifest_sha256 = sha256_for(json_path)
    append_run_ledger(
        database_root / "pipeline_runs.csv",
        records,
        scan_timestamp,
        "PASS",
        manifest_sha256,
        config_sha256,
        pipeline_version,
    )

    print(f"Inventory complete: {len(records)} files")
    print(f"JSON: {json_path}")
    print(f"CSV: {csv_path}")
    print(f"Manifest SHA-256: {manifest_sha256}")
    print(f"SHA-256 calculated: {hashed_files}; reused: {reused_hashes}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
