# Changelog

本文件记录 Darius Traveler Mod 的全部版本历史，最新版本在最上方。
条目按版本倒序排列；详细的视觉验证报告与测试清单见 `docs/`。

版本号对应 `about/metadata.json` 的 `modVer` 字段。

---

## v0.30.6-final - Mecha visual-fidelity hotfix 3

- 血怒白色气流保留，但将 `P_enraged/smoke` 与 `smoke1` 的尺寸和运动范围收紧到原来的 52%，避免气流扩散到角色数米之外。
- 血怒矩形白片定位为 `P_enraged/Distort`：该层使用纯白 `color-hold` 贴图并依赖 LoL distortion shader；Unity 普通粒子材质只会画成白色矩形，因此机神路径直接 fidelity-gate。
- Q 蓄力 `AOE/AOE_BG` 改按 Riot `[radius,1,1]` 的圆形 Quad 语义解释，两条平面轴都由 X 半径驱动，不再形成扁椭圆。
- Q 蓄力 `teamring_circle*` 的 X/Z 改为圆形比例；外层精确校准到 4.25m，与释放后的主环和实际攻击范围一致，内层保留为较小同心装饰。
- 继续过滤 Q 的绿色 `ant_dark1/ant_dark2` WindowPattern；继续拒绝无明确 submesh hash 的 Skin67 AttachedMesh 整身覆盖，避免全身绿色网格。
- 保留 E 旧方向残留清理、R Mesh UV 修复、模型生命周期整代重建以及 v0.30.4 BUILD_FIX1。

细节：`docs/MECHA_VFX_HOTFIX3_CHANGELOG.md` · 测试：`docs/MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md`

## v0.30.5-final - Mecha visual-fidelity hotfix 2

- 修复机神 Q 起手绿色 WindowPattern 网格（`ant_dark1` / `ant_dark2`）。
- Q 蓄力与命中表现半径归一化到 4.25m，与玩法判定半径一致。
- 移除 Skin67 无 hash AttachedMesh 的「整身覆盖」回退，修复战斗叠层后全身绿色网格。
- 过滤血怒 `Ground_Lighting`、max-stack `Temp_*` 与 `FireCards` 白片/卡片层。
- 收紧 Skin67 E 旧方向残留的清理窗口，保留可移动端点绑定。

细节：`docs/MECHA_VFX_HOTFIX2_CHANGELOG.md` · 测试：`docs/MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md`

## v0.30.3-final - Riot VFX + model lifecycle hotfix

- 修复最终版构建脚本的媒体校验问题。
- Flash 基础冷却固定为 3 秒；单位碰撞不再阻挡闪现穿过，静态世界碰撞仍会限制终点。
- Fixes the Q ring being too pale: Q Ring/Windup lifetime colour curves are no longer multiplied twice when `birthColor` is absent, and Q-only chroma/alpha compensation improves visibility without restoring the old global VFX brightness boost.
- Fixes the v0.26 video regression where W's long-lived attachment system became a large magenta cloud: persistent Riot ribbons are no longer emitted continuously without their animation-event gate, and negative-lifetime single CameraQuad layers are no longer converted into repeating 999-second particles.
- God-King R `BlastColumn*` directional ArbitraryQuad layers are temporarily omitted instead of being rendered with an incorrect Unity quad basis. Surrounding Riot mesh/flash impact layers remain.
- Keeps v0.26 `isUniformScale`, Ray/Beam omission, native Hero.icon and per-Skin previewImage fixes.

## v0.30.0-final - Pass 5 release convergence

- Completes runtime Riot VFX routing for Classic, God-King, Dunkmaster and Mecha. Dunkmaster/Mecha source BIN systems captured in Pass 2 are now converted and selected by the active skin instead of falling back globally to Classic.
- Final Darius Riot VFX manifest: 164 systems total; 117 Dunkmaster/Mecha systems added during Pass 5; 35 additional converted SCB meshes; zero asset-reference errors.
- Runtime now decodes exact Riot DXT1/DXT5 `.tex` resources used by the additional skins. No PNG approximation is generated for those source textures.
- Removed the obsolete hand-authored `DariusAdvancedVfx` basic-attack accent layer. Dedicated Dunkmaster/Mecha basic-attack/passive/Hemorrhage systems now use the Riot source; when a current Riot bank has no champion-specific effect, the mod leaves that layer absent rather than inventing a substitute.
- Retains Pass 1/2 R lock recovery, God-King Q timing/spin fixes, hover-hidden-model fix, 25% R baseline nerf, seven audio controls, equipment stars and 【嗷呜！！！】.

## v0.26.0 - Riot VFX fidelity / native cosmetic icon contract build

- Fixes the largest geometry regression visible in the v0.25 test video: Riot `isUniformScale` now treats X as the authored uniform scalar instead of interpreting all stored XYZ values as independent dimensions. This specifically prevents emitters such as God-King Q `Slashes` (`[8,600,150]`) from becoming giant triangular sheets.
- Keeps the v0.25 Unity velocity-curve mode repair, so randomized XYZ velocity/force no longer generates `Particle Velocity curves must all be in the same mode` errors.
- Removes the global additive/alpha brightness multiplier introduced in v0.25. Authored source colour is preserved; HDR/bloom differences are no longer hidden by multiplying every emitter into white/magenta blobs.
- Removes the false generic geometry path for Riot Trail/Ray/Beam primitives. Attached Camera/Arbitrary trails now use a dedicated world-space `TrailRenderer` following the actual animated attachment. World-static trails without a proven target binding and endpoint-dependent Ray/Beam/Laser emitters are fidelity-gated and logged instead of being drawn as incorrect polygons/fans.
- `VfxPrimitiveAttachedMesh` keeps the original Riot material hash mapping. Finite overlays now animate colour/alpha using the authored lifetime curve; persistent W weapon overlays remain attached to the real axe submesh.
- God-King R continues to use the restored original `Wolf_Mat` and original `Lion_*` Spell4 animation hierarchy. Authored-hidden Wolf/Throne materials now start on a genuinely transparent shader path so runtime visibility changes can actually reveal the original mesh.
- Restores the game's native cosmetic icon contract instead of repainting UI Images by reflection: `Hero_Darius.icon` and `Hero_Darius.mainColor` are set directly, and every `Skin_Darius_*` resource owns its own `Skin.previewImage`. Classic, God-King, Dunkmaster and Mecha reuse the previously validated dedicated 256x256 model-derived wardrobe thumbnails.
- `UI_HeroIcon.Setup` still renders the native Hero resource. A narrow postfix only neutralizes the exact serialized hero-icon Image RGB multiplier after verifying its sprite is `Hero_Darius.icon`; the old broad child-Image sprite-forcing scan is removed. Skin-list items rely on stock `Skin.previewImage` lookup.
- Converted Riot systems/resources remain a visual-validation interpreter in this build. Once primitive/material mappings are accepted visually, the same mappings can be baked offline for the release path.

## v0.24.0 - Riot BIN/SKN direct-conversion test

- Replaced the hand-built Q/W/E/R presentation path with a runtime interpreter for Riot `VfxSystemDefinitionData` extracted from the supplied current Darius WAD. The test payload contains 47 Classic/God-King skill systems, 113 converted original TEX textures and 16 converted original SCB meshes.
- Restored the God-King `Wolf_Mat` and `Throne_mat` submeshes that the old SKN->GLB export had dropped. `Wolf_Mat` stays authored-hidden at rest and is made visible only during the original God-King `Spell4`; the original `Lion_*` animation channels drive the beast geometry.
- God-King R now starts the converted `R_cast_axe`, `R_Cast_Wolf` and `R_Trail` systems and uses the converted `R_tar` target effect on impact. No procedural LineRenderer/quad substitute is layered over Q/W/E/R.
- Riot `VfxPrimitiveAttachedMesh` is deliberately reported as unsupported rather than faked as a billboard; it requires a future skinned-material overlay bridge. All other selected primitive families are interpreted through Unity ParticleSystem/converted meshes.
- Cast VO now uses a dedicated listener-space 2D AudioSource rather than the world-space skill-SFX route. Runtime logs include `[VO-PLAY]` and `[VO-PROBE]` with `isPlaying`/`timeSamples` checks.
- Q/W/E/R/Basic Attack/Dodge volume sliders, gameplay mechanics, numbers, Workshop identity and shared-Mods-directory logging remain unchanged.

## v0.23.0 - LoL-authentic Classic + God-King VFX/SFX/VO test

- Replaced the main Classic Q/W presentation masks with TEX resources converted directly from the user-supplied current League client.
- Added God-King skin15 source textures for Q ring/windup/blade, W blade, E wolf scratch, R impact/streak, glow/sparks/smoke and wired the major skill quads to skin-specific source assets.
- Replaced Q/W/E/R/basic-attack skill SFX for Classic and God-King with PCM16 files decoded from the matching League Wwise event/source pools. Variants remain separate and are selected randomly at runtime.
- Added zh_CN random cast VO pools for Q/W/R on Classic and Q/W/E/R on God-King. The current Classic Darius bank resolves no dedicated Apprehend cast VO event, so Classic E intentionally has no extra random line.
- Q/W/E/R/Basic Attack/Dodge ModConfig sliders continue to apply independently; cast VO follows the corresponding skill channel.
- Added an 8000-byte Steam Workshop description guard to the build script; current bilingual description is kept well under the limit.
- This is a cross-engine reconstruction: LoL source textures/audio are authentic, while particle emission/material/geometry/timing still run through the Shape of Dreams Unity-side VFX implementation.

## v0.22.7 - independent SFX mix controls

- Mod settings now expose independent Q/W/E/R/Basic Attack/Dodge SFX sliders from 0-200% (100% default). Playback reads the live value on every cue; the reused `q_swing` basic-attack cue is explicitly routed to Basic Attack rather than Q. Settings persist through ModConfig.

## v0.22.7 - per-skin GLB profiles + multiplayer constellation profile isolation

- Added formal outfits for 灌篮高手 德莱厄斯 and 机神 德莱厄斯. Each Skin resource is explicitly bound to its own GLB and its own verified animation map; no mesh/bone/animation-count template is shared across skins.
- Build/runtime checks verify each skin's expected primitive/bone/animation counts and required core clips. Mecha is intentionally handled as a 13-submesh / 246-bone / 59-animation rig, while Dunkmaster is a 1-submesh / 99-bone / 36-animation rig.
- All Darius GLBs now prefer authored inverseBindMatrices and force Bone4 skinning to reduce clothing/torso tearing on dense skin rigs.
- Multiplayer safety: the server-side saved-loadout reconciliation fallback now runs only for the process-local Darius hero. A host can no longer apply `DewSave.profileMain` / local preferred constellation page data to a remote player's Darius; remote heroes rely on the game's native networked StarEffect state.
- v0.22.5 R direct input and room hard-reset behavior is otherwise unchanged.

## v0.22.5 - Guillotine direct input + room hard reset

- R now intercepts the fragile stock ControlManager target wrapper and resolves its own legal target before entering the normal native completion path.
- Entering every `Room_*` performs a hard reset of transient Guillotine state (intent/execution/buffer/watchdogs/queued target/charge timing) while preserving Memory level and upgrades.
- Reset sweeps repeat over the early room-load frames so a freshly rebuilt R trigger is also cleaned.

## v0.22.4 - constellation UI isolation hotfix

- StarItem UI objects are pooled across travelers. Darius now clears its cached League icon binding whenever a pooled item is reassigned to a non-Darius StarEffect.
- Disabled direct repainting of shared constellation portrait/background widgets to prevent a custom traveler portrait from persisting into stock travelers.
- Gameplay/balance from v0.22.3 is unchanged.

## v0.22.3 - PvE balance / constellation persistence

- v0.22.3: runtime logs are written only to the shared game Mods directory (one level above character mod folders), so live log handles never touch the Workshop upload payload; native dynamic tooltip changes from v0.22.2 are retained.
- Raises Darius skill coefficients to the Shape of Dreams PvE scale: Q inner/outer 150%/225% AD, W +100% AD, R 260% AD without Hemorrhage or 180% base with stronger stack amplification, and Hemorrhage 7% AD per stack per tick.
- Rebalances Darius constellation effects into substantially stronger roguelite ranges instead of low single-digit bonuses.
- Adds profile Validate protection plus an Application.persistentDataPath backup for Darius constellation purchases/loadouts. If an early profile cleanup removes Darius stars and refunds Stardust, the custom data is restored and only that suspicious refund increase is neutralized.

## v0.21.0 - constellation expansion

- Uses a directional AOE basic attack: 1.75m, 110-degree sector, legal air attacks, multi-target hits, no target-lock requirement.
- Classic uses the restrained base VFX path, while God-King keeps its dedicated presentation layer; gameplay and balance remain skin-independent.
- Constellation expanded to 26 native StarEffects. Character branches include 斩断退路, 铁腕征服, 断头台的律法, 血路疾行, 真正的诺克萨斯之力, 扣篮王, 血债血偿 and 诺克萨斯之手.
- 铁腕征服 marks pulled enemies for 3s and amplifies Darius skill/Hemorrhage physical damage; 斩断退路 refunds W cooldown from Hemorrhage stacks.
- Workshop identity remains 3790488345.
- Root BUILD_ALL_MODS.bat is the preferred one-click build entry for the combined character package.
