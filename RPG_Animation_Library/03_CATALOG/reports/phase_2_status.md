# Phase 2 controlled unpack

Run: `b13d3ff481714f44a2441d66d94c8246`
Timestamp: `2026-08-14T15:20:00.087169Z`

Result: **PASS**.

Validated outputs: **2,050 files**, **141,580,050 bytes**.

| Job | Status | Output files | Output bytes | Duration (s) |
|---|---:|---:|---:|---:|
| `eldenvins-c0000-character` | SKIPPED_UNCHANGED | 5 | 209,015 | 0.003 |
| `eldenvins-c0000-events` | SKIPPED_UNCHANGED | 783 | 112,419,846 | 0.370 |
| `eldenvins-c0000-a00-hi` | SKIPPED_UNCHANGED | 1,262 | 28,951,189 | 0.502 |

## Safety and provenance

- RAW inputs were opened read-only, hashed, and checked against the Phase 1 manifest.
- WitchyBND processed copies inside `02_UNPACKED`; source files were not passed to the tool.
- PASS receipts contain hashes for every generated output file.
- Human-readable log: `logs/unpack_20260814T152000Z_b13d3ff4.log`.
- Per-operation ledger: `Database/unpack_operations.csv`.

Phase 3 indexing is not part of this run.
