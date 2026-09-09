# Darius Traveler Mod

> 为 **Shape of Dreams** 制作的非官方角色 Mod：把《英雄联盟》的**诺克萨斯之手 · 德莱厄斯**作为一个独立的 Traveler 接入游戏，包含独立的技能、被动、星座（星效）、皮肤、动画、VFX、音效与中/英/日本地化。

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3790488345) · Workshop ID `3790488345` · 目标版本 `0.30.8-balance` · 详细版本历史见 [CHANGELOG.md](CHANGELOG.md)

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
| 音效与语音 | 由 Riot Wwise 源解码的 129 个技能音效 + 411 条 zh_CN 施法语音，按皮肤/技能随机选取 |
| 本地化 | zh_CN / en_US / ja_JP |
| 多人安全 | 远程玩家的 Darius 使用游戏原生网络状态，主机不写入他人存档 |

> 这是一个**跨引擎重建**：LoL 侧的贴图/音频是原始资源，但粒子发射、材质、几何与时序仍由 Shape of Dreams 的 Unity 侧实现驱动。

## 2. 环境要求

| 项 | 要求 |
| --- | --- |
| 游戏 | Steam 版 Shape of Dreams，Mod 目录位于 `<游戏目录>\Mods\` |
| 构建工具 | .NET SDK **8.0+**（`BuildAndInstall.ps1` 会检查 `dotnet --list-sdks`） |
| 脚本环境 | Windows + Windows PowerShell 5.1（`BUILD_DARIUS.bat` 调用 `powershell.exe`） |
| 可选 | Python 3（仅在重新生成 Riot VFX 载荷时需要） |
| 资产 | **本仓库不含任何二进制资产**，见 [docs/assets.md](docs/assets.md) |

## 3. 构建与安装

### 3.1 标准布局

```
<Steam>\steamapps\common\Shape of Dreams\
├── Shape of Dreams_Data\Managed\      # 编译引用（mscorlib / Assembly-CSharp / Dew.* / UnityEngine.*）
└── Mods\
    ├── TravelerBasicAttackVfxReplication.cs   # 跨 Mod 共享源码（见 3.4）
    └── DariusPrototype\                        # 本仓库
        ├── DariusPrototype.csproj
        └── BUILD_DARIUS.bat
```

### 3.2 一键构建

```bat
:: 在 Mod 根目录执行
BUILD_DARIUS.bat
```

等价于直接调用主脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\BuildAndInstall.ps1
```

构建流程：

1. 定位游戏目录（沿路径向上查找 / 读注册表 / 解析 `libraryfolders.vdf` / 读环境变量 `SOD_GAME_DIR`）。
2. 校验 `about\metadata.json`、`about\publishedfileid.txt`、Workshop 描述字节数（≤ 8000）。
3. 校验全部游戏 `Managed` 引用 DLL 存在。
4. 校验资产契约（图标、音频、VFX 清单、GLB 骨骼/动画数、动画 clip 数），必要时自动触发 Pass2 WEM→WAV 解码。
5. `dotnet build -c Release -p:GameDir=... -p:GameManagedDir=...`。
6. 把 `bin\Release\netstandard2.1\DariusPrototype.dll` 回写到 Mod 根目录，并打印 SHA256。

成功标志：控制台出现 `=== BUILD COMPLETE ===`，且根目录生成了 `DariusPrototype.dll`。
失败时查看根目录 `BUILD_LOG.txt`（脚本每次运行会重写它）。

### 3.3 自定义游戏路径

脚本按以下顺序自动探测，通常无需配置；非标准布局时设置环境变量：

```powershell
$env:SOD_GAME_DIR = 'D:\Games\Shape of Dreams'
```

### 3.4 构建前置：外部共享源码

`DariusPrototype.csproj` 通过链接引用仓库**之外**的一个文件：

```xml
<Compile Include="..\TravelerBasicAttackVfxReplication.cs" Link="TravelerBasicAttackVfxReplication.cs" />
```

在标准布局中它位于 `<游戏目录>\Mods\TravelerBasicAttackVfxReplication.cs`，由共享/基础角色 Mod 包提供。代码依赖其 `TravelerBasicAttackVfxReplication.Initialize()` 与 `.Broadcast(...)` API。

> ⚠️ 该文件**不在本仓库内**，当前快照中也未找到。缺少它时编译会直接失败（`CS2001`）。把它放回 `Mods\` 目录后再构建。

## 4. 仓库结构

```
.
├── DariusPrototype.cs              # ModBehaviour 入口（Awake/Start 引导、Harmony PatchAll、诊断）
├── DariusPrototype.csproj          # SDK 风格工程：netstandard2.1 / C# 9 / 无 NuGet 依赖
├── BuildAndInstall.ps1             # 构建主脚本（资产契约校验 → dotnet build → 回写 DLL）
├── BUILD_DARIUS.bat                # 双击入口，包装 BuildAndInstall.ps1
├── PREPARE_DARIUS_PASS2_MEDIA.bat  # 单独执行 WEM → WAV 媒体准备
├── Formal/                         # 全部游戏逻辑（40 个 .cs）
│   └── UniversalAnimation/         # 通用动画重定向运行时（5 个 .cs）
├── Tools/                          # 离线资产转换脚本（Python / PowerShell）
├── about/                          # Mod 加载器元数据与 Workshop 清单
├── assets/                         # 二进制资产（不纳入版本控制，见 docs/assets.md）
├── docs/                           # 设计、验证与测试文档
├── CHANGELOG.md                    # 版本历史
└── AGENTS.md                       # AI 编码代理工作手册
```

源码职责地图与开发约定见 [AGENTS.md](AGENTS.md)。

## 5. 文档索引

| 文档 | 内容 |
| --- | --- |
| [CHANGELOG.md](CHANGELOG.md) | 全部版本历史 |
| [AGENTS.md](AGENTS.md) | 源码结构地图、编码约定、构建契约、常见陷阱 |
| [docs/assets.md](docs/assets.md) | 资产来源、离线生成流程与版权说明 |
| [docs/BALANCE_PASS_2026-08-30.md](docs/BALANCE_PASS_2026-08-30.md) | 平衡性调整记录（历史快照） |
| [docs/PROGRESSION_PASS_2026-08-30.md](docs/PROGRESSION_PASS_2026-08-30.md) | 成长曲线与星座解锁重排（含 38 星效注册表，历史快照） |
| [docs/MECHA_VFX_HOTFIX2_CHANGELOG.md](docs/MECHA_VFX_HOTFIX2_CHANGELOG.md) | v0.30.5 机神视觉修复明细 |
| [docs/MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md](docs/MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md) | v0.30.5 游戏内验证清单 |
| [docs/MECHA_VFX_HOTFIX3_CHANGELOG.md](docs/MECHA_VFX_HOTFIX3_CHANGELOG.md) | v0.30.6 机神视觉修复明细 |
| [docs/MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md](docs/MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md) | v0.30.6 游戏内验证清单 |

## 6. 版本与已知问题

- **版本号单一真源**：只维护 `about/metadata.json` 的 `modVer`。构建脚本与运行时（`DariusModEnvironment.Version`）都从它读取，代码与脚本中不再有版本字面量。
- **构建契约由真源推导**：`BuildAndInstall.ps1` 的校验值来自 `DariusPrototype.csproj`、`Formal/*.cs` 与 `assets/**/*.json`，不再手工维护第二份清单。唯一保留的手写数值是脚本顶部的 `$ReleaseGates`（VFX 发布门槛与描述字节上限），见 [AGENTS.md](AGENTS.md) 第 4.3 节。
- **调试日志**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`（默认 `debug`）。日志写在共享 Mods 目录（Mod 目录上一层）。
- **无自动化测试**：仓库没有单元测试或 CI。唯一的自动校验是 `BuildAndInstall.ps1` 的元数据/资产契约；玩法正确性依赖游戏内人工验证（见 `docs/*_TEST_CHECKLIST.md`）。
- **外部静态校验报告已移除**：早期提交里的 `docs/*_STATIC_VALIDATION.txt` 由仓库外的工具产出、无法复现，且其断言会随代码漂移（曾声称 metadata 为 0.30.6-final，实际已是 0.30.8-balance），因此不再入库。
- **无法在无游戏环境构建**：编译需要游戏自带的 `Shape of Dreams_Data\Managed\*.dll`，以及 3.4 节的外部共享源码。
- **资产未入库**：克隆后必须自行准备资产包才能构建，见 [docs/assets.md](docs/assets.md)。

## 7. 资产与版权

- 本仓库**只包含源码、构建脚本与文本文档**；模型、贴图、音频等二进制资产一律不入库（见 `.gitignore`）。
- League of Legends 的角色/皮肤/模型/动画/VFX/音频资产版权归 **Riot Games** 所有。本项目依据 Riot 的 [Legal Jibber Jabber](https://www.riotgames.com/en/legal) 政策创作，Riot Games 不认可、不赞助本项目。
- Shape of Dreams 及其资产版权归其开发者/发行商所有。
- 这是一个非官方、粉丝制作的互操作/Mod 项目；使用者需自行拥有合法的游戏与 League 客户端资源。
- 源码许可证尚未选定。请勿假定第三方美术/模型/音频资产会沿用未来可能选定的源码许可证。

## 8. 免责声明

本项目可能违反游戏或第三方服务条款，使用风险自负。请勿用于商业分发，也不要公开传播从游戏或 League 客户端提取的原始资产。
