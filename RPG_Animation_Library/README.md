# RPG Animation Reference Library

Internal, non-commercial prototype pipeline for cataloging and later retargeting FromSoftware-compatible animation references into Project S.

The authoritative execution specification is stored as `CODEX_FromSoftware_Animation_Pipeline.md`. The first repository milestone intentionally implements Phase 0 and Phase 1 only.

Raw data is not copied into this directory. `config/pipeline.json` maps the logical immutable `01_RAW` sources to the existing `../Elden` folders and archives. Inventory operations only open source files for reading.

Run the inventory from the repository root:

```powershell
python RPG_Animation_Library/scripts/inventory_sources.py --config RPG_Animation_Library/config/pipeline.json
```

Unchanged records reuse their previous SHA-256 after path, size, and modification-time validation. Add `--force` to recalculate every hash.

Generated inventory:

- `03_CATALOG/manifests/raw_files.csv`
- `03_CATALOG/manifests/raw_files.json`
- `03_CATALOG/reports/phase_1_status.md`
- `Database/pipeline_runs.csv`

Environment and installed tool versions are documented in `03_CATALOG/reports/environment.md` and `00_Tools/README.md`.

Do not rename, overwrite, unpack, or convert files inside the mapped RAW sources.
