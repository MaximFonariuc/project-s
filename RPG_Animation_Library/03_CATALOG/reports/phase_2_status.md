# Phase 2 controlled unpack

Run: `0ae30e0667084c3e805ce346127b108a`
Timestamp: `2026-08-14T15:05:47.175213Z`

Result: **PASS**.

Validated outputs: **2,050 files**, **141,580,050 bytes**.

| Job | Status | Output files | Output bytes | Duration (s) |
|---|---:|---:|---:|---:|
| `eldenvins-c0000-character` | SKIPPED_UNCHANGED | 5 | 209,015 | 0.004 |
| `eldenvins-c0000-events` | SKIPPED_UNCHANGED | 783 | 112,419,846 | 0.404 |
| `eldenvins-c0000-a00-hi` | SKIPPED_UNCHANGED | 1,262 | 28,951,189 | 0.545 |

## Safety and provenance

- RAW inputs were opened read-only, hashed, and checked against the Phase 1 manifest.
- WitchyBND processed copies inside `02_UNPACKED`; source files were not passed to the tool.
- PASS receipts contain hashes for every generated output file.
- Human-readable log: `logs/unpack_20260814T150547Z_0ae30e06.log`.
- Per-operation ledger: `Database/unpack_operations.csv`.

Phase 3 indexing is not part of this run.
