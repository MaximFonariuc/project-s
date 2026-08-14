# Phase 1 source inventory

Scan timestamp: `2026-08-14T15:06:39.701186Z`

Result: **PASS**. Every file in every configured RAW root is represented by a read-only SHA-256 inventory in deterministic source/path order. This run reused hashes whose size and modification time still match; use `--force` for a complete re-hash.

## Totals

- Files: **12,047**
- Bytes: **9,792,760,172**
- Unique SHA-256 values: **11,989**
- Duplicate hash groups: **32**
- SHA-256 calculated this run: **0**
- SHA-256 reused from unchanged records: **12,047**

## Counts by source and type

| Source | Extension | Files | Bytes |
|---|---:|---:|---:|
| EldenVins | `.dcx` | 425 | 1,473,311,576 |
|  | `.partsbnd.dcx` | 236 | 2,115,193,152 |
|  | `.h` | 148 | 2,163,567 |
|  | `.anibnd.dcx` | 131 | 477,109,095 |
|  | `.hpp` | 110 | 1,086,110 |
|  | `.log` | 104 | 1,577,208 |
|  | `.gfx` | 100 | 36,067,986 |
|  | `.behbnd.dcx` | 57 | 7,197,029 |
|  | `.chrbnd.dcx` | 42 | 193,375,515 |
|  | `.hks` | 24 | 2,060,529 |
|  | `.dll` | 17 | 5,017,600 |
|  | `.bat` | 13 | 5,395 |
|  | `.txt` | 13 | 186,811 |
|  | `.bnk` | 12 | 120,783,554 |
|  | `.inl` | 12 | 183,982 |
|  | `.toml` | 12 | 38,454 |
|  | `.ini` | 7 | 13,187 |
|  | `.lua` | 7 | 237,927 |
|  | `.png` | 7 | 301,277 |
|  | `.exe` | 5 | 51,918,469 |
|  | `.cmake` | 4 | 8,741 |
|  | `.ccm` | 3 | 877,312 |
|  | `.md` | 3 | 23,705 |
|  | `.tpf` | 3 | 12,461,892 |
|  | `.geomhkxbnd.dcx` | 2 | 19,600 |
|  | `.ico` | 2 | 379,777 |
|  | `.lib` | 2 | 31,738 |
|  | `.wem` | 2 | 47,672,360 |
|  | `.bin` | 1 | 2,268,432 |
|  | `.geombnd.dcx` | 1 | 149,136 |
|  | `.lnk` | 1 | 1,576 |
|  | `.ppo` | 1 | 752 |
|  | `.prev` | 1 | 6,768 |
|  | `.pwt` | 1 | 167 |
|  | `.textlist` | 1 | 108 |
|  | `.tpfbhd` | 1 | 372,824 |
|  | `.vpo` | 1 | 1,276 |
|  | `.zip` | 1 | 4,458,363,284 |
| PlayerRepo | `.hkx` | 10,531 | 508,285,952 |
|  | `.ps1` | 1 | 1,146 |
|  | `.tae` | 1 | 25,328 |
|  | `.zip` | 1 | 273,979,875 |

## Missing configured sources

- EldenRing_Vanilla

## Outputs

- `03_CATALOG/manifests/raw_files.csv`
- `03_CATALOG/manifests/raw_files.json`

Duplicate files can be queried by grouping the JSON records on `SHA256`.
