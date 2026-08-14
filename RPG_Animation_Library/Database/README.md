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
