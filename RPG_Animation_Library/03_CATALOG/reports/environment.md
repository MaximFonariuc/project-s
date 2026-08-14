# Environment report — Phase 0

Generated for Project S on 2026-08-14.

| Item | Detected value |
|---|---|
| OS | Windows 64-bit |
| Python | CPython 3.12.8 (`python` and `py`) |
| Blender | `D:/Games/SteamGames/steamapps/common/Blender/blender.exe`, version 5.2.0 LTS |
| Unity project | `D:/Git/SoloProjects/project-s` |
| Unity | `C:/Program Files/Unity/Hub/Editor/6000.0.80f1/Editor/Unity.exe`, version 6000.0.80f1 |
| Unity MCP | Connected to `project-s@7635d723a88cab93` |
| WitchyBND | `../Elden/.pipeline/tools/WitchyBND-3.0.1.0/WitchyBND.exe`, version 3.0.1.0 |
| DSAnimStudio | `../Elden/.pipeline/tools/DSAnimStudio-4.9.9/DS ANIM STUDIO.exe`, version 4.9.9 |
| HKX Blender bridge | `../Elden/.pipeline/blender-user/scripts/addons/io_soulstruct`, Soulstruct for Blender 2.6.0 |
| PlayerRepo present | Yes |
| ELDEN VINS present | Yes |
| Vanilla Elden Ring present | No |

## Phase 0 result

PASS. Repository and tool paths were detected rather than guessed. The logical library directory structure exists, installed third-party versions are pinned, and missing sources are explicit.

## Blockers and constraints

- Vanilla Elden Ring game files are not available at the checked Steam locations.
- `All Characters` combines content from multiple games and does not carry a complete source skeleton beside every loose HKX set. Skeleton families must be identified before conversion.
- Soulstruct labels Elden Ring animation support as partial/experimental. Conversion must remain pilot-only until pose, axes, scale, duration, and root motion are verified.
- Generated FromSoftware-derived animation assets must not be committed without an explicit Git LFS/distribution decision.
- Automated tests were not created because Project S repository rules require an explicit user request for test scaffolding.
