# Phase 4 semantic preview pilot

Timestamp: `2026-08-14T15:48:36.094728Z`

Result: **PASS**.

## Scope and evidence policy

Only six `c0000/a00_hi` clips were sampled. Semantic labels are based on a Blender/Soulstruct skeleton pose contact sheet plus the associated raw TAE entry; they are not extrapolated to the full library.

`Verified` is used only when visible motion and an explicit named TAE flag agree. Pose-only interpretations remain `Probable`; ambiguous clips remain `Unknown`.

DSAnimStudio remains the preferred interactive viewer. Its normal startup requires a game executable/project selection, so it was not used as a headless batch renderer. All non-Verified labels still require an interactive DSAnimStudio review before gameplay use.

## Pilot results

| Animation | Classification | Confidence | Events | QA |
|---|---|---:|---:|---|
| `a000_010000` | Unknown / Unknown | Unknown | 3 | NeedsReview |
| `a000_019270` | Dodge / ForwardRoll | Verified | 48 | Previewed |
| `a000_020000` | Locomotion / ForwardStep | Probable | 20 | NeedsReview |
| `a000_040590` | Attack / ForwardLeapingAttack | Probable | 46 | Previewed |
| `a000_041150` | Attack / RisingStrike | Probable | 29 | Previewed |
| `a000_080000` | Unknown / Unknown | Unknown | 43 | NeedsReview |

## Counts

- Pilot clips: **6**
- Verified: **1**
- Probable: **3**
- Unknown: **2**
- Raw TAE events preserved: **189**

## Rejected candidates

These HKX entries looked like animation clips by filename but could not be parsed as animations by the current Soulstruct/Havok bridge. They remain in the canonical identity catalog and must not be silently treated as conversion-ready clips.

| Animation | Import diagnostic |
|---|---|
| `a000_012100` | Soulstruct rejected the HKX because the Havok type hkMemoryResourceContainer is missing from the active type registry. |
| `a000_050470` | Soulstruct rejected the HKX because the Havok type hkxScene is missing from the active type registry. |

## Outputs

- `Database/pilot_clips.csv` — pilot-level semantic and pose metrics.
- `Database/tae_events.csv` — lossless raw event identity/timing/parameters plus normalized boundary classes.
- `Database/animations.csv` — curated fields updated only for the six selected canonical rows.
- `03_CATALOG/previews/phase_4/*.png` — sampled skeleton pose contact sheets.
- `Database/phase_4_state.json` — hashes of versioned outputs.
- `logs/semantic_preview_20260814T154836Z_53d94ed1.log` — execution log (ignored by Git).

## Known limitations

- Contact sheets show sampled skeleton poses, not final mesh deformation, weapon alignment, additive-layer composition, or camera-relative intent.
- On this host Blender 5.2 writes valid pose metadata and then exits with an Intel `igxelpicd64.dll` access violation. The pipeline accepts that non-zero exit only when the expected JSON was already written and can be parsed.
- `a000_010000` produces a collapsed full-body projection and stays unclassified.
- `a000_080000` is visibly animated but has no sufficiently reliable action identity and stays unclassified.
- `a000_020000` looks locomotion-like, but its TAE contains a `BulletBehavior` event; it remains only Probable.
- Phase 3 currently classifies HKX primarily by identity/path conventions. The rejected entries demonstrate that Phase 5 needs a parseability gate before conversion status changes.

## Next safe action

Review the Probable/Unknown pilot clips interactively in DSAnimStudio, then use only an approved subset for the first HKX-to-Blender conversion gate. Do not batch-convert the library yet.
