# Animation module

`ModularAnimation` is the rig-independent presentation boundary between
gameplay and a conventional Unity Animator Controller. Gameplay owns movement,
action eligibility, combat timelines, cancel windows, damage and
collision-resolved translation. The Animator owns clips, blend trees and pose
crossfades only.

The module does not contain Project S assets, clip names or a ScriptableObject
animation graph. Both Generic and Humanoid Avatars are supported when the
assigned controller and clips are compatible with the Avatar.

## Class tree and dependencies

```mermaid
classDiagram
    namespace AnimationCore {
        class ICharacterAnimationDriver {
            <<interface>>
            +ApplyLocomotion(frame, deltaTime)
            +PlayAction(request) AnimationActionPlayback
            +CancelAction()
            +SetPlaybackSpeed(speed)
            +Reset()
        }
        class CharacterLocomotionFrame
        class AnimationActionRequest
        class AnimationActionPlayback
        class ICharacterLookAnimationDriver
    }

    namespace UnityBackend {
        class MecanimCharacterAnimationDriver {
            -Animator animator
            +ApplyLocomotion(frame, deltaTime)
            +PlayAction(request) AnimationActionPlayback
            +CancelAction()
        }
        class MecanimCharacterParameters
        class NullCharacterLookAnimationDriver
    }

    namespace ProjectSCore {
        class PlayerCharacterAnimationPresenter
        class PlayerAnimationCoordinator
        class PlayerLocomotionAnimationAdapter
        class PlayerCombatAnimationAdapter
    }

    ICharacterAnimationDriver <|.. MecanimCharacterAnimationDriver
    ICharacterLookAnimationDriver <|.. NullCharacterLookAnimationDriver
    PlayerCharacterAnimationPresenter --> ICharacterAnimationDriver
    PlayerAnimationCoordinator --> ICharacterAnimationDriver
    PlayerLocomotionAnimationAdapter --> PlayerCharacterAnimationPresenter
    PlayerLocomotionAnimationAdapter --> PlayerAnimationCoordinator
    PlayerCombatAnimationAdapter --> PlayerAnimationCoordinator
    MecanimCharacterAnimationDriver ..> MecanimCharacterParameters
```

Dependency direction:

```text
ProjectS.Core.Runtime -> ModularAnimation.Unity -> ModularAnimation.Core
```

The module assemblies do not reference `_App/Core` or `_Game`. Clip selection,
transition tuning and the Animator Controller asset belong to Project S.

## Runtime ownership

```mermaid
flowchart LR
    I["Semantic input"] --> G["Gameplay state machines"]
    G --> M["Character motor"]
    G --> P["Animation adapters"]
    P --> D["Mecanim driver"]
    D --> A["Animator parameters"]
    A --> C["Animator Controller"]
    C --> V["Pose and crossfades"]
    M --> W["Collision-resolved world movement"]
```

- `MecanimCharacterAnimationDriver` is the only runtime writer of Animator
  parameters.
- Locomotion is continuous. The driver does not convert speed into a discrete
  idle/walk/run enum.
- Finite actions use semantic action IDs and variants. Project S maps those
  values to ordinary Animator transitions.
- `PlayerAnimationCoordinator` arbitrates action priority
  (`Death > Reaction > Traversal > Combat > Locomotion`) without choosing a
  clip.
- `Animator.applyRootMotion` is disabled. Dodge and combat translation pass
  through `CharacterMotor` and its collision/grounding rules.
- Combat duration belongs to gameplay authoring data; it is never inferred from
  clip length or Animator normalized time.

## Animator contract

Every compatible controller defines these parameters:

| Parameter | Type | Meaning |
| --- | --- | --- |
| `PlanarSpeed` | Float | actual local planar speed in metres per second |
| `NormalizedSpeed` | Float | actual speed divided by the active gameplay maximum |
| `MoveX` | Float | normalized local lateral direction |
| `MoveY` | Float | normalized local forward direction |
| `VerticalSpeed` | Float | signed motor velocity along the character up axis |
| `Grounded` | Bool | confirmed motor grounding |
| `LocomotionPlaybackRate` | Float | optional locomotion state speed multiplier |
| `JumpStarting` | Bool | gameplay-owned jump anticipation window |
| `Action` | Trigger | starts a semantic finite action |
| `ActionId` | Int | action family |
| `ActionVariant` | Int | direction/content variant |
| `CancelAction` | Trigger | requests an interruptible action exit |

`PlanarSpeed` and `NormalizedSpeed` are damped over `0.18 s`; direction over
`0.08 s`; vertical velocity over `0.04 s`. The longer speed filter keeps an
abrupt motor stop from crossing the walk/run blend in a single rendered frame.
These presentation filters do not delay gameplay state or motor response.

The old `MovementType`, `MovementDirection`, `FallingDuration` and `OnGround`
names remain constants only for imported/reference controllers. They are not
written by the Project S player driver.

## Project S controller policy

`PlayerKurtzPel.controller` is a normal, editable Animator Controller with one
layer and Write Defaults disabled on every state. Its builder creates it only
when it is missing; later manual Animator edits are validated and preserved.

- Idle is separate from the locomotion Blend Tree so movement never starts at
  an arbitrary shared phase.
- The locomotion tree contains only compatible cyclic walk/run/sprint clips and
  uses physical thresholds `3 / 5 / 10 m/s`.
- Idle/move use `0.10 / 0.14 m/s` hysteresis and `0.16 s` crossfades.
- Walk is the phase reference. Run and sprint cycle offsets align their first
  measured foot contacts to it.
- Jump anticipation, rise, fall and landing are separate states driven by
  gameplay grounding/velocity rather than clip completion alone.
- The seven finite player clips are non-destructive binary FBX exports from
  `KurtzPel_Player_Seamless.asset`. Their boundary keys provide authored
  anticipation and recovery poses while the imported reference FBX files stay
  unchanged. The generated FBX importers keep animation compression disabled;
  UMotion owns the intentional key reduction and the runtime files remain
  compact without Unity applying a second lossy pass.
- Dodge entry is `0.14 s`; attack entry is `0.18 s`; dodge exit is `0.16 s`;
  attack exit is `0.18 s`. Each finite-action return uses a sampled destination
  phase instead of restarting locomotion or idle at frame zero.
- Dodge playback speed is fitted to the motor-owned `0.78 s` window, allowing
  both source clips to play at approximately their authored rate.

The current semantic map uses action `1` for forward/backward dodge and action
`2` for the first unarmed light attack. Combo expansion adds ordinary Animator
states/transitions and gameplay timelines; it does not add another animation
graph abstraction.

## KurtzPel authoring windows

`Assets/_Game/Animations/Character/UMotion/KurtzPel_Player_Seamless.asset` is
the editable source for the current finite player clips. Source package FBX
files are references only and are never overwritten.

| Clip | Authored range | Purpose |
| --- | --- | --- |
| `HM_ENFJ_Air_Start` | frames `0-7` | idle-compatible anticipation ending at the original rise pose |
| `HM_ENFJ_Air_JumpUp` | unchanged | preserves the source ascent |
| `HM_ENFJ_Air_JumpDown` | frames `0-8` | begins at the sampled ascent apex and blends back to the source fall |
| `HM_ENFJ_Air_JumpLanding` | frames `45-57` | completes recovery at the sampled idle phase |
| `HM_ENFJ_Dash` | frames `0-8`, `33-45` | locomotion-compatible entry and forward-dodge recovery |
| `CC_C_Rage_Avoid` | frames `0-8`, `37-49` | locomotion-compatible entry and backward-dodge recovery |
| `HMGT_BasicCombo_01` | frames `0-11`, `66-80` | neutral anticipation and locomotion-compatible attack recovery |

UMotion exports one binary Generic FBX per clip with `0.0001` position,
rotation and scale reduction thresholds. Unity importer compression stays off,
so re-exporting is deterministic and does not stack two reduction passes.

## Generic and Humanoid

The Player currently uses the KurtzPel Generic Avatar because the model and all
437 clips share the exact source skeleton, including weapon/twist bones. A
temporary Humanoid import probe produced a valid Human Avatar with 52 mapped
human bones, so later migration is feasible.

Migration should happen when the production Humanoid character exists. Doing it
now would add retargeting differences without providing a runtime benefit. The
module contracts, gameplay state machines, motor and semantic action IDs remain
unchanged during that content migration.
