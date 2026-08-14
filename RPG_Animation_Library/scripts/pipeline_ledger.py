#!/usr/bin/env python3
"""Shared append-only ledger helpers for animation pipeline batch runs."""

from __future__ import annotations

import csv
import hashlib
from pathlib import Path
from uuid import uuid4


PIPELINE_RUN_FIELDS = (
    "RunID",
    "Timestamp",
    "Phase",
    "Tool",
    "ToolVersion",
    "InputCount",
    "SuccessCount",
    "FailureCount",
    "ConfigHash",
    "PipelineVersion",
    "LogPath",
    "Notes",
)


def new_run_id() -> str:
    return uuid4().hex


def _legacy_run_id(timestamp: str, phase: str) -> str:
    value = f"legacy\0{timestamp}\0{phase}".encode("utf-8")
    return f"legacy-{hashlib.sha256(value).hexdigest()[:24]}"


def _normalize_row(row: dict[str, str]) -> dict[str, str]:
    if "RunID" in row:
        return {field: row.get(field, "") for field in PIPELINE_RUN_FIELDS}

    timestamp = row.get("RunTimestamp", "")
    phase = row.get("Phase", "")
    status = row.get("Status", "")
    record_count = row.get("RecordCount", "0")
    successful = record_count if status in {"PASS", "SKIPPED_UNCHANGED"} else "0"
    notes = "; ".join(
        part
        for part in (
            f"legacy_status={status}" if status else "",
            f"manifest_sha256={row.get('ManifestSHA256', '')}" if row.get("ManifestSHA256") else "",
            f"host_os={row.get('HostOS', '')}" if row.get("HostOS") else "",
        )
        if part
    )
    return {
        "RunID": _legacy_run_id(timestamp, phase),
        "Timestamp": timestamp,
        "Phase": phase,
        "Tool": "Python",
        "ToolVersion": row.get("Python", ""),
        "InputCount": record_count,
        "SuccessCount": successful,
        "FailureCount": "0" if successful != "0" else record_count,
        "ConfigHash": row.get("ConfigSHA256", ""),
        "PipelineVersion": row.get("PipelineVersion", ""),
        "LogPath": "",
        "Notes": notes,
    }


def append_pipeline_run(path: Path, row: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    existing_rows: list[dict[str, str]] = []
    if path.is_file():
        with path.open("r", encoding="utf-8-sig", newline="") as source:
            existing_rows = [_normalize_row(item) for item in csv.DictReader(source)]

    normalized = {field: str(row.get(field, "")) for field in PIPELINE_RUN_FIELDS}
    existing_rows.append(normalized)
    with path.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=PIPELINE_RUN_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(existing_rows)
