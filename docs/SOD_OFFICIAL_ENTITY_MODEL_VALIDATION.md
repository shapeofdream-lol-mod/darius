# Shape of Dreams Official EntityModel Integration Validation

Status: **Fresh EntityModel lifecycle, stock-controller locomotion, and URP/Unlit native material presentation validated; fresh-only action integration remains runtime-validation pending**

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
- stock ability-animation RPC lifecycle remains enabled; the Darius overlay owns only the final
  sampled model pose and does not replace SoD cast/action state;
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

## Fresh action lifecycle + authored alpha correction — runtime validation pending

A later all-skin runtime pass showed a more severe regression than presentation-only stalling:
after Q/R/basic attacks, subsequent attack/skill input could stop producing both animation and
gameplay damage on several fresh skins, while God-King behaved differently.

Comparison against the working `main` implementation identified a fresh-only divergence:
`DariusEntityAnimationAbilityRpcPatch` suppressed SoD's
`EntityAnimation.UserCode_RpcPlayAbilityAnimation*` receiver path whenever the fresh native
overlay was available. The working main presentation never replaces SoD's gameplay/action
lifecycle this way; it only owns the model pose.

Fresh skins therefore now always allow the stock ability-animation RPC to run. The native action
overlay remains a late pose layer only. This restores SoD as the authoritative owner of cast/attack
lifecycle while retaining Darius' native clips for presentation.

Implementation:
- `98da19b65e4374cc3287a5b613c4954833278ead` — preserve SoD ability RPC lifecycle for fresh skins

The same pass also revisited God-King's black-background material issue using `main` as the
reference. Main reads the GLB material `alphaMode` and configures BLEND materials as transparent
URP/Unlit surfaces. The native FBX sidecar previously discarded `alphaMode`, and runtime material
rebinding then forced every visible material to Opaque.

A static review then found that the first alpha-sidecar revision was incomplete: the converter
emitted a five-column manifest while the Unity binder still validated four columns before reading
the fifth. CI did not catch this because it compiles the editor source but does not execute Unity's
native bundle build.

The corrected native material contract is deliberately small and mirrors the useful parts of the
working `main` path:

- the sidecar now has seven explicit fields:
  `renderer / slot / material / texture / alphaMode / alphaCutoff / authoredVisible`;
- `BLEND` is preserved as transparent URP/Unlit;
- `MASK` is preserved as alpha-cutout with the authored/default cutoff;
- GLB `extras.visible=false` participates in the existing hidden-submesh split instead of being
  left in EntityVisual's normal body renderer set;
- the hidden split now runs for every skin rather than being hard-coded to God-King;
- runtime URP/Unlit rebinding preserves both transparent and alpha-cutout state.

Implementation:
- `4de67a8ff13e21e8af839881539d6d0a13d3fe0f` — complete native material visibility contract
- `b5fc0ee3292f9630bcec50328f9c8de342a9bca6` — parse complete native material contract
- `098110400641dcec543b8ebcfe4bf29c838b901f` — keep authored-hidden submeshes out of body renderers
- `7d6dc258d010bf5200085b15791f50173548ff61` — preserve alpha cutout through runtime material rebind
- `ff59a29afa94829e10966c60c26784271904ce42` — run authored-hidden split for every skin

The same review compared the fresh action layer against the working `main` behavior and corrected
three over-broad simplifications without changing gameplay rules:

- lower-body locomotion is restored only while the Hero is actually moving during an action;
  stationary Q/R/basic attacks keep their authored full-body pose;
- interrupting/restarting an action clears the W-swing state and attack-facing ownership;
- basic attacks and W preserve the direction supplied by gameplay and use a lightweight visual
  facing pivot, matching main's separation between gameplay facing and model presentation.

Implementation:
- `86718dbd462fb73fe4e7dc1f976631ec4e06d3c2` — align fresh action layering with main runtime
- `efb419945f293e91b1aa472d20e3fff1cfcedffc` — release fresh attack facing on every exit
- `764c964b06d629781e6752c9eb6624cafc24d354` — preserve fresh attack facing direction
- `776d4855d95c6f6a64e83ec43bd4b0e530a69050` — remove stray Q facing cleanup after CI caught it

Unlike recent C#-only presentation changes, the material-contract fix changes native bundle inputs
and therefore requires rebuilding `darius_models.bundle`; the existing fingerprint contract
already includes the converter and all Unity editor pipeline files, so DeployMod will reject the
stale bundle.

Repository contracts, build tooling checks, package configuration, and the Release build all pass at
`776d4855d95c6f6a64e83ec43bd4b0e530a69050`. Runtime behavior remains unvalidated until the
bundle is rebuilt and repeated Q/R/basic attacks plus God-King alpha/hidden-prop behavior are tested.

## Ponytail architecture convergence — 2026-09-20

After the fresh EntityModel path became authoritative for all shipped skins, the remaining legacy
model stack was reviewed using the Ponytail rule: retain Darius-specific semantics, but remove
parallel systems where SoD already owns the lifecycle.

The registration path first proved that the old fallback was unreachable: every shipped skin must
have a `DariusNativeSkinProfile`, and that same condition was previously used to choose the fresh
path. Registration/readiness were therefore simplified to fresh EntityModel only.

The following retired runtime systems were then disconnected from all presentation entry points and
deleted:

- `DariusNativeModelHost`
- `DariusNativeModelBridge*`
- `DariusTravelerModelInstance` runtime player
- `DariusGlbRuntimeModel*` raw GLB parser/skinner/animation player
- `UniversalAnimation/*` / `UniversalRetargeter`
- `DariusNativeEntityAnimationLease` and the `EntityAnimation.FrameUpdate` suppression patch
- legacy model-load / ReplaceAnimation / ability-RPC compatibility patches

The first cleanup pass temporarily isolated `DariusSkinModelBinding`, then a second pass removed it
after proving it duplicated `DariusNativeSkinProfile`. Fresh registration and
`DariusOfficialActionRuntime` now read the same profile object directly. The retained information is
the authored Darius skin/clip contract from `main`; the duplicate runtime binding component is gone.

Compilation after the deletion exposed only two external references: Flash still read the old
Traveler model's movement cache, and teardown still cleared the retired animation lease. Both were
removed without restoring compatibility shims. Repository contracts and the Release build then
passed at `f5bb09df512b192c7bfe9ee7f27ecbbbf348ddd8`.

Two additional thin custom layers were replaced with explicit SoD public contracts:

- `DariusOfficialActionRuntime` now uses `Hero.Control.isWalking` instead of maintaining a
  second world-position/time movement sampler;
- standard weapon and health/chest presentation anchors now come from
  `EntityModel.weapon` / `EntityModel.healthBarPosition`; generic hierarchy lookup remains only
  for uncommon LoL-specific named anchors.

These changes passed the Release reference-pack build at
`f2fc28e4a63b0a1f062a418b2344eab50d6320dd`.

Apprehend displacement was also simplified. The previous implementation searched loaded
assemblies for `DispByDestination`, instantiated it reflectively, enumerated arbitrary
fields/properties, inferred values from member names, and reflected over
`EntityControl.StartDisplacement` overloads. SoD publicly exposes `Knockback` as its helper for a
non-friendly displacement with explicit `distance`, `duration`, `ignoreTenacity`, and
`ApplyWithDirection`. Apprehend already computes an exact destination, so the native path now
uses the target-to-destination direction and distance with that helper. The existing short
time-based fallback is retained only if the official helper throws; no reflection adapter remains.

Flash direction likewise uses the public everywhere-available
`EntityControl.agentVelocity` instead of probing private movement member names. The normal
cast-point and facing fallbacks remain unchanged.

The combined official-control/displacement changes pass repository contracts and the Release build
at `6a3ffaac8e4bed158f70d145aab13b72d51dc214` (CI #497).

Current ownership boundary:

```text
SoD:
  Hero / EntityModel / EntityVisual / EntityAnimation / EntityControl
  cast/action lifecycle / locomotion / displacement primitives

Darius:
  gameplay semantics
  skin + clip metadata
  moving-action lower-body pose composition
  attack/W visual-facing pivot
  God-King authored prop timing
  LoL material-hash/submesh overlays
```

The remaining Darius presentation exceptions are intentionally narrow. They must not grow into a
second generic model, locomotion, animation, renderer, or displacement framework.

Resource compatibility received only deletion-safe cleanup in the same pass:

- unused generic/shared-resource patch factories and the obsolete generic star resolver were removed;
- the remaining lookup patches are only the ones actually installed by `Install()`;
- constellation presentation now resolves Darius stars without the removed generic compatibility
  layer;
- the Hero detail UI now reads public `EntityStatus` / `EntityControl` members directly instead
  of reflecting those gameplay values.

The remaining resource patches are intentionally retained because they each correspond to a known
runtime-only-resource gap: Addressables lookup/preload, network prefab lookup, inclusion checks,
attack binding, loot-pool injection, and previously observed UI/profile failures. Removing them
without a runtime replacement would be speculative cleanup rather than Ponytail simplification.

The retired runtime animation/retarget asset directories are also gone, and the package project no
longer carries dead `assets/animations` / `assets/retarget` globs. This keeps the distributable
contract aligned with the fresh-only runtime instead of advertising deleted compatibility paths.

The convergence code head `03667c87a6ce74aaaf1674406b1ea0035d93d11f` passes repository
contracts, build-tooling checks, package configuration, and the Release reference-pack build
(CI #508). This is compile/reference-pack validation, not a substitute for the pending four-skin
runtime smoke test after rebuilding the native bundle.

Two intentionally deferred areas remain:

- enemy/miniboss classification still contains broad reflection because the public API evidence does
  not yet prove an equivalent runtime-promoted-miniboss classifier;
- Hemorrhage/Noxian Might still have Darius gameplay state alongside native StatusEffect
  presentation. Collapsing those authorities would change passive gameplay state ownership and
  requires a dedicated runtime-backed refactor.

Do not expand either subsystem merely to make the architecture look uniform. The next change should
start only from a concrete runtime failure or a verified public SoD contract that removes code.


## Pending basic-attack range contract smoke test — 2026-09-20

The working `fix/basic-attack-range-desync` branch was reviewed before migration. Its final useful
contract is now ported to the fresh-only branch without the discarded runtime collider/reflection
experiments:

- the Darius attack preset is configured once at construction time with stock-style
  `CastMethodType.Target` semantics;
- `AttackTrigger.allowNonTargetedCast=true` keeps empty-space swings legal;
- the native `MeleeAttackInstance` keeps its fixed broad phase;
- each accepted swing snapshots its live `TriggerConfig.effectiveRange`;
- the Darius forward-sector filter uses that same captured range rather than a second hard-coded
  hit distance;
- the final Darius cleave contract is 2.60 base range and 90 degrees;
- no runtime `TriggerConfig` rewrite, `scaleRangeWithTriggerRange` reflection, or
  `DewCollider` geometry-copy layer was introduced.

Runtime acceptance point:

1. Test normal, alternate, and critical attacks at the outer edge of the accepted attack range.
2. If SoD accepts the attack, the Darius sector filter must not reject the hit because of a smaller
   independent range.
3. Confirm a target outside the captured effective range is still rejected.
4. Confirm large targets use the closest collider contact point rather than transform-center range.
5. Confirm empty-space attacks remain legal and the following attack/skill can still start.
6. Repeat while moving and verify visual-facing correction does not change the captured gameplay
   direction/range.

The same review found that `GetChestAnchor()` had been incorrectly mapped to
`EntityModel.healthBarPosition`. Chest-attached League VFX now again resolve
`C_BuffBone_Glb_Chest_Loc / Chest / Spine` from the fresh native hierarchy. Standard weapon and
health-bar anchors remain on the public `EntityModel` fields.

These are static/compile-target changes only until the four-skin runtime smoke test is performed.

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
