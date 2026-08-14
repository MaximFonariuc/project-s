#!/usr/bin/env python3
"""Run the configured pilot-only, read-only RAW unpack jobs through WitchyBND."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import logging
import shutil
import subprocess
import sys
import time
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

from pipeline_ledger import append_pipeline_run, new_run_id


OPERATION_FIELDS = (
    "OperationID",
    "RunID",
    "Timestamp",
    "JobID",
    "Source",
    "InputPath",
    "InputSHA256",
    "Tool",
    "ToolVersion",
    "Command",
    "OutputPath",
    "Status",
    "DurationSeconds",
    "OutputFileCount",
    "OutputBytes",
    "OutputFingerprint",
    "Error",
)
RECEIPT_NAME = ".pipeline-unpack-receipt.json"


@dataclass
class OperationResult:
    operation_id: str
    run_id: str
    timestamp: str
    job_id: str
    source: str
    input_path: str
    input_sha256: str
    tool: str
    tool_version: str
    command: str
    output_path: str
    status: str
    duration_seconds: float
    output_file_count: int
    output_bytes: int
    output_fingerprint: str
    error: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument(
        "--job",
        action="append",
        dest="job_ids",
        help="Run only the selected configured job ID. May be specified more than once.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Replace the selected generated job outputs even when a PASS receipt exists.",
    )
    return parser.parse_args()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_for(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def unpack_phase_config_hash(config: dict) -> str:
    payload = {
        "phaseVersion": config["phase_versions"]["unpack"],
        "unpackedPath": config["paths"]["unpacked"],
        "toolPath": config["tools"]["witchybnd"],
        "toolVersion": config["tool_versions"]["witchybnd"],
        "unpack": config["unpack"],
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def resolve_library_root(config_path: Path, config: dict) -> Path:
    configured = Path(config.get("project_root", "."))
    return (config_path.parent.parent / configured).resolve()


def resolve_from_library(library_root: Path, configured_path: str) -> Path:
    path = Path(configured_path)
    return path.resolve() if path.is_absolute() else (library_root / path).resolve()


def ensure_generated_job_root(path: Path, unpacked_root: Path) -> Path:
    resolved = path.resolve()
    root = unpacked_root.resolve()
    if resolved == root or not resolved.is_relative_to(root):
        raise ValueError(f"Generated job path escapes 02_UNPACKED: {resolved}")
    return resolved


def compound_extension(path: Path, known_extensions: list[str]) -> str:
    lower_name = path.name.casefold()
    for extension in sorted(known_extensions, key=len, reverse=True):
        if lower_name.endswith(extension.casefold()):
            return extension.casefold()
    return path.suffix.casefold()


def load_raw_manifest(path: Path) -> dict[tuple[str, str], dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    return {
        (record["Source"], record["RelativePath"]): record
        for record in payload.get("records", [])
    }


def inventory_outputs(job_root: Path, source_copy: Path) -> tuple[list[dict], str]:
    outputs: list[dict] = []
    for path in sorted(job_root.rglob("*"), key=lambda item: item.relative_to(job_root).as_posix().casefold()):
        if not path.is_file() or path == source_copy or path.name == RECEIPT_NAME:
            continue
        outputs.append(
            {
                "relativePath": path.relative_to(job_root).as_posix(),
                "sizeBytes": path.stat().st_size,
                "sha256": sha256_for(path),
            }
        )

    digest = hashlib.sha256()
    for output in outputs:
        digest.update(output["relativePath"].encode("utf-8"))
        digest.update(b"\0")
        digest.update(output["sha256"].encode("ascii"))
        digest.update(b"\n")
    return outputs, digest.hexdigest()


def validate_expected_outputs(job: dict, outputs: list[dict]) -> None:
    minimum = int(job.get("minimum_output_files", 1))
    if len(outputs) < minimum:
        raise RuntimeError(f"Expected at least {minimum} output files, found {len(outputs)}")

    output_extensions = {Path(item["relativePath"]).suffix.casefold() for item in outputs}
    missing = [
        extension
        for extension in job.get("required_extensions", [])
        if extension.casefold() not in output_extensions
    ]
    if missing:
        raise RuntimeError(f"Required output extensions are missing: {', '.join(missing)}")


def load_receipt(path: Path) -> dict | None:
    if not path.is_file():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def validate_cached_receipt(
    receipt: dict | None,
    job_root: Path,
    source_copy: Path,
    input_sha256: str,
    phase_config_sha256: str,
    phase_version: str,
    tool_version: str,
    tool_sha256: str,
) -> tuple[bool, list[dict], str]:
    if receipt is None or receipt.get("status") != "PASS":
        return False, [], ""
    expected = {
        "inputSHA256": input_sha256,
        "toolVersion": tool_version,
        "toolExecutableSHA256": tool_sha256,
    }
    if any(receipt.get(key) != value for key, value in expected.items()):
        return False, [], ""
    if receipt.get("phaseConfigSHA256") is not None and (
        receipt.get("phaseConfigSHA256") != phase_config_sha256
        or receipt.get("phaseVersion") != phase_version
    ):
        return False, [], ""

    outputs, fingerprint = inventory_outputs(job_root, source_copy)
    if outputs != receipt.get("outputs") or fingerprint != receipt.get("outputFingerprint"):
        return False, outputs, fingerprint
    return True, outputs, fingerprint


def write_receipt(
    path: Path,
    job: dict,
    timestamp: str,
    input_sha256: str,
    phase_config_sha256: str,
    phase_version: str,
    pipeline_version: str,
    tool_version: str,
    tool_sha256: str,
    command: str,
    outputs: list[dict],
    fingerprint: str,
) -> None:
    payload = {
        "schemaVersion": 1,
        "status": "PASS",
        "timestamp": timestamp,
        "jobID": job["id"],
        "source": job["source"],
        "catalogRelativePath": job["catalog_relative_path"],
        "inputSHA256": input_sha256,
        "phaseConfigSHA256": phase_config_sha256,
        "phaseVersion": phase_version,
        "pipelineVersion": pipeline_version,
        "tool": "WitchyBND",
        "toolVersion": tool_version,
        "toolExecutableSHA256": tool_sha256,
        "command": command,
        "outputFingerprint": fingerprint,
        "outputs": outputs,
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run_job(
    job: dict,
    run_id: str,
    config: dict,
    phase_config_sha256: str,
    library_root: Path,
    unpacked_root: Path,
    raw_manifest: dict[tuple[str, str], dict],
    tool_path: Path,
    tool_version: str,
    tool_sha256: str,
    force: bool,
    logger: logging.Logger,
) -> OperationResult:
    started = time.perf_counter()
    timestamp = utc_now()
    input_path = resolve_from_library(library_root, job["input"])
    job_root = ensure_generated_job_root(
        unpacked_root / job["output_relative_path"],
        unpacked_root,
    )
    source_copy = job_root / input_path.name
    receipt_path = job_root / RECEIPT_NAME
    command = subprocess.list2cmdline([str(tool_path), "-s", str(source_copy)])
    input_sha256 = ""

    try:
        known_extensions = config["unpack"]["known_container_extensions"]
        actual_extension = compound_extension(input_path, known_extensions)
        if actual_extension not in {item.casefold() for item in known_extensions}:
            raise ValueError(f"Unsupported container extension: {input_path.name}")
        if not input_path.is_file():
            raise FileNotFoundError(input_path)

        before = input_path.stat()
        input_sha256 = sha256_for(input_path)
        after = input_path.stat()
        if before.st_size != after.st_size or before.st_mtime_ns != after.st_mtime_ns:
            raise RuntimeError(f"RAW source changed while hashing: {input_path}")

        catalog_key = (job["source"], job["catalog_relative_path"])
        catalog_record = raw_manifest.get(catalog_key)
        if catalog_record is None:
            raise RuntimeError(f"Input is absent from the Phase 1 RAW manifest: {catalog_key}")
        if catalog_record["SHA256"] != input_sha256:
            raise RuntimeError(
                "Input SHA-256 differs from the Phase 1 RAW manifest; rerun inventory before unpacking"
            )

        receipt = load_receipt(receipt_path)
        cache_valid, outputs, fingerprint = validate_cached_receipt(
            receipt,
            job_root,
            source_copy,
            input_sha256,
            phase_config_sha256,
            config["phase_versions"]["unpack"],
            tool_version,
            tool_sha256,
        )
        if cache_valid and not force:
            validate_expected_outputs(job, outputs)
            if receipt.get("phaseConfigSHA256") is None:
                write_receipt(
                    receipt_path,
                    job,
                    timestamp,
                    input_sha256,
                    phase_config_sha256,
                    config["phase_versions"]["unpack"],
                    config["pipeline_version"],
                    tool_version,
                    tool_sha256,
                    command,
                    outputs,
                    fingerprint,
                )
                logger.info("MIGRATE %s: receipt upgraded to phase-scoped provenance", job["id"])
            duration = time.perf_counter() - started
            logger.info("SKIP %s: unchanged PASS receipt and outputs", job["id"])
            return OperationResult(
                uuid4().hex,
                run_id,
                timestamp,
                job["id"],
                job["source"],
                str(input_path),
                input_sha256,
                "WitchyBND",
                tool_version,
                command,
                str(job_root),
                "SKIPPED_UNCHANGED",
                duration,
                len(outputs),
                sum(item["sizeBytes"] for item in outputs),
                fingerprint,
                "",
            )

        if job_root.exists():
            if not force:
                raise RuntimeError(
                    f"Generated output is not a valid PASS cache: {job_root}. Use --force to replace it."
                )
            checked_root = ensure_generated_job_root(job_root, unpacked_root)
            shutil.rmtree(checked_root)

        job_root.mkdir(parents=True, exist_ok=False)
        shutil.copy2(input_path, source_copy)
        if sha256_for(source_copy) != input_sha256:
            raise RuntimeError(f"Working copy hash mismatch: {source_copy}")

        logger.info("START %s", job["id"])
        logger.info("Input: %s", input_path)
        logger.info("Input SHA-256: %s", input_sha256)
        logger.info("Command: %s", command)
        completed = subprocess.run(
            [str(tool_path), "-s", str(source_copy)],
            cwd=job_root,
            capture_output=True,
            text=True,
            errors="replace",
            timeout=int(config["unpack"].get("timeout_seconds", 300)),
            check=False,
        )
        if completed.stdout.strip():
            logger.info("WitchyBND stdout:\n%s", completed.stdout.rstrip())
        if completed.stderr.strip():
            logger.warning("WitchyBND stderr:\n%s", completed.stderr.rstrip())
        if completed.returncode != 0:
            raise RuntimeError(f"WitchyBND exited with code {completed.returncode}")

        outputs, fingerprint = inventory_outputs(job_root, source_copy)
        validate_expected_outputs(job, outputs)
        write_receipt(
            receipt_path,
            job,
            timestamp,
            input_sha256,
            phase_config_sha256,
            config["phase_versions"]["unpack"],
            config["pipeline_version"],
            tool_version,
            tool_sha256,
            command,
            outputs,
            fingerprint,
        )
        if input_path.stat().st_size != before.st_size or input_path.stat().st_mtime_ns != before.st_mtime_ns:
            raise RuntimeError(f"RAW source changed during unpack: {input_path}")

        duration = time.perf_counter() - started
        output_bytes = sum(item["sizeBytes"] for item in outputs)
        logger.info(
            "PASS %s: %d files, %d bytes, %.3f seconds",
            job["id"],
            len(outputs),
            output_bytes,
            duration,
        )
        return OperationResult(
            uuid4().hex,
            run_id,
            timestamp,
            job["id"],
            job["source"],
            str(input_path),
            input_sha256,
            "WitchyBND",
            tool_version,
            command,
            str(job_root),
            "PASS",
            duration,
            len(outputs),
            output_bytes,
            fingerprint,
            "",
        )
    except Exception as exception:
        duration = time.perf_counter() - started
        logger.exception("FAILED %s", job["id"])
        return OperationResult(
            uuid4().hex,
            run_id,
            timestamp,
            job["id"],
            job.get("source", ""),
            str(input_path),
            input_sha256,
            "WitchyBND",
            tool_version,
            command,
            str(job_root),
            "FAILED",
            duration,
            0,
            0,
            "",
            f"{type(exception).__name__}: {exception}",
        )


def append_operation_rows(path: Path, results: list[OperationResult]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    existing: list[dict[str, str]] = []
    if path.is_file():
        with path.open("r", encoding="utf-8-sig", newline="") as source:
            existing = list(csv.DictReader(source))

    rows = []
    for result in results:
        raw = asdict(result)
        rows.append(
            {
                "OperationID": raw["operation_id"],
                "RunID": raw["run_id"],
                "Timestamp": raw["timestamp"],
                "JobID": raw["job_id"],
                "Source": raw["source"],
                "InputPath": raw["input_path"],
                "InputSHA256": raw["input_sha256"],
                "Tool": raw["tool"],
                "ToolVersion": raw["tool_version"],
                "Command": raw["command"],
                "OutputPath": raw["output_path"],
                "Status": raw["status"],
                "DurationSeconds": f"{raw['duration_seconds']:.6f}",
                "OutputFileCount": str(raw["output_file_count"]),
                "OutputBytes": str(raw["output_bytes"]),
                "OutputFingerprint": raw["output_fingerprint"],
                "Error": raw["error"],
            }
        )

    with path.open("w", encoding="utf-8-sig", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=OPERATION_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(existing)
        writer.writerows(rows)


def write_status_report(
    path: Path,
    timestamp: str,
    run_id: str,
    results: list[OperationResult],
    log_relative_path: str,
) -> None:
    failures = [result for result in results if result.status == "FAILED"]
    total_files = sum(result.output_file_count for result in results)
    total_bytes = sum(result.output_bytes for result in results)
    lines = [
        "# Phase 2 controlled unpack",
        "",
        f"Run: `{run_id}`",
        f"Timestamp: `{timestamp}`",
        "",
        f"Result: **{'FAIL' if failures else 'PASS'}**.",
        "",
        f"Validated outputs: **{total_files:,} files**, **{total_bytes:,} bytes**.",
        "",
        "| Job | Status | Output files | Output bytes | Duration (s) |",
        "|---|---:|---:|---:|---:|",
    ]
    for result in results:
        lines.append(
            f"| `{result.job_id}` | {result.status} | {result.output_file_count:,} | "
            f"{result.output_bytes:,} | {result.duration_seconds:.3f} |"
        )
    lines.extend(
        [
            "",
            "## Safety and provenance",
            "",
            "- RAW inputs were opened read-only, hashed, and checked against the Phase 1 manifest.",
            "- WitchyBND processed copies inside `02_UNPACKED`; source files were not passed to the tool.",
            "- PASS receipts contain hashes for every generated output file.",
            f"- Human-readable log: `{log_relative_path}`.",
            "- Per-operation ledger: `Database/unpack_operations.csv`.",
            "",
            "Phase 3 indexing is not part of this run.",
            "",
        ]
    )
    if failures:
        lines.extend(["## Failures", ""])
        lines.extend(f"- `{result.job_id}`: {result.error}" for result in failures)
        lines.append("")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def select_jobs(config: dict, selected_ids: list[str] | None) -> list[dict]:
    jobs = config["unpack"].get("jobs", [])
    if selected_ids is None:
        return jobs
    requested = set(selected_ids)
    available = {job["id"] for job in jobs}
    unknown = sorted(requested - available)
    if unknown:
        raise ValueError(f"Unknown unpack job IDs: {', '.join(unknown)}")
    return [job for job in jobs if job["id"] in requested]


def main() -> int:
    args = parse_args()
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    if not config.get("unpack", {}).get("pilot_only", False):
        raise RuntimeError("Phase 2 requires unpack.pilot_only=true")

    library_root = resolve_library_root(config_path, config)
    unpacked_root = resolve_from_library(library_root, config["paths"]["unpacked"])
    catalog_root = resolve_from_library(library_root, config["paths"]["catalog"])
    database_root = resolve_from_library(library_root, config["paths"]["database"])
    logs_root = resolve_from_library(library_root, config["paths"]["logs"])
    for directory in (unpacked_root, database_root, logs_root):
        directory.mkdir(parents=True, exist_ok=True)

    jobs = select_jobs(config, args.job_ids)
    max_batch_size = int(config.get("conversion", {}).get("max_batch_size", 20))
    if not jobs:
        raise RuntimeError("No unpack jobs selected")
    if len(jobs) > max_batch_size:
        raise RuntimeError(f"Selected {len(jobs)} jobs; pilot maximum is {max_batch_size}")

    timestamp = utc_now()
    file_timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_id = new_run_id()
    log_path = logs_root / f"unpack_{file_timestamp}_{run_id[:8]}.log"
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)sZ %(levelname)s %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[logging.FileHandler(log_path, encoding="utf-8"), logging.StreamHandler(sys.stdout)],
    )
    logger = logging.getLogger("animation-pipeline.unpack")

    phase_config_sha256 = unpack_phase_config_hash(config)
    tool_path = resolve_from_library(library_root, config["tools"]["witchybnd"])
    if not tool_path.is_file():
        raise FileNotFoundError(tool_path)
    tool_version = config["tool_versions"]["witchybnd"]["version"]
    tool_sha256 = sha256_for(tool_path)
    expected_tool_sha256 = config["tool_versions"]["witchybnd"]["executable_sha256"]
    if tool_sha256.casefold() != expected_tool_sha256.casefold():
        raise RuntimeError(
            f"WitchyBND executable SHA-256 mismatch: expected {expected_tool_sha256}, got {tool_sha256}"
        )

    raw_manifest_path = catalog_root / "manifests" / "raw_files.json"
    raw_manifest = load_raw_manifest(raw_manifest_path)
    logger.info("Phase 2 run %s: %d pilot jobs", run_id, len(jobs))
    results = [
        run_job(
            job,
            run_id,
            config,
            phase_config_sha256,
            library_root,
            unpacked_root,
            raw_manifest,
            tool_path,
            tool_version,
            tool_sha256,
            args.force,
            logger,
        )
        for job in jobs
    ]

    append_operation_rows(database_root / "unpack_operations.csv", results)
    failures = sum(result.status == "FAILED" for result in results)
    passed = sum(result.status == "PASS" for result in results)
    skipped = sum(result.status == "SKIPPED_UNCHANGED" for result in results)
    log_relative_path = log_path.relative_to(library_root).as_posix()
    append_pipeline_run(
        database_root / "pipeline_runs.csv",
        {
            "RunID": run_id,
            "Timestamp": timestamp,
            "Phase": "Phase2ControlledUnpack",
            "Tool": "WitchyBND",
            "ToolVersion": tool_version,
            "InputCount": len(results),
            "SuccessCount": passed + skipped,
            "FailureCount": failures,
            "ConfigHash": phase_config_sha256,
            "PipelineVersion": config["pipeline_version"],
            "LogPath": log_relative_path,
            "Notes": f"passed={passed}; skipped={skipped}; failed={failures}",
        },
    )
    write_status_report(
        catalog_root / "reports" / "phase_2_status.md",
        timestamp,
        run_id,
        results,
        log_relative_path,
    )
    logger.info("Phase 2 complete: passed=%d skipped=%d failed=%d", passed, skipped, failures)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
