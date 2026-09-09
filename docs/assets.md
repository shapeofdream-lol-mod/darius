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
| `assets/audio/` | 运行时 PCM16 WAV / OGG + 音频清单 | 555 个 · 约 160 MB | ❌ | ✅ |
| `assets/raw_lol_audio/` | Riot Wwise `.wem` 原始源 + 映射清单 | 436 个 · 约 12 MB | ❌ | ❌ |
| `assets/raw_lol_vfx_pass2/` | Riot `.tex` / `.skn` / `.bin` 原始提取物 | 903 个 · 约 51 MB | ❌ | ❌ |
| `about/icon.png`、`about/preview.png` | Workshop 列表缩略图 | 2 个 · 85 KB | ❌ | ✅ |

### 1.1 打包（发布什么）

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -t:PackageMod
```

生成 `build/`：

```
build/
├── DariusPrototype.dll (+ .pdb)
├── about/        # metadata.json / description.txt / publishedfileid.txt / icon.png / preview.png
└── assets/       # 仅上表标 ✅ 的运行时子树
```

**发布只发 `build/`**。`src/`、`docs/`、`tools/`、`assets/raw_*` 都不进包——仅排除两个 `raw_*` 目录就省下约 63 MB。

`VerifyPackageAssets` 只检查必需文件**存在**（`about/metadata.json`、`assets/models/darius.glb`、`assets/lol_vfx/darius_lol_vfx.json`、`assets/audio/flash.ogg`）；数量与结构校验交给运行时日志，不在构建期做。

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

### 3.1 音频：WEM → WAV

```
assets/raw_lol_audio/*.wem
        │  vgmstream-cli（外部工具，无仓库内脚本）
        ▼
assets/audio/*.wav（PCM16）
```

输出文件名 = 源文件基名 + `.wav`（运行时按基名查找）。批量转换示例：

```powershell
Get-ChildItem assets/raw_lol_audio -Recurse -Filter *.wem | ForEach-Object {
    & vgmstream-cli -o (Join-Path assets/audio ($_.BaseName + '.wav')) $_.FullName
}
```

`vgmstream-cli` 从 [vgmstream releases](https://github.com/vgmstream/vgmstream/releases) 获取，放在 PATH 或 `tools/vgmstream/`（已 gitignore）。事件 → media id → 输出文件名的映射见 `assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`。

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

当前 `build/` 约 **225 MB**，构成（本地实测）：

| 部分 | 大小 | 占比 |
| --- | --- | --- |
| `assets/audio`（552 个 PCM16 WAV） | 159.5 MB | 71% |
| `assets/models`（4 个 GLB） | 49.8 MB | 22% |
| `assets/lol_vfx` | 10.5 MB | 5% |
| animations + icons + vfx + about | 5.1 MB | 2% |

**音频（最大项）**

现状：16-bit / 44.1 kHz / 单声道 PCM WAV，运行时由 `DariusMedia.LoadPcmWave` 同步解析 RIFF。可选方案：

1. **降采样 / 降位深（无代码改动）**：转 22.05 kHz 单声道 WAV，约省 50%（≈80 MB）。语音与短音效基本听不出差别，代价是高频损失。
2. **转 OGG Vorbis（≈1/8～1/10，≈16–20 MB）**：Unity 只能通过 `UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS)` **异步**加载。`DariusMedia` 已有这条路径（`flash.ogg`），但另外 540 个片段走同步 WAV 路径；要全量转 OGG，必须改成「启动时异步预加载 + 同步取缓存」，属于运行时改动，需游戏内验证。
3. **混合**：长语音转 OGG（异步预加载），短技能音效保留 WAV（低延迟、零改动）。

工具：

```powershell
ffmpeg -i in.wav -ac 1 -ar 22050 -c:a libvorbis -q:a 4 out.ogg
# 或 oggenc2 -q 4 in.wav -o out.ogg
```

**模型（49.8 MB）**

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
