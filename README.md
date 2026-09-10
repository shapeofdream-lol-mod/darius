# Darius Traveler Mod

> 为 **Shape of Dreams** 制作的非官方角色 Mod：把《英雄联盟》的**诺克萨斯之手 · 德莱厄斯**作为一个独立的 Traveler 接入游戏，包含独立的技能、被动、星座（星效）、皮肤、动画、VFX、音效与中/英/日本地化。

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3790488345) · Workshop ID `3790488345` · 版本历史见 [CHANGELOG.md](CHANGELOG.md)

---

## 1. 项目简介

本 Mod 通过游戏内置的 `DewMod` 加载器（`ModBehaviour` + Harmony）在运行时注册一个**独立**的 `Hero_Darius`，不覆盖任何原版 Traveler。所有 Hero / Skin / StarEffect / Memory 资源都在运行时构建，并接入游戏原生的存档、星座与多人网络路径。

核心内容：

| 模块 | 说明 |
| --- | --- |
| 角色与技能 | Q 大杀四方 / W 致残打击 / E 无情铁手 / R 诺克萨斯断头台 + 被动「出血」；独立 `AbilityInstance`、`SkillTrigger`、`Gem` 实现 |
| 皮肤 | 经典 / 神王 / 灌篮高手 / 机神，四套 GLB 各自绑定独立动画映射，无共享模板 |
| 星座（星效） | 38 个 `Se_Star_Darius_*` 定义，接入原生星座界面与存档；见 [docs/PROGRESSION_PASS_2026-08-30.md](docs/PROGRESSION_PASS_2026-08-30.md) |
| 召唤师技能 | 以 League 风格替换原版位移槽：Flash + Ghost |
| VFX | Riot `VfxSystemDefinitionData` 运行时解释器，164 个系统 / 114 张贴图 / 51 个网格 |
| 音效与语音 | 由 Riot Wwise 源解码的 129 个技能音效 + 411 条 zh_CN 施法语音；技能 SFX 保持同步 WAV，语音只使用 OGG 并按需异步解码缓存 |
| 本地化 | zh_CN / en_US / ja_JP |
| 多人安全 | 远程玩家的 Darius 使用游戏原生网络状态，主机不写入他人存档 |

> 这是一个**跨引擎重建**：LoL 侧的贴图/音频是原始资源，但粒子发射、材质、几何与时序仍由 Shape of Dreams 的 Unity 侧实现驱动。

## 2. 环境要求

| 项 | 要求 |
| --- | --- |
| 游戏 | Steam 版 Shape of Dreams，Mod 目录位于 `<游戏目录>\Mods\` |
| 构建工具 | .NET SDK **8.0+**（`dotnet build`，MSBuild） |
| 可选 | 任意 C# IDE；Python 3（仅在重新生成 Riot VFX 载荷时需要） |
| 资产 | **本仓库不含任何二进制资产**，见 [docs/assets.md](docs/assets.md) |

## 3. 构建与安装

### 3.1 标准布局

```
<Steam>\steamapps\common\Shape of Dreams\
├── Shape of Dreams_Data\Managed\              # 编译引用（mscorlib / Assembly-CSharp / Dew.* / UnityEngine.*）
└── Mods\
    ├── TravelerBasicAttackVfxReplication.cs   # 跨 Mod 共享源码（见 3.4）
    └── DariusPrototype\                        # 安装位置（由 DeployMod 写入，不是仓库）
```

仓库可以放在任何地方（本机为 `D:\Project\mod-sod-lol\darius`），与游戏目录解耦。`DeployMod` 会拒绝把部署目标设为仓库本身，以免清理安装目录时误删源码。

### 3.2 构建与打包

```powershell
# 只编译（需要游戏程序集）
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release

# 组装可发布包 -> build/（先清理旧 build，再生成完整 Release snapshot）
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:PackageMod

# 打包并部署到 <游戏目录>\Mods\DariusPrototype（先清理旧安装目录）
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:DeployMod
```

| 目标 | 产物 |
| --- | --- |
| `Build` | `src/DariusPrototype/bin/Release/netstandard2.1/DariusPrototype.dll` |
| `PackageMod` | 干净的 `build/` snapshot：DLL + `about/` + 运行时资产；不含 raw 提取资产，仅保留运行时需要的 `raw_lol_audio/PASS2_MEDIA_MANIFEST.json` |
| `DeployMod` | 用上面的 package snapshot 完整替换 `<GameDir>\Mods\DariusPrototype` |

**发布只发 `build/` 目录**，不要发仓库。语音发布格式固定为 OGG。包内容与体积构成见 [docs/assets.md](docs/assets.md) 第 1.1 与 3.5 节。

### 3.3 指定游戏路径

按以下顺序解析，任一命中即可：

```powershell
# 1) 命令行参数
dotnet build -p:GameDir="D:\Steam\steamapps\common\Shape of Dreams"

# 2) 仓库根的 Directory.Build.props（已 gitignore）
#    <Project><PropertyGroup><GameDir>D:\Steam\steamapps\common\Shape of Dreams</GameDir></PropertyGroup></Project>

# 3) 环境变量
$env:SOD_GAME_DIR = 'D:\Steam\steamapps\common\Shape of Dreams'
```

解析不到时 `CheckGameInstall` 直接报错。`ModsDir` 默认取 `<GameDir>\Mods`，可用 `-p:ModsDir=` 覆盖。

### 3.4 构建前置：外部共享源码

csproj 从 `<游戏目录>\Mods\TravelerBasicAttackVfxReplication.cs` 链接引用一个仓库**之外**的文件：

```xml
<Compile Include="$(SharedSource)" Link="TravelerBasicAttackVfxReplication.cs" Condition="Exists('$(SharedSource)')" />
```

代码依赖其 `TravelerBasicAttackVfxReplication.Initialize()` 与 `.Broadcast(...)` API。

> ⚠️ 该文件**不在本仓库内**，当前快照中也未找到。缺失时 `CheckGameInstall` 会直接报错。把它放回 `Mods\` 目录后再构建。

## 4. 仓库结构

```
.
├── src/DariusPrototype/            # 全部 C# 源码 + DariusPrototype.csproj
│   └── UniversalAnimation/         # 通用动画重定向运行时
├── tools/                          # 离线资产转换（Python）
├── about/                          # Mod 加载器元数据与 Workshop 清单
├── assets/                         # 二进制资产（不纳入版本控制，见 docs/assets.md）
├── docs/                           # 设计、验证与测试文档
├── build/                          # 可发布包（gitignore，由 PackageMod 生成）
├── CHANGELOG.md                    # 版本历史
└── AGENTS.md                       # AI 编码代理工作手册
```

源码职责地图与开发约定见 [AGENTS.md](AGENTS.md)。

## 5. 文档索引

| 文档 | 内容 |
| --- | --- |
| [CHANGELOG.md](CHANGELOG.md) | 0.1.0 起的版本历史（Keep a Changelog 格式） |
| [docs/archive/CHANGELOG-legacy.md](docs/archive/CHANGELOG-legacy.md) | 0.30.x 及更早的 Mod 历史（归档，不再维护） |
| [AGENTS.md](AGENTS.md) | 源码结构地图、编码约定、构建定义、常见陷阱 |
| [docs/assets.md](docs/assets.md) | 资产来源、打包内容、生成流程、体积优化与版权 |
| [docs/BALANCE_PASS_2026-08-30.md](docs/BALANCE_PASS_2026-08-30.md) | 平衡性调整记录（历史快照） |
| [docs/PROGRESSION_PASS_2026-08-30.md](docs/PROGRESSION_PASS_2026-08-30.md) | 成长曲线与星座解锁重排（含 38 星效注册表，历史快照） |
| [docs/MECHA_VFX_HOTFIX2_CHANGELOG.md](docs/MECHA_VFX_HOTFIX2_CHANGELOG.md) | v0.30.5 机神视觉修复明细 |
| [docs/MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md](docs/MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md) | v0.30.5 游戏内验证清单 |
| [docs/MECHA_VFX_HOTFIX3_CHANGELOG.md](docs/MECHA_VFX_HOTFIX3_CHANGELOG.md) | v0.30.6 机神视觉修复明细 |
| [docs/MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md](docs/MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md) | v0.30.6 游戏内验证清单 |

## 6. 版本与已知问题

- **版本号单一真源**：只维护 `about/metadata.json` 的 `modVer`。运行时（`DariusModEnvironment.Version`）从它读取；README 不再复制“当前版本”字面量。
- **构建就是 `dotnet build`**：没有 `.bat` / `.ps1` 构建或校验脚本，游戏路径通过 `GameDir` 属性注入（见 3.3）。
- **发布只发 `build/`**：`PackageMod` 会先清理 `build/` 再生成完整 snapshot；raw 提取资产不进包，`PASS2_MEDIA_MANIFEST.json` 是运行时所需的唯一例外。
- **语音 OGG-only**：运行时只从 `vo_*.ogg` 加载语音；启动阶段不做语音预加载。
- **部署是完整替换**：`DeployMod` 先清理 `<GameDir>\Mods\DariusPrototype`，再复制 package，因此旧版本已删除的音频、贴图或 manifest 不会残留并覆盖新资源。
- **构建只做轻量边界检查**：`CheckGameInstall`（游戏程序集 / 外部共享源码）与 `VerifyPackageAssets`（打包前必需资产存在）。数量与结构校验交给运行时日志。
- **发布体积约 95 MB**：411 条语音约 8 MB；运行时按需异步解码并只缓存实际触发过的语音，不在启动时批量解码。当前最大静态项是模型 49.8 MB。
- **调试日志**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`（默认 `debug`）。日志写在共享 Mods 目录（Mod 目录上一层）。
- **无自动化测试**：仓库没有单元测试或 CI，唯一的自动校验是编译本身；玩法正确性依赖游戏内人工验证（见 `docs/*_TEST_CHECKLIST.md`）。
- **无法在无游戏环境构建**：编译需要游戏自带的 `Shape of Dreams_Data\Managed\*.dll`，以及 3.4 节的外部共享源码。
- **资产未入库**：克隆后必须自行准备资产包才能打包或运行，见 [docs/assets.md](docs/assets.md)。

## 7. 资产与版权

- 本仓库**只包含源码、构建定义与文本文档**；模型、贴图、音频等二进制资产一律不入库（见 `.gitignore`）。
- League of Legends 的角色/皮肤/模型/动画/VFX/音频资产版权归 **Riot Games** 所有。本项目依据 Riot 的 [Legal Jibber Jabber](https://www.riotgames.com/en/legal) 政策创作，Riot Games 不认可、不赞助本项目。
- Shape of Dreams 及其资产版权归其开发者/发行商所有。
- 这是一个非官方、粉丝制作的互操作/Mod 项目；使用者需自行拥有合法的游戏与 League 客户端资源。
- 源码许可证尚未选定。请勿假定第三方美术/模型/音频资产会沿用未来可能选定的源码许可证。

## 8. 免责声明

本项目可能违反游戏或第三方服务条款，使用风险自负。请勿用于商业分发，也不要公开传播从游戏或 League 客户端提取的原始资产。
