# 资产说明（Asset Pipeline & Provenance）

本仓库是**纯源码仓库**：`.gitignore` 忽略整个 `assets/`、`about/*.png` 与 `Tools/vgmstream/`。
克隆仓库后需要在本机准备下列资产才能构建；本文档说明它们从哪来、怎么生成、受什么约束。

## 1. 目录清单

| 路径 | 内容 | 规模（本地参考） | 是否入库 |
| --- | --- | --- | --- |
| `assets/models/` | 4 个 GLB 模型（经典 / 神王 / 灌篮高手 / 机神） | 4 个 · 约 50 MB | ❌ |
| `assets/animations/` | `.sodanim.json` 动画剪辑 + `manifest.json` + 轨道图 | 26 个 · 约 2.5 MB | ❌ |
| `assets/retarget/` | 人形重定向配置 `darius_humanoid_profile.json` | 1 个 · 8 KB | ❌ |
| `assets/icons/` | 技能/符文/装备/皮肤图标（含 `star_awoo.png`） | 40 个 · 约 2 MB | ❌ |
| `assets/vfx/` | 保留的兼容 VFX 贴图（`gk_glow.png`、`gk_wisps_red.png` 等） | 8 个 · 约 0.5 MB | ❌ |
| `assets/lol_vfx/` | 转换后的 Riot VFX 清单 + 贴图 + 网格 | 168 个 · 约 10 MB | ❌ |
| `assets/audio/` | 运行时 PCM16 WAV / OGG + 音频清单 | 555 个 · 约 160 MB | ❌ |
| `assets/raw_lol_audio/` | Riot Wwise `.wem` 原始源 + 映射清单 | 436 个 · 约 12 MB | ❌ |
| `assets/raw_lol_vfx_pass2/` | Riot `.tex` / `.skn` / `.bin` 原始提取物 | 903 个 · 约 51 MB | ❌ |
| `about/icon.png`、`about/preview.png` | Workshop 列表缩略图 | 2 个 · 85 KB | ❌ |

构建脚本对以下数量做**硬校验**（详见 `AGENTS.md` 第 4.3 节）：

- 38 个图标 + `assets/audio/flash.ogg`；
- 每个 `raw_lol_audio/**/*.wem` 都必须有对应的 `assets/audio/*.wav`；
- Riot VFX 清单 `systems = 164`、`pass5ConvertedSystems = 117`、PNG 贴图 ≥ 113、网格 ≥ 51、`assetErrors = 0`；
- 4 个 GLB 的图元/骨骼/动画数量与必需动画片段；
- 动画剪辑 ≥ 22，且 `manifest.json` 与重定向配置存在。

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
        │  Tools/PrepareDariusPass2Media.ps1（vgmstream-cli）
        ▼
assets/audio/*.wav（PCM16） + PASS2_MEDIA_READY.txt 标记
```

```bat
PREPARE_DARIUS_PASS2_MEDIA.bat
```

脚本行为：

1. 读取 `assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`（事件 → media id → 输出文件名的映射）。
2. 查找 `vgmstream-cli.exe`：PATH → `Tools/vgmstream/` → `%LOCALAPPDATA%`；找不到则从 GitHub 官方 release 自动下载到 `Tools/vgmstream/`（需要网络）。
3. 逐个解码 WEM；已存在且比源文件新的 WAV 会跳过。
4. 校验解码数量（skin SFX ≥ 60、full VO ≥ 350），写出 `PASS2_MEDIA_READY.txt`。

`BuildAndInstall.ps1` 在构建前会自动检测并调用它（当标记缺失或 WAV 数量不足时）。

### 3.2 VFX：Riot BIN/TEX/SCN → JSON + PNG

```
assets/raw_lol_vfx_pass2/{bins,tex,skn}
        │  Tools/BuildDariusPass5AuthenticVfx.py（Python 3，纯标准库）
        ▼
assets/lol_vfx/darius_lol_vfx.json
assets/lol_vfx/textures/*.png
assets/lol_vfx/meshes/*.json
```

- 脚本自行解析 Riot `PROP` 二进制容器（`VfxSystemDefinitionData`），不依赖第三方库。
- 运行时由 `Formal/DariusLolVfxRuntime.cs` 解释这些参数，映射到 Unity `ParticleSystem` / 转换后的网格。
- **原则**：不受支持的 Riot 原语显式记录并跳过（fidelity gate），不得用自制效果冒充。

### 3.3 模型与动画

- `assets/models/*.glb`：外部导出产物，构建时由 `BuildAndInstall.ps1` 校验骨骼/动画/图元数量。
- `assets/animations/*.sodanim.json` + `manifest.json`：预烘焙动画数据；运行时由 `Formal/UniversalAnimation/*` 重定向。

### 3.4 图标与 Workshop 素材

- `assets/icons/*.png`：从 League 客户端导出的技能/符文/装备/皮肤图标，构建脚本要求 38 个全部存在且 > 512 字节。
- `about/icon.png`、`about/preview.png`：Steam Workshop 列表图，不参与编译。

## 4. 版权与合规

- League of Legends 的角色、皮肤、模型、动画、VFX、音频资产版权归 **Riot Games** 所有。本项目依据 Riot 的 Legal Jibber Jabber 政策创作；Riot Games 不认可、不赞助本项目。
- Shape of Dreams 及其资产版权归其开发者/发行商所有。
- 本项目为**非官方粉丝互操作 Mod**，不公开分发上述二进制资产；使用者需自行拥有合法的游戏与客户端资源。
- `assets/icons/star_awoo.png` 是用户提供的原始图片，**必须保持字节一致**，禁止重绘、裁剪、改色或替换。
- 不要把这些资产上传到公开仓库、网盘或 Steam Workshop 包以外的任何位置。
