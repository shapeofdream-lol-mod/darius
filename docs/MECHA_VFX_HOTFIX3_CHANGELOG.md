# Darius v0.30.6-final — Mecha VFX Hotfix 3

This pass is driven by the in-game visual result rather than `built/unsupported` counts.

## Noxian Might / max-stack passive

- **White airflow is retained, but tightened.** `Darius_Skin67_P_enraged/smoke` and `smoke1` are the authentic soft white airflow layers. Their particle size and velocity are now multiplied by `0.52`, keeping the effect around Darius instead of spreading several metres away.
- **The large rectangular white sheet is removed at its real source.** `P_enraged/Distort` uses a tiny solid-white `color-hold` texture that is meaningful only with Riot's distortion shader. Rendering it through a normal Unity alpha particle material produces the visible white rectangle, so Skin67 now fidelity-gates this emitter.
- Existing guards for `Ground_Lighting`, the unsupported max-stack champion-atlas meshes, and `FireCards` remain.
- Hashless Skin67 AttachedMesh emitters remain omitted rather than being painted over the full character model, preventing the previous full-body green-grid failure.

## Q windup geometry

- `Q_RingWindup/AOE` and `AOE_BG` are circular source textures authored as `[radius,1,1]`. The runtime now treats the X component as the in-plane Riot half-extent for **both** quad axes instead of creating a flattened/elliptical quad.
- The four `teamring_circle*` windup mesh layers are normalized to circular X/Z scaling. The inner pair remains a smaller concentric decoration; the outer pair is normalized to the same **4.25 m** outer boundary as the released Q ring/gameplay hit radius.
- The existing Skin67 windup root scale (`0.992376`) and released-ring root scale (`1.041622`) are retained, so both phases converge on the same world-space boundary.
- The green `WindowPattern` windup emitters `ant_dark1/ant_dark2` remain filtered.

## Preserved repairs

- Skin67 E stale-direction lifetime/endpoint-trail repairs remain.
- Skin67 R mesh UV/fractional-texDiv repair remains.
- Complete Hero/Skin/EntityModel/Attack generation rebuild after run teardown remains.
- v0.30.4 BUILD_FIX1 `CreateFullMeshOverlay` forwarding method remains present even though unsafe hashless Skin67 overlays are no longer used.
