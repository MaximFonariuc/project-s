# Animation database contract

`animations.csv` is the canonical Phase 3 identity catalog. One row represents one discovered animation clip, TAE timeline, animation container, or skeleton resource.

## Stable identity

`LibraryID` is deterministic:

```text
RPGREF_<first 24 uppercase hex characters of SHA-256(SourceStage + NUL + Source + NUL + OriginalPath)>
```

RAW paths are immutable, so an unchanged source keeps the same `LibraryID` across machines and pipeline runs. `SourceHash` identifies file content independently from path identity and may intentionally repeat for duplicated source files.

Do not manually change these provenance fields:

- `LibraryID`, `RecordKind`, `SourceStage`, `Source`, `SourcePackageVersion`;
- `OriginalPath`, `SourceHash`, `SizeBytes`, `ProvenanceID`;
- `AnimationID`, `OriginalFileName`.

They are rebuilt from the Phase 1 manifest or a verified Phase 2 receipt.

## Curated metadata

`index_animations.py` preserves existing values in semantic, preview, conversion, retarget, Unity import, QA, TAE-link, and notes fields. These fields may be enriched in Phase 4 and later without being reset by an index rebuild.

Unverified semantics must remain `Unknown`. A name inferred from a file or directory is not `Verified` until a reliable source mapping or preview confirms it.

`animation_index_state.json` records the phase configuration hash, input fingerprint, record count, and final CSV SHA-256 used for idempotent reruns.

## Phase 4 pilot evidence

`pilot_clips.csv` contains the small, explicitly reviewed Phase 4 subset. It stores semantic confidence, sampled duration and root translation, TAE linkage, preview location, and QA notes without redefining canonical identity.

`tae_events.csv` is event evidence rather than gameplay configuration. It preserves:

- raw TAE event ID and template name;
- exact source start/end strings and calculated duration;
- group index, enum label context, and all raw parameters as compact JSON;
- separate normalized start/end classes for search and later mapping.

Never discard raw fields after normalization. An event that cannot be mapped safely must remain `Unknown`.

`phase_4_state.json` hashes every versioned Phase 4 output. Because Phase 4 enriches `animations.csv`, the Phase 3 output hash becomes stale until `index_animations.py` is run again; the indexer preserves curated values while refreshing its own state.

## Phase 5 conversion evidence

`conversion_operations.csv` is an append-only audit ledger for HKX-to-Blender pilot attempts. Each row records source identity, generated `.blend` path/hash/size, motion fingerprint, source timing and root-motion metrics, and the result of every hard-gate dimension.

`phase_5_state.json` records the exact pilot approval scope and hashes both the versioned evidence and ignored local conversion artifacts. Phase state hashes are historical run evidence: later phases may legitimately enrich the shared `animations.csv`, so use the newest phase state plus the current Phase 3 index state when validating the active catalog.

`PilotPassed` in `ConversionStatus` means only that the selected source HKX survived the bounded Blender gate. It does not imply semantic approval, successful retargeting, FBX export, Unity import, or compatibility with another skeleton family.

## Phase 6 skeleton evidence

`skeleton_profile_operations.csv` records the measured source and target armatures used by the pilot retarget boundary. The versioned skeleton profiles identify semantic bones, root-motion ownership, coordinate space, scale, and Unity rig type.

`ER_Player_to_ProjectS_CLazy.json` is a skeleton-family mapping. It is intentionally shared by every pilot clip and excludes service, control, cloth, SFX and weapon bones that have no target equivalent. A clip-specific map would be a pipeline defect, not an extension point.

## Phase 7 retarget evidence

`retarget_operations.csv` is the append-only pilot retarget ledger. It records the source/target artifacts and profiles, mapped-bone count, measured scale, target root motion, pose bounds, motion fingerprint, clean-reopen result and repeatability result.

`PilotPassed` in `RetargetStatus` means that the selected action was baked onto `ProjectS_CLazy_Player` and passed the mechanical gates. Contact sheets are retained for human pose review. They do not promote `Probable` or `Unknown` semantic labels to `Verified`.

## Phase 8 FBX evidence

`fbx_export_operations.csv` records each animation-only FBX, its adjacent `.animlib.json` contract, hashes, pinned pipeline/profile identifiers and the clean Blender reimport result.

The adjacent contract is the authoritative hand-off to Unity. It carries semantic metadata without encoding gameplay events into the animation file and tells the editor importer whether the clip is in-place or preserves scaled root motion.
