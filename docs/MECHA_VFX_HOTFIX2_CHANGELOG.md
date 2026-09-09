# Darius v0.30.5-final — Mecha VFX Hotfix 2

This revision is driven by in-game visual defects, not by `built/unsupported` counts alone.

## Fixed

- Mecha Q green grid on cast start:
  - filters `Darius_Skin67_Q_RingWindup/ant_dark1`
  - filters `Darius_Skin67_Q_RingWindup/ant_dark2`
  - these two use the WindowPattern grid texture and were the visible green ground grid.
- Mecha Q visual range mismatch:
  - windup system normalized from ~4.283 m to 4.250 m radius.
  - impact system normalized from ~4.080 m to 4.250 m radius.
  - gameplay hit radius remains 4.25 m; only presentation is corrected.
- Full-body green grid after combat stacks:
  - removed the Skin67 fallback that interpreted a hashless `VfxPrimitiveAttachedMesh` as "apply to the entire visible avatar".
  - Skin67 hashless AttachedMesh emitters are now omitted until their exact Riot submesh/shader binding is known.
- Noxian Might / 5-stack white cards:
  - explicitly filters empty `P_enraged/Ground_Lighting`.
  - filters unsupported hologram/card layers `Temp_start`, `Temp_Mesh`, `Temp_Mesh1` in `DariusBasePassiveOverheadMaxStack`.
  - filters `FireCards` in the same max-stack system to avoid additive card-like white flashes.
- Mecha E stale off-direction residue:
  - shortens only Skin67 E visual cleanup windows after the hook beat.
  - `E_Cast` 0.58 s, `E_ClawMarks` 0.34 s, `E_AxegrabCollision` 0.30 s, `E_Tar02` 0.48 s.
  - moving endpoint binding for the actual pull trail remains enabled.

## Preserved

- Classic, God-King and Dunkmaster routing is unchanged.
- Mecha W path is unchanged.
- Previous Mecha R mesh-UV fix remains enabled.
- Complete Hero/Skin/EntityModel lifecycle rebuild remains enabled.
- v0.30.4 BUILD_FIX1 `CreateFullMeshOverlay` forwarding method remains in source, so the earlier CS1061 build failure is not reintroduced.
