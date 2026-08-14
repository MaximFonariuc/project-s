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

Run the configured Phase 2 pilot unpack after Phase 1 passes:

```powershell
python RPG_Animation_Library/scripts/unpack_sources.py --config RPG_Animation_Library/config/pipeline.json
```

The pilot is limited to the three configured `c0000` jobs. Use repeated `--job <id>` arguments for a subset. `--force` replaces only the selected generated job directories after verifying that every resolved target remains below `02_UNPACKED`.

Generated unpack data and logs remain ignored. Versioned provenance is stored in `Database/pipeline_runs.csv`, `Database/unpack_operations.csv`, and `03_CATALOG/reports/phase_2_status.md`.

Build or update the Phase 3 canonical identity catalog:

```powershell
python RPG_Animation_Library/scripts/index_animations.py --config RPG_Animation_Library/config/pipeline.json
```

Phase 3 indexes loose RAW HKX/TAE/container identities plus hash-verified Phase 2 outputs. It creates stable `LibraryID` values and preserves curated metadata columns on rebuild. Semantic classification remains `Unknown` until Phase 4 preview and identification.

Build the bounded Phase 4 semantic-preview pilot:

```powershell
python RPG_Animation_Library/scripts/build_phase4_pilot.py --config RPG_Animation_Library/config/pipeline.json
```

Phase 4 processes only the six explicitly configured `c0000/a00_hi` clips. Blender and Soulstruct generate sampled skeleton pose contact sheets; WitchyBND serializes the associated `a00.tae` entries. Raw event IDs, timing, enum context, and parameters remain available in `Database/tae_events.csv`, while normalized start/end classes provide a narrow searchable view.

The confidence policy is deliberately conservative: a pose-only interpretation is `Probable`, ambiguous motion stays `Unknown`, and `Verified` requires agreement between the visible motion and explicit TAE evidence. The phase does not approve bulk conversion.

Generated Phase 4 outputs:

- `03_CATALOG/previews/phase_4/*.png`;
- `03_CATALOG/reports/phase_4_status.md`;
- `Database/pilot_clips.csv`;
- `Database/tae_events.csv`;
- `Database/phase_4_state.json`;
- curated fields for the selected rows in `Database/animations.csv`.

Environment and installed tool versions are documented in `03_CATALOG/reports/environment.md` and `00_Tools/README.md`.

Do not rename, overwrite, unpack, or convert files inside the mapped RAW sources.
