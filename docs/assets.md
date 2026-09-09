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
| `assets/audio/` | 技能 SFX：PCM16 WAV；语音：22.05 kHz 单声道 OGG | 554 个 · 约 29 MB | ❌ | ✅ |
| `assets/raw_lol_audio/` | Riot Wwise `.wem` 原始源 + 映射清单 | 436 个 · 约 12 MB | ❌ | ❌ |
| `assets/raw_lol_vfx_pass2/` | Riot `.tex` / `.skn` / `.bin` 原始提取物 | 903 个 · 约 51 MB | ❌ | ❌ |
| `about/icon.png`、`about/preview.png` | Workshop 列表缩略图 | 2 个 · 85 KB | ❌ | ✅ |

### 1.1 打包（发布什么）

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:PackageMod
```

`PackageMod` 会先删除旧 `build/`，再生成一个完整、干净的 Release snapshot：

```
build/
├── DariusPrototype.dll (+ .pdb)
├── about/        # metadata.json / description.txt / publishedfileid.txt / icon.png / preview.png
└── assets/       # 上表标 ✅ 的运行时子树 + raw_lol_audio/PASS2_MEDIA_MANIFEST.json
```

**发布只发 `build/`**。`src/`、`docs/`、`tools/`、`assets/raw_*` 都不进包——仅排除两个 `raw_*` 目录就省下约 63 MB。

`DeployMod` 同样把安装目录视为 snapshot：先清理 `<GameDir>\Mods\DariusPrototype`，再复制 `build/`。因此从旧版 WAV 迁移到 OGG、删除旧贴图或替换 manifest 时，不会被安装目录中残留文件覆盖新行为。部署目标不得与仓库根目录相同。

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

### 3.1 音频：WEM → WAV / OGG

```
assets/raw_lol_audio/**/*.wem
        │  vgmstream-cli（外部工具，无仓库内脚本）
        ▼
assets/audio/*.wav（PCM16，技能 SFX）
        │  ffmpeg（仅语音 vo_*）
        ▼
assets/audio/vo_*.ogg（22.05 kHz 单声道 Vorbis）
```

输出文件名 = 源文件基名 + 扩展名（运行时按基名查找）。批量转换示例：

```powershell
# 1) WEM -> PCM16 WAV
Get-ChildItem assets/raw_lol_audio -Recurse -Filter *.wem | ForEach-Object {
    & vgmstream-cli -o (Join-Path assets/audio ($_.BaseName + '.wav')) $_.FullName
}

# 2) 语音 -> 降采样 OGG（技能 SFX 保持 WAV）
Get-ChildItem assets/audio -Filter vo_*.wav | ForEach-Object {
    & ffmpeg -y -i $_.FullName -ac 1 -ar 22050 -c:a libvorbis -q:a 4 ($_.FullName -replace '\.wav$', '.ogg')
}
```

`vgmstream-cli` 从 [vgmstream releases](https://github.com/vgmstream/vgmstream/releases) 获取，放在 PATH 或 `tools/vgmstream/`（已 gitignore）。事件 → media id → 输出文件名的映射见 `assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`。

**运行时加载方式**：

- 技能 SFX（`lol_*` 等）：`DariusMedia.LoadPcmWave` 同步解析 RIFF，低延迟；
- League Flash：启动时通过 `UnityWebRequestMultimedia` 异步解码 `flash.ogg`；
- 语音（`vo_*`）：不做全量启动预加载。某条语音第一次被事件选中时，`DariusMedia` 才异步解码对应 OGG，缓存得到的 `AudioClip`，并在解码完成后继续本次播放。后续同一条语音直接命中缓存。

这样静态发布包仍只有约 8 MB 语音 OGG，同时运行时 PCM 分配与实际触发过的语音集合相关，不再在启动阶段一次性分配全部 411 条语音（旧方案约 70 MB PCM）。同一 key 正在解码时会去重请求，避免重复分配。

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
| `assets/audio`（技能 SFX WAV + 语音 OGG） | 29.3 MB | 31% |
| `assets/lol_vfx` | 10.5 MB | 11% |
| animations + icons + vfx + about | 5.1 MB | 5% |

**已做：语音压缩 + 惰性分配**。411 条语音从 44.1 kHz PCM16 WAV（138.3 MB）转为 22.05 kHz 单声道 Ogg Vorbis（8.1 MB）；运行时仅在实际触发时异步解码所需语音，不再启动时预分配全部 PCM。技能 SFX 保留 WAV（约 21 MB）是为低延迟。

**技能 SFX（可选，约 21 MB）**

若还要压，可对 `lol_*.wav` 同样转 OGG，并复用当前的按需 OGG 解码路径；代价是技能音效也从同步内存读取改为首次使用时异步解码，需要游戏内确认首次触发延迟是否可接受。

**模型（49.8 MB，当前最大项）**

实测 GLB 构成：内嵌 PNG 仅 0.3–2.4 MB，**主体是网格与动画缓冲**（bin 5.6–18.1 MB）。

- 可行：**裁掉未使用的动画**。`DariusSkinSpec` 每套皮肤只引用约 11–17 个片段，而 GLB 里含 22–59 个（机神 59 个 / 24 MB）。离线裁剪后需在游戏内回归检查动画。
- 当前不可行：Draco / meshopt / `KHR_mesh_quantization` —— 需要解码器或 accessor 支持，本项目的自定义 GLB 解析没有。
- 小收益：内嵌 PNG 转 JPEG 或更小 PNG（最多约 2 MB）。

**贴图**

`assets/lol_vfx` + `assets/icons` 共约 12.5 MB PNG，可用 `oxipng -o4` 或 `pngquant` 无损/近无损压缩（通常 20–40%）。运行时用 `ImageConversion.LoadImage` 解码，格式不变即可，无代码改动。

**红线**：不得为了体积用有损方式重画 Riot 原始贴图（违反 fidelity gate 原则）。

## 4. 版权与合规

- League of Legends 的角色、皮肤、模型、动画、VFX、音频资产版权归 **Riot Games** 所有。本项目依据 Riot 的 Legal Jibber Jabber 政策创作；Riot Games 不认可、不赞助本项目。
- Shape of Dreams 及其资产版权归其开发者/发行商所有。
- 本项目为**非官方粉丝互操作 Mod**，不公开分发上述二进制资产；使用者需自行拥有合法的游戏与客户端资源。
- `assets/icons/star_awoo.png` 是用户提供的原始图片，**必须保持字节一致**，禁止重绘、裁剪、改色或替换。
- 不要把这些资产上传到公开仓库、网盘或 Steam Workshop 包以外的任何位置。
