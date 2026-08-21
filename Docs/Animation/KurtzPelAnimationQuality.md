# KurtzPel animation quality report

This deterministic editor audit samples every imported clip on the shared reference rig. It detects structural/timing/loop/contact issues; semantic combat suitability still requires visual review in the preview window.

- Total clips: `437`
- Clips with authored root/motion curves: `0`
- Clips flagged for review: `18`

## Categories

| Category | Clips | Flagged |
| --- | ---: | ---: |
| Airborne | 86 | 0 |
| Combat | 33 | 1 |
| Dodge | 12 | 0 |
| Idle | 28 | 0 |
| Locomotion | 192 | 15 |
| Other | 14 | 2 |
| Reaction | 10 | 0 |
| StartStop | 62 | 0 |

## Review flags

- `FootPhaseReview`: 15
- `LongClip`: 2
- `OneFrame`: 1

Full row-level measurements are stored in `KurtzPelAnimationQuality.csv`.
