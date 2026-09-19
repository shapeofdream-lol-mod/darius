# Shape of Dreams Official EntityModel Integration Validation

Status: **Fresh EntityModel lifecycle, stock-controller locomotion, and URP/Unlit native material presentation validated; action integration remains in progress**

This document records the reusable validation path for integrating a custom traveler model through
Shape of Dreams' public model/animation lifecycle. Nothing in the "Validated knowledge" section
should be treated as proven until the runtime acceptance criteria below pass.

## Official API evidence

## API documentation/version caveat

The online API documentation can be newer than the game assemblies used by a released build.
During the 2026-09-19 experiment, the online `EntityVisual` documentation exposed
`public IReadOnlyList<Material> materials`, but the locally referenced game assembly used for the
Release build did not expose that member to C# and compilation failed when it was referenced
directly.

Reusable rule:

- treat the online docs as the preferred API reference,
- but compile against the target game's actual assemblies before promoting an API member into
  reusable integration code,
- record version mismatches instead of adding reflection-based dependencies unless the missing
  member is required for functionality.

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

The first experiment was deliberately Classic-only. After Classic passed lifecycle, locomotion,
and URP/Unlit material validation, the same baseline was extended to all four shipped Darius skins.

Each migrated skin is registered as a resource whose root directly contains:

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

As of commit `b6bdb9148d58f8f967ca172564f506679c3f3ef7`, God-King, Dunkmaster, and Mecha no
longer use the synthetic Host/Bridge model-loading path. They now use the same fresh EntityModel
baseline as Classic.

God-King retains its authored wolf/throne presentation renderers in the native prefab hierarchy,
but those `DariusHidden_*` renderers are excluded from `EntityModel.bodyRenderers` and remain
disabled. Their skill-specific visibility transitions are intentionally deferred to the later
action-integration phase rather than reintroducing the legacy Bridge.

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

## Runtime evidence — stock AnimatorController experiment succeeded

Using the same fresh Classic EntityModel but replacing the generated Darius controller with the
stock Vesper `Base Controller` produced the expected locomotion behavior.

Observed diagnostic sequence:

- stationary: `currentClip=Idle1`
- movement begins with measured world speed around `3.96`: `currentClip=Run`
- sustained movement at approximately `4.8-4.9`: `currentClip=Run`
- the Darius `Run` clip, not a Vesper run clip, was active in the Animator

This is the first runtime confirmation that the reusable official path is:

`fresh custom EntityModel + stock SoD AnimatorController contract + custom animation clips`

rather than:

`fresh custom EntityModel + arbitrary custom AnimatorController`

The current renderer/material visibility issue is independent and remains unresolved.


### Runtime evidence — material/PropertyBlock diagnostics

In the 2026-09-19 material diagnostic pass, the Darius renderer's shared material remained stable
through spawn and movement:

- shader: `Standard`
- RenderType: `Opaque`
- render queue: `2000`
- `_Color=(0.8,0.8,0.8,1)`
- `_SrcBlend=1`
- `_DstBlend=0`
- `_ZWrite=1`
- Darius texture remained assigned

The renderer nevertheless carried a `MaterialPropertyBlock` throughout the same lifecycle. The
queried common color/alpha/visibility values read as zero, although Unity's getter semantics do not
by themselves prove that every queried property is explicitly present in the block.

At the same time the renderer was enabled, active, reported visible, had sane bounds, cast a visible
shadow, and the Animator switched between Darius Idle and Run. This further separates the remaining
problem from model loading, animation, transforms, bounds, and ordinary renderer enablement.

The next isolated experiment therefore reuses the stock Traveler material/shader template while
preserving the Darius texture. This tests whether `EntityVisual` runtime shader/property updates
require the stock visual shader contract.


## Runtime evidence — material path resolution

The material experiments established that renderer visibility and the official EntityModel lifecycle
must be treated as separate concerns.

First, replacing Unity Standard with the stock Traveler `Dew/Dew Entity` shader made the Classic
body visible, proving that the earlier invisibility was material/shader-related rather than a model,
Animator, bounds, or EntityModel lifecycle failure. However, even after common metallic,
smoothness, specular-highlight, and environment-reflection controls were neutralized, the Classic
body retained a pronounced lit/plastic response that the other Darius skins did not show.

A later isolated test at commit
`b2d3d6690938e55941f3111d45ed5b7e947b1705` kept the already validated fresh EntityModel and
stock AnimatorController path, but rebound Classic through the same existing
`DariusRuntimePerformance.OptimizeSkinnedRenderers()` path used by the other Darius skins.

Runtime evidence from that build:

- Classic remained visible in normal gameplay;
- the renderer used `Universal Render Pipeline/Unlit`;
- the Darius base texture remained assigned;
- the tester reported that the plastic reflection disappeared;
- the tester also reported that the model colour returned to the expected appearance;
- `Hero_Darius.OnModelLoaded` still reported:
  - `officialFresh=True`
  - `native=False`
  - `legacy=False`
  - `initialized=True`
  - `animator=Model`;
- locomotion remained alive, with diagnostics showing Darius Idle and Run clips on the stock
  AnimatorController path;
- no `NullReferenceException` or model/Animator setup failure was observed in that run.

The same run also contained three lobby-loadout `KeyNotFoundException` entries from
`UI_Lobby_Loadout_SkillSlot.UpdateHasNewStatus()` for Darius skill resource names. These are
tracked as a separate registration/UI issue and are not evidence against the model/material path.

Reusable rule:

> A fresh custom EntityModel does **not** require `Dew/Dew Entity` specifically. The renderer
> material may use a compatible alternative such as URP/Unlit while the official
> EntityModel/EntityAnimation lifecycle and stock AnimatorController remain authoritative.

For League-style models whose visual source is primarily the authored base-colour texture, the
tested Darius baseline is therefore a texture-dominant URP/Unlit material path rather than forcing
the stock Traveler lit shader.

Performance note: the tester reported no obvious performance problem in normal play after this
change. This is qualitative runtime observation only; no profiler capture or benchmark was taken,
so no quantitative performance claim is promoted to validated knowledge.

## Next isolated experiment — stock AnimatorController

Keep the validated fresh EntityModel lifecycle and stock controller path.

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

## All-skin migration — runtime validation pending

Implementation commit:
`b6bdb9148d58f8f967ca172564f506679c3f3ef7`

The following skins now share the Classic baseline:

- Classic
- God-King
- Dunkmaster
- Mecha

For every skin, registration now:

1. instantiates that skin's native Unity prefab as the resource root;
2. adds a fresh, uninitialized `EntityModel`;
3. captures Idle/Run/Death clips from the native controller before replacing it;
4. assigns the stock SoD AnimatorController;
5. fills the EntityModel Idle/Run/Death contract with that skin's own clips;
6. applies the shared URP/Unlit native renderer path;
7. uses the prefab's real health/weapon anchors;
8. marks the resource with `DariusOfficialEntityModelMarker`;
9. does not add `DariusNativeModelHost` or `DariusTravelerModelInstance`.

Runtime acceptance for the three newly migrated skins:

- `Hero_Darius.OnModelLoaded officialFresh=True native=False legacy=False`;
- loaded EntityModel is initialized;
- Animator is non-null and uses the stock controller;
- Idle and Run use the selected skin's authored clips;
- body renderer remains visible with URP/Unlit;
- God-King wolf/throne hidden presentation meshes do not become permanently visible;
- no model-load / Animator exception occurs.

This section remains implementation state. The three skins did load through the fresh EntityModel
baseline at runtime, but action integration exposed the lower-body/full-body ownership regression
documented below. Do not promote complete all-skin action compatibility into `Validated knowledge`
until the new native action overlay passes runtime testing.

## All-skin action regression — 2026-09-19

Runtime testing after the all-skin fresh EntityModel migration exposed a presentation-layer
regression in God-King, Dunkmaster, and Mecha:

- the selected skin loaded successfully through the fresh EntityModel path;
- stock Idle/Run locomotion remained active;
- Q gameplay execution reached its normal windup and server-resolution stages;
- fresh skins had no `DariusNativeModelBridge`, so Q/basic-attack hooks fell through to the
  generic `UniversalRetargeter`;
- that retargeter and the stock Animator both wrote the same skeleton while moving;
- during a moving Mecha Q, diagnostics showed the stock Animator back on `Run_Normal` before
  the Q gameplay windup had completed, matching the observed visual interruption;
- repeated basic-attack `OnCastStart` events were present in the same runtime log, so the reported
  repeated-attack failure is not yet proven to be a native attack cadence lock;
- only one Q trigger was recorded per newly tested skin and the old diagnostics did not expose
  charge/cooldown/ability-lock state, so a second-Q gameplay lock remains a separate unknown.

The relevant behavior baseline in `main` is not a full-body locomotion/action race. Its runtime
model keeps Root/Pelvis/torso/weapon authority on the combat action and independently restores only
the leg-chain locomotion pose while the Hero moves.

The native fresh-model experiment now ports that ownership rule without restoring raw GLB runtime
playback:

- each native skin's Unity `AnimationClip` references are captured before the AnimatorController is
  replaced by the stock SoD controller;
- Q and basic attacks are sampled in `LateUpdate` through
  `DariusOfficialActionRuntime`;
- the stock Animator remains authoritative for Idle/Run;
- before action sampling, the stock lower-body pose is captured;
- after the action clip is sampled, only the leg-chain pose is restored;
- the model GameObject transform is restored after sampling so world/model placement stays SoD-owned;
- stock ability-animation RPC presentation is suppressed only when this fresh action overlay is
  available, preserving a single action-animation owner;
- Q recovery diagnostics now record charge, cooldown, minimum delay, `CanBeCast()`, ability index,
  and `EntityAbility.IsAbilityCastLocked()` after cast and after cooldown expiry.

Implementation commits:

- `0bb21995a60d967de2ab0318fb146f8a5579ead3` — native lower-body action overlay
- `a23bf5733adb3500e4762a6fa9ad215a9631073d` — preserve native clips on fresh templates
- `c9afa4b4bf90c82de532a8a1c858adbb26e49a09` — route Q/basic attacks to the overlay
- `78b762b6613c765f01e4cd3770204e3475927ce8` — bind overlay and enforce one action owner
- `aef4a1e48a11a9d8e57b44bdd06fbd6c5932fd92` — Q recovery diagnostics
- `f675ab6996bfbf9a17bcdd75c345a350262ed947` — preserve fresh-skin variant identity
- `2a29c7d1ad3a1664e72b4b4f73e211a7b3a37fd8` — keep God-King hidden wolf/throne objects inactive

God-King runtime testing also showed that excluding authored hidden renderers from
`EntityModel.bodyRenderers` and setting `Renderer.enabled=false` was not a stable enough contract:
the fresh EntityVisual lifecycle can still enumerate/re-enable renderers from the loaded hierarchy.
The native prefab already splits wolf/throne geometry into dedicated `DariusHidden_*` objects, so
the fresh registration path now keeps those GameObjects inactive as well as disabling their
renderers. This avoids a per-frame guard and keeps the hidden objects available for a later,
explicit God-King action-specific opt-in path.

The code passes repository contracts and the Release build against the SoD reference pack. Runtime
behavior remains **unvalidated** until a moving Q, repeated attacks, and a second Q after cooldown are
tested on the migrated skins.

## Fresh presentation parity completion — 2026-09-19

Static review after the first all-skin migration showed that the shared Classic baseline had been
ported, but several presentation contracts from the old native Bridge were still missing from fresh
EntityModel skins. The follow-up implementation keeps gameplay logic unchanged and completes only
the presentation layer:

- Q and basic attacks continue to use native Unity clips sampled after stock locomotion;
- W, E, and R now use the same fresh native action overlay instead of falling through to the generic
  UniversalRetargeter;
- authored attack-to-idle clips are restored for skins that provide them;
- E selects its authored ToIdle/ToRun tail from actual movement state;
- R selects its authored ToRun tail and God-King explicitly opts its wolf renderer in only for the
  R action window;
- God-King Q preserves its stationary Spell1_ToIdle tail;
- God-King W restores ActivateIdle/ActivateRun, alternating IdleIn entries, persistent armed
  locomotion clips, and authored Deactivate transitions;
- the existing stock lower-body locomotion pose remains authoritative while all these action clips
  own Root/Pelvis/torso/weapon presentation;
- lower-body bone-mask resolution is validated during fresh skin registration and fails early if no
  compatible leg-chain nodes exist;
- fresh EntityModel skins now resolve weapon/chest anchors directly from EntityVisual.model;
- fresh EntityModel skins now create filtered submesh/full-mesh overlays through the same native
  overlay mesh cache and material-hash contract previously available only through the Bridge;
- legacy Bridge/GLB fallbacks remain available only for their existing compatibility path and are
  not reintroduced into fresh skin model loading.

Implementation commits:

- `950028ee1a9f933d2aa266bdd82b953a3736a872` — restore authored fresh-skin action transitions
- `7eefd4241e4af3239b90b08ced1c60d0e368fa29` — route fresh W/E/R through native action overlay
- `615869552e3a82b3be4c2b6d628051b37a6d34c1` — expose fresh model anchors and overlays
- `5380a052d410bac93a2c7c48dd5dcf15ddfeaf1c` — use fresh presentation surface helpers
- `b6aae8662479abae45e2bda5e236a9bcb1ef6b31` — validate lower-body mask during registration

The changes are intentionally presentation-only. They do not change Q/W/E/R damage, targeting,
cooldown, charge, Hemorrhage, displacement, or execute/reset gameplay rules.

Runtime validation is still required before promoting complete all-skin action parity into the
validated section. Test repeated attacks, moving/stationary Q, W arm/consume/expire, moving E,
R, fresh VFX anchors/overlays, and God-King hidden/wolf behavior.

## Validated knowledge

1. **Freshness matters and is observable.** A custom `EntityModel` template can be registered with
   `isInitialized == false` and later appears as a distinct initialized clone after SoD loads it.
2. **The official model lifecycle can bind a custom Animator.** In the tested Classic path,
   `EntityAnimation.animator` resolved to the native `Model` Animator without a Darius bridge
   rebinding it.
3. **Model-lifecycle compatibility and locomotion-controller compatibility are separate contracts.**
   Successful `LoadModelLocal` / initialization does not by itself prove that SoD can drive the
   custom AnimatorController's Idle/Run states.

4. **Stock AnimatorController locomotion is compatible with a fresh custom EntityModel.** In the
   2026-09-19 Classic test using the stock Vesper `Base Controller`, the same fresh Darius
   EntityModel switched from `Idle1` to the Darius `Run` clip while the Hero's measured world
   speed rose to approximately 4.9. The Animator remained on `Run` throughout sustained movement.
   This validates that the official locomotion path depends on the stock AnimatorController
   contract/state machine, while custom clips can still be supplied through the custom EntityModel.

5. **Renderer material choice is separable from the official model/animation lifecycle.** The
   Classic body remained fully compatible with the fresh EntityModel + stock AnimatorController
   path after rebinding its renderer to `Universal Render Pipeline/Unlit`. Runtime testing at
   `b2d3d6690938e55941f3111d45ed5b7e947b1705` confirmed normal body visibility, working
   Idle/Run animation, removal of the Classic-only plastic reflection, and corrected colour. This
   supersedes the earlier hypothesis that `Dew/Dew Entity` itself was required for custom
   Traveler visibility.

Only items backed by runtime evidence should be promoted into this section. Future successful
animation/render findings should record the game build, mod commit, bundle fingerprint, exact
behavior, and required EntityModel/controller fields.
