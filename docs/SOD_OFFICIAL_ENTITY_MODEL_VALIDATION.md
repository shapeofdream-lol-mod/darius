# Shape of Dreams Official EntityModel Integration Validation

Status: **Lifecycle validated; locomotion/render integration still under investigation**

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

## Runtime evidence — 2026-09-19 Classic experiment

Tested commit: `4e84129fabf3628e9dd9660cc3db00da5ce57997`

Native bundle fingerprint:

`c9267a619c9becc848028dd324fd4bd4ba838fbd3e9e6ba1288ca13ffa20e3a8`

Game runtime reported Unity `6000.0.77f1`.

Observed runtime facts:

- The registered Classic template was fresh before load:
  - `initialized=False`
  - `locomotion=EightDirections`
  - `walkSpeed=0.9`
  - one body renderer
  - `support4=True`
  - `support8=True`
  - native Animator object `Model`.
- SoD successfully consumed the template through its ordinary model lifecycle.
- In `Hero_Darius.OnModelLoaded`, the loaded model was:
  - `Skin_Darius_Default(Clone)`
  - `initialized=True`
  - `officialFresh=True`
  - `native=False`
  - `legacy=False`
  - `EntityAnimation.animator=Model`.
- No `EntityVisual.LoadModelLocal` / `EntityAnimation.SetupModel` exception was observed during
  that load.

These facts validate the **fresh EntityModel lifecycle contract**: a custom runtime resource can be
accepted and initialized by SoD, and `EntityAnimation` can bind to the Animator contained in that
loaded model.

### Not validated / failed in the same experiment

- The visible Classic body did not render in the normal gameplay view.
- A standing shadow/reflection remained visible.
- Moving did not produce a Run animation in that shadow/reflection.
- Therefore a fresh EntityModel plus populated Idle/Run fields is **not sufficient evidence** that
  an arbitrary custom AnimatorController satisfies SoD's locomotion controller contract.
- Do not document the current generated Darius AnimatorController as stock-compatible.

## Runtime evidence — 2026-09-19 renderer/Animator diagnostics

With the diagnostic build after the first experiment:

- During actual movement, measured Hero world speed reached approximately `4.9`.
- Across all samples, Animator layer 0 remained on `Idle1`.
- `transition=False` and no next Animator state/clip was observed while moving.
- Therefore Run was not merely invisible: the generated custom controller never entered a Run
  transition under stock `EntityAnimation` driving.
- The temporary `EntityVisual.isRendererOff=True` state correlated with spawn/teleport staging,
  including the Hero temporarily being positioned around `(-5000,-5000,0)`.
- After returning to the playable map, `isRendererOff=False`.
- The Darius mesh renderer remained:
  - enabled,
  - active in hierarchy,
  - `forceRenderingOff=False`,
  - layer 0,
  - shadow casting On,
  - using the Standard shader.
- The loaded model and Animator both remained active/enabled.

This rules out the simplest visibility causes (persistent EntityVisual renderer shutdown, disabled
GameObject/Renderer, force-render-off, or an unexpected layer) and separates the remaining rendering
problem from the locomotion-controller problem.

## Next isolated experiment — stock AnimatorController

Keep the already validated fresh EntityModel lifecycle, but replace only the generated Darius
AnimatorController with the runtime controller from the stock EntityModel template.

Purpose:

> Test whether SoD locomotion is implemented through a stock AnimatorController graph/parameter
> contract rather than direct state selection against arbitrary controllers.

Darius Idle/Run/Death clips remain referenced by the custom EntityModel. No custom locomotion
sampler, action lease, or Darius Animator state switching is added.

If locomotion begins working with the stock controller, future traveler integrations should prefer
the game's controller/state-machine contract and official animation replacement APIs instead of
reimplementing locomotion transitions.

A concurrent observational diagnostic records model transform scale/rotation and SkinnedMeshRenderer
world/local bounds to investigate the still-invisible main body without changing visibility behavior.

The next diagnostic pass records:

- `EntityVisual.isRendererOff`
- `EntityVisual.renderers` / `solidRenderers`
- loaded model active state and layer
- each renderer's enabled/active/layer/material/shader state
- Animator current/next clip and state hashes
- actual Hero world movement speed

This diagnostic is observational only and must not change renderer or animation behavior.

## Validated knowledge

1. **Freshness matters and is observable.** A custom `EntityModel` template can be registered with
   `isInitialized == false` and later appears as a distinct initialized clone after SoD loads it.
2. **The official model lifecycle can bind a custom Animator.** In the tested Classic path,
   `EntityAnimation.animator` resolved to the native `Model` Animator without a Darius bridge
   rebinding it.
3. **Model-lifecycle compatibility and locomotion-controller compatibility are separate contracts.**
   Successful `LoadModelLocal` / initialization does not by itself prove that SoD can drive the
   custom AnimatorController's Idle/Run states.

Only items backed by runtime evidence should be promoted into this section. Future successful
animation/render findings should record the game build, mod commit, bundle fingerprint, exact
behavior, and required EntityModel/controller fields.
