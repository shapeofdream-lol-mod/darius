# 资产说明（Asset Pipeline & Provenance）

本仓库是**纯源码仓库**：`.gitignore` 忽略整个 `assets/`、`about/*.png`、`tools/vgmstream/` 与 `build/`。
克隆仓库后需要在本机准备下列资产才能打包或运行；本文档说明它们从哪来、怎么生成、受什么约束。

## 1. 目录清单

| 路径 | 内容 | 规模（本地参考） | 入库 | 发布包 |
| --- | --- | --- | --- | --- |
| `assets/models/` | 4 个 GLB 模型（经典 / 神王 / 灌篮高手 / 机神） | 4 个 · 约 50 MB | ❌ | ✅ |
| `assets/animations/` | `.sodanim.json` 动画剪辑 + `manifest.json` + 轨道图 | 26 个 · 约 2.5 MB | ❌ | ✅ |
| `assets/retarget/` | 人形重定向配置 `darius_humanoid_profile.json` | 1 个 · 8 KB | ❌ | ✅ |
| `assets/icons/` | 技能/符文/装备/皮肤图标（含 `star_awoo.png`） | 40 个 · 约 2 MB | ❌ | ✅ |
| `assets/vfx/` | 保留的兼容 VFX 贴图（`gk_glow.png`、`gk_wisps_red.png` 等） | 8 个 · 约 0.5 MB | ❌ | ✅ |
| `assets/lol_vfx/` | 转换后的 Riot VFX 清单 + 贴图 + 网格 | 168 个 · 约 10 MB | ❌ | ✅ |
| `assets/audio/` | 技能 SFX：PCM16 WAV；语音：仅 22.05 kHz 单声道 OGG | 约 29 MB | ❌ | ✅ |
| `assets/raw_lol_audio/` | Riot Wwise `.wem` 原始源 + 映射清单 | 约 12 MB | ❌ | 仅 manifest |
| `assets/raw_lol_vfx_pass2/` | Riot `.tex` / `.skn` / `.bin` 原始提取物 | 约 51 MB | ❌ | ❌ |
| `about/icon.png`、`about/preview.png` | Workshop 列表缩略图 | 2 个 · 85 KB | ❌ | ✅ |

### 1.1 打包（发布什么）

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:PackageMod
```

`PackageMod` 会先删除旧 `build/`，再生成一个完整、干净的 Release snapshot：

```
build/
├── DariusPrototype.dll (+ .pdb)
├── about/
└── assets/
    ├── audio/        # 技能 SFX + voice OGG
    ├── animations/
    ├── icons/
    ├── lol_vfx/
    ├── models/
    ├── retarget/
    ├── vfx/
    └── raw_lol_audio/PASS2_MEDIA_MANIFEST.json
```

**发布只发 `build/`**。原始 WEM、Riot VFX 提取源、`src/`、`docs/`、`tools/` 都不进入发布包；`assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json` 是唯一保留的 raw 目录文件，因为运行时需要它完成事件路由。

语音发布契约是 **OGG-only**，`assets/audio` 中的语音资源统一为 `vo_*.ogg`。

`DeployMod` 把安装目录视为 snapshot：先清理 `<GameDir>\Mods\DariusPrototype`，再复制 `build/`，所以已删除的旧音频、贴图或 manifest 不会残留并覆盖新行为。

`VerifyPackageAssets` 只检查必需文件**存在**（`about/metadata.json`、`assets/models/darius.glb`、`assets/lol_vfx/darius_lol_vfx.json`、`assets/audio/flash.ogg`、`assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`）；数量与结构校验交给运行时日志，不在构建期做。

## 2. 来源

### 2.1 League of Legends 客户端（用户本机提供）

用于本轮音画重建的 WAD 由用户于 **2026-08-28** 从其本地 League 客户端提供：

| 文件 | SHA-256 |
| --- | --- |
| `Darius.wad.client` | `f4c7c4131c1e3924c3d8dc391fe3292100eb32ab1064a573ddce6e7c44011934` |
| `Darius.zh_CN.wad.client` | `72daa705f31eefaf590183514464a4deb6e2b93128432c382992e04e91b3af50` |

由此导出的内容：技能/皮肤贴图、Wwise 音频源、`VfxSystemDefinitionData`、SCB 网格、图标。

### 2.2 其他

- 模型/动画包：既有的 Darius 模型包（由外部工具从 Riot 资源导出为 GLB）。
- Shape of Dreams 本体资产：仅通过游戏目录的 `Managed` 程序集与运行时 API 引用，不复制入库。

## 3. 生成流程

### 3.1 音频

最终运行时资产只有两类：

- 技能 SFX（`lol_*` 等）：PCM16 WAV，由 `DariusMedia.LoadPcmWave` 同步读取，保证首次播放低延迟；
- 语音（`vo_*`）：22.05 kHz 单声道 Ogg Vorbis，最终文件统一为 `assets/audio/vo_*.ogg`。

原始源位于 `assets/raw_lol_audio/**/*.wem`，事件 → media id → 输出文件名的映射见 `assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`。重新生成资源时，将 `vgmstream-cli` 与所需编码工具加入 PATH；任何转换中间文件都应放在 `assets/audio` 之外并在转换完成后删除。

**运行时加载方式**：

- 技能 SFX：同步读取 PCM16 WAV；
- League Flash：启动时通过 `UnityWebRequestMultimedia` 异步解码 `flash.ogg`；
- 语音：第一次被事件选中时才异步解码对应 `vo_*.ogg`，缓存得到的 `AudioClip` 并继续本次播放；之后同一条语音直接命中缓存。

411 条语音的静态载荷约 8 MB。运行时 PCM 分配与实际触发过的语音集合相关，不再在启动阶段一次性解码全部语音。

### 3.2 VFX：Riot BIN/TEX/SCN → JSON + PNG

```
assets/raw_lol_vfx_pass2/{bins,tex,skn}
        │  tools/BuildDariusPass5AuthenticVfx.py（Python 3，纯标准库）
        ▼
assets/lol_vfx/darius_lol_vfx.json
assets/lol_vfx/textures/*.png
assets/lol_vfx/meshes/*.json
```

- 脚本自行解析 Riot `PROP` 二进制容器（`VfxSystemDefinitionData`），不依赖第三方库。
- 运行时由 `src/DariusPrototype/DariusLolVfxRuntime.cs` 解释这些参数，映射到 Unity `ParticleSystem` / 转换后的网格。
- **原则**：不受支持的 Riot 原语显式记录并跳过（fidelity gate），不得用自制效果冒充。

### 3.3 模型与动画

- `assets/models/*.glb`：外部导出产物；预期骨骼/动画/图元数量记录在 `src/DariusPrototype/DariusTravelerSystem.cs` 的 `DariusSkinSpec` 表中，运行时加载时校验并写日志。
- `assets/animations/*.sodanim.json` + `manifest.json`：预烘焙动画数据；运行时由 `src/DariusPrototype/UniversalAnimation/*` 重定向。

### 3.4 图标与 Workshop 素材

- `assets/icons/*.png`：从 League 客户端导出的技能/符文/装备/皮肤图标，文件名映射见 `src/DariusPrototype/DariusPrototypeIcons.cs`。
- `about/icon.png`、`about/preview.png`：Steam Workshop 列表图，不参与编译。

### 3.5 体积优化（发布体积）

当前 `build/` 约 **95 MB**，构成（本地实测）：

| 部分 | 大小 | 占比 |
| --- | --- | --- |
| `assets/models`（4 个 GLB） | 49.8 MB | 53% |
| `assets/audio`（技能 SFX + voice OGG） | 29.3 MB | 31% |
| `assets/lol_vfx` | 10.5 MB | 11% |
| animations + icons + vfx + about | 5.1 MB | 5% |

语音压缩与惰性分配已经完成：411 条语音最终只以 OGG 存在于运行时资产中，并在第一次使用时解码。技能 SFX 继续使用 PCM16 WAV，当前不在本轮资源改造范围内。

**模型（49.8 MB，当前最大项）**

实测 GLB 构成：内嵌 PNG 仅 0.3–2.4 MB，主体是网格与动画缓冲（bin 5.6–18.1 MB）。

- 可行：裁掉未使用的动画。`DariusSkinSpec` 每套皮肤只引用约 11–17 个片段，而 GLB 里含 22–59 个；离线裁剪后需在游戏内回归检查动画。
- 当前不可行：Draco / meshopt / `KHR_mesh_quantization`，因为当前自定义 GLB 解析没有对应解码支持。
- 小收益：优化内嵌图片。

**贴图**

`assets/lol_vfx` + `assets/icons` 共约 12.5 MB PNG，可在不改变运行时格式的前提下做无损压缩。

**红线**：不得为了体积重画 Riot 原始贴图（违反 fidelity gate 原则）。

## 4. 版权与合规

- League of Legends 的角色、皮肤、模型、动画、VFX、音频资产版权归 Riot Games 所有。本项目依据 Riot 的 Legal Jibber Jabber 政策创作；Riot Games 不认可、不赞助本项目。
- Shape of Dreams 及其资产版权归其开发者/发行商所有。
- 本项目为非官方粉丝互操作 Mod，不公开分发上述二进制资产；使用者需自行拥有合法的游戏与客户端资源。
- `assets/icons/star_awoo.png` 是用户提供的原始图片，必须保持字节一致，禁止重绘、裁剪、改色或替换。
