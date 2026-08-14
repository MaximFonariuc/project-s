# RPG Animation Reference Library

Internal, non-commercial prototype pipeline for cataloging and later retargeting FromSoftware-compatible animation references into Project S.

The authoritative execution specification is stored as `CODEX_FromSoftware_Animation_Pipeline.md`. The current bounded pilot implements Phase 0 through Phase 9; batch processing remains disabled.

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

Run the Phase 5 HKX-to-Blender hard gate:

```powershell
python RPG_Animation_Library/scripts/run_phase5_conversion.py --config RPG_Animation_Library/config/pipeline.json
```

Phase 5 converts only the five configured `c0000/a00_hi` pilot clips into native `.blend` files below ignored `04_CONVERTED/phase_5`. Each artifact is reopened in a fresh Blender process and checked against a deterministic per-frame motion fingerprint. One root-motion clip is also imported twice to verify repeatability.

The PASS result approves only the tested standard Elden Ring player HKX variant. It does not approve unrecognized HKX containers, other skeleton families, bulk conversion, retargeting, FBX export, or Unity import.

Versioned Phase 5 evidence:

- `03_CATALOG/reports/phase_5_status.md`;
- `Database/conversion_operations.csv`;
- `Database/phase_5_state.json`;
- `ConversionStatus=PilotPassed` for the selected rows in `Database/animations.csv`.

The Phase 6-9 pilot then profiles the source and target skeletons, retargets five clips, exports animation-only FBX files and imports them through the Unity Editor gate:

```powershell
python RPG_Animation_Library/scripts/build_phase6_profiles.py --config RPG_Animation_Library/config/pipeline.json
python RPG_Animation_Library/scripts/run_phase7_retarget.py --config RPG_Animation_Library/config/pipeline.json
python RPG_Animation_Library/scripts/run_phase8_export.py --config RPG_Animation_Library/config/pipeline.json
python RPG_Animation_Library/scripts/stage_phase9_unity.py --config RPG_Animation_Library/config/pipeline.json
```

After importing the five staged FBX files in Unity, run `Tools/Project S/Animation Library/Validate Phase 9 Pilot`, then finalize the versioned receipt:

```powershell
python RPG_Animation_Library/scripts/finalize_phase9_unity.py --config RPG_Animation_Library/config/pipeline.json
```

Phase 9 evidence is stored in `Database/unity_import_operations.csv`, `Database/phase_9_state.json` and `03_CATALOG/reports/phase_9_status.md`. A PASS proves the bounded technical import and does not promote uncertain semantic labels or approve bulk conversion.

Environment and installed tool versions are documented in `03_CATALOG/reports/environment.md` and `00_Tools/README.md`.

Do not rename, overwrite, unpack, or convert files inside the mapped RAW sources.
