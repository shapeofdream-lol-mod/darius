# Shape of Dreams Official EntityModel Integration Validation

Status: **Pending Runtime Validation**

This document records the reusable validation path for integrating a custom traveler model through
Shape of Dreams' public model/animation lifecycle. Nothing in the "Validated knowledge" section
should be treated as proven until the runtime acceptance criteria below pass.

## Official API evidence

The Shape of Dreams API documents the following public contracts:

- `EntityVisual.LoadModelLocal(EntityModel)` locally changes an entity model. The provided
  `EntityModel` must be **fresh** and must not be an already initialized instance.
  - https://lizardsmoothie.com/sod/moddoc/api/Global.EntityVisual.html
- `EntityModel` owns the presentation data contract: body renderers, health/weapon anchors,
  idle/lobby/death/stagger clips, locomotion type, walk animation speed, and directional run clips.
  - https://lizardsmoothie.com/sod/moddoc/api/Global.EntityModel.html
- `EntityAnimation` exposes the runtime animation lifecycle through `SetupModel()`,
  `FrameUpdate()`, `LogicUpdate()`, `PlayAbilityAnimation()`, `PlayStaggerAnimation()`,
  and animation replacement APIs.
  - https://lizardsmoothie.com/sod/moddoc/api/Global.EntityAnimation.html
- `Hero.LoadEntityModelLocal()` and `Hero.OnModelLoaded()` are the standard Hero-side model
  lifecycle boundaries.
  - https://lizardsmoothie.com/sod/moddoc/api/Global.Hero.html

## Experiment scope

The first experiment is deliberately Classic-only.

Classic is registered as a resource whose root directly contains:

- `Skin`
- a fresh, uninitialized `EntityModel`
- the Unity-native Darius model hierarchy
- the native `Animator`
- the real skinned renderers and skeleton
- Darius Idle / Run / Death clips bound into `EntityModel`
- real health/weapon anchors
- a `DariusOfficialEntityModelMarker` diagnostic marker

The experiment intentionally does **not** use these systems for Classic:

- `DariusNativeModelHost`
- `DariusNativeModelBridge`
- custom locomotion sampling
- native Animator action lease
- custom `EntityAnimation.FrameUpdate` suppression
- custom `ReplaceAnimationLocal` suppression
- custom ability-RPC suppression

God-King, Dunkmaster, and Mecha remain on the previous path and serve as a control group.

## Why this experiment is useful

The previous native implementation used a synthetic `EntityModel` shell and instantiated the
actual native model later as a child. That required post-load attempts to reconcile
`EntityModel`, `EntityAnimation`, and a separately created Animator.

This experiment asks one narrow question:

> Can a complete fresh custom `EntityModel` template enter the ordinary SoD model lifecycle and
> receive stock locomotion/death animation ownership without a second presentation controller?

A positive result means future traveler mods should prefer constructing a complete EntityModel
resource up front instead of repairing the contract after model load.

## Runtime acceptance criteria

Use **Classic Darius only** for the first pass.

1. Enter a run with Classic selected.
2. Confirm the log contains an `OFFICIAL-ENTITYMODEL` template line with:
   - `initialized=False` before load,
   - non-zero renderer count,
   - native Animator name.
3. Confirm `Hero_Darius.OnModelLoaded` logs:
   - `officialFresh=True`,
   - `native=False`,
   - `legacy=False`,
   - a non-null EntityModel,
   - `initialized=True`,
   - a non-null Animator.
4. Stand still for several seconds: Darius must remain in Idle.
5. Move continuously: Darius must enter Run without Darius-specific locomotion code.
6. Repeat Idle -> Run -> Idle at least five times.
7. Change movement direction repeatedly and confirm animation remains alive.
8. Die while Idle and while moving; SoD must play the configured Death presentation without a
   Darius bridge taking ownership.
9. Check the log for unsuppressed `EntityVisual.LoadModelLocal`, `EntityAnimation.SetupModel`,
   Animator, or NullReference failures.

Ability/basic-attack validation is intentionally a second phase. Do not use attack/Q behavior to
judge the first locomotion experiment.

## Failure interpretation

- If `LoadModelLocal` fails before `OnModelLoaded`, the resource/prefab EntityModel contract is
  incomplete.
- If `OnModelLoaded` reports the expected fresh model and Animator but Idle/Run does not switch,
  the remaining unknown is the stock Animator/locomotion-controller contract rather than model
  lifecycle ownership.
- If Idle/Run/Death work, the official model lifecycle is validated and should become the default
  architectural baseline before testing `PlayAbilityAnimation(DewAnimationClip)`.

## Validated knowledge

**None yet.**

After a successful runtime pass, record here:

- game build/version,
- mod commit SHA,
- native bundle fingerprint,
- exact tested behaviors,
- relevant log excerpts,
- any required EntityModel fields,
- any fields that proved unnecessary,
- known limitations.

Only items backed by a successful runtime test belong in this section.
