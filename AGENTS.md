# AGENTS.md

> 本文件是 AI 编码代理（Codex / Claude / DeepSeek 等）在本仓库工作时的操作手册。
> 人类贡献者也可把它当作开发约定参考。**动手前请先读第 2、4、7 节。**

---

## 1. 项目速览

- **是什么**：Shape of Dreams（Unity / Steam）的非官方角色 Mod，把 League of Legends 的德莱厄斯作为独立 `Hero_Darius` Traveler 接入游戏。
- **形态**：单个 .NET 类库 `DariusPrototype.dll` + 一个 `about\` 元数据目录 + 一组二进制资产（不入库）。
- **加载方式**：游戏内置 `DewMod` 加载器（`ModBehaviour`）+ Harmony 补丁，全部资源在运行时构建。
- **语言**：C# 9.0 / `netstandard2.1`。
- **规模**：`Formal/` 共 45 个 `.cs`（40 个在根目录 + 5 个在 `UniversalAnimation/`）、约 106 万字符；最大的三个文件是 `DariusTravelerSystem.cs`（~192 KB）、`DariusConstellations.cs`（~106 KB）、`DariusLolVfxRuntime.cs`（~84 KB）。
- **没有测试框架、没有 CI**。验证 = 构建契约 + 游戏内人工清单。

## 2. 技术栈与硬约束

| 约束 | 值 | 说明 |
| --- | --- | --- |
| 目标框架 | `netstandard2.1` | 必须匹配游戏 Unity 运行时 |
| 语言版本 | C# 9.0 | 不要使用 C# 10+ 语法（file-scoped namespace、record struct、全局 using 等） |
| 编译方式 | `NoStdLib` + `DisableImplicitFrameworkReferences` | 所有程序集引用都来自游戏 `Managed` 目录 |
| 依赖 | **零 NuGet 包** | 不允许新增 NuGet/PackageReference；需要新程序集时改 csproj 的 `<Reference>` 并同步构建脚本 |
| 平台 | Windows | 构建脚本是 PowerShell 5.1 + `.bat` |
| 编码 | UTF-8 | 仓库内 BOM 状态不统一（如 `DariusTravelerSystem.cs` 有 BOM）；新建文件用**无 BOM UTF-8**，行尾 LF |

**不要做的事**：升级 TargetFramework、开启 implicit usings、引入第三方库、把代码拆成新项目、改 `AssemblyName`（`about\metadata.json` 与构建脚本都写死了 `DariusPrototype.dll`）。

## 3. 源码职责地图

### 3.1 入口与构建

| 文件 | 职责 |
| --- | --- |
| `DariusPrototype.cs` | `DariusPrototypeMod : ModBehaviour` 入口。`Awake` 早期引导 → `Start` 中 `harmony.PatchAll()`、注册生命周期/兼容层、启动自愈协程。**引导路径的异常必须被捕获且不得中断核心注册**（见文件内注释） |
| `DariusPrototype.csproj` | SDK 风格工程，默认递归包含 `**/*.cs`。新增 `.cs` 无需登记；但**不要**在 `assets/`、`docs/` 下放 `.cs` |
| `BuildAndInstall.ps1` | 构建主脚本：定位游戏目录 → 校验元数据（版本/ID/描述）→ **从 csproj 推导**并校验游戏引用 → **从代码/清单推导**并校验资产契约 → `dotnet build` → 回写 DLL |
| `BUILD_DARIUS.bat` | 双击入口，包装 `BuildAndInstall.ps1`，输出 `LAUNCH_LOG.txt` |
| `PREPARE_DARIUS_PASS2_MEDIA.bat` | 单独执行 `Tools\PrepareDariusPass2Media.ps1`（WEM → WAV） |

### 3.2 技能与战斗

| 文件 | 职责 |
| --- | --- |
| `Ai_Darius_Decimate.cs` | Q 大杀四方，`AbilityInstance` 服务端执行体 |
| `Ai_Darius_CripplingStrike.cs` | W 致残打击 |
| `Ai_Darius_Apprehend.cs` | E 无情铁手 |
| `Ai_Darius_NoxianGuillotine.cs` | R 诺克萨斯断头台（`OnDestroy` 有意隐藏基类成员，编译有 CS0114 警告） |
| `St_Darius_*.cs` | 对应技能的 `SkillTrigger` 定义（客户端触发/准备） |
| `St_D_Darius_Hemorrhage.cs` | 被动「出血」的 Identity `SkillTrigger` |
| `Gem_Darius_Hemorrhage.cs` | 出血 `Gem` |
| `DariusMemoryScaling.cs` | Memory 无限成长曲线 |
| `DariusMemoryEffectBridge.cs` | 直接执行桥：不生成 `AbilityInstance` 预制体，规避运行时 Actor parenting 限制 |
| `DariusNativeAttack.cs` | 原生普攻接入 |
| `DariusNativeDisplacement.cs` | Flash / Ghost 的原生位移接管 |
| `DariusSummonerSkills.cs` | 召唤师技能槽替换 |
| `DariusSlowHelper.cs` | 减速工具 |
| `DariusHemorrhageHud.cs` | 出血层数 HUD |
| `DariusEnemyClassifier.cs` | 敌人分类（精英 / Boss），供星座倍率使用 |
| `DariusRInputGuard.cs` | R 输入守卫：监听 `ControlManager` 层，自行解析合法目标 |
| `DariusTriggerConfigRuntimeEditor.cs` | 写入 `TriggerConfig` 的私有后备字段 |

### 3.3 资源、表现与本地化

| 文件 | 职责 |
| --- | --- |
| `DariusTravelerSystem.cs` | **核心**：独立 Traveler 的 Hero/Skin/EntityModel/资源构建、场景切换后的整代重建 |
| `DariusSkinSystem.cs` | 皮肤与动画层；技能发起的动画请求桥（含普攻 VFX 网络广播） |
| `DariusMedia.cs` | 运行时媒体加载：VFX 贴图 + 技能音频（WAV/OGG） |
| `DariusLolVfxRuntime.cs` | Riot `VfxSystemDefinitionData` 运行时解释器；不支持的原语必须显式记录并跳过（fidelity gate） |
| `DariusVoiceRuntime.cs` | 施法语音路由；**不得**伪造或跨皮肤替代语音 |
| `DariusPrototypeIcons.cs` | 图标加载（`assets/icons/*.png`） |
| `DariusFormalRegistry.cs` | 正式 Memory/Essence 的运行时资源注册（GUID 必须稳定） |
| `DariusDejaVuRegistry.cs` | 把测试 Memory/Essence 注入原生 Deja Vu 起手装备系统 |
| `DariusRuntimeResourceCompatibility.cs` | 让运行时资源参与 Dew 的全部查找路径（Harmony 兼容层） |
| `DariusConstellations.cs` | 星效效果定义 |
| `DariusEquipmentConstellations.cs` | 装备类星效 |
| `DariusConstellationPersistence.cs` | 星座购买/存档持久化 + `persistentDataPath` 备份与回滚保护 |
| `DariusFormalLocalization.cs` / `DariusEnglishLocalization.cs` / `DariusJapaneseLocalization.cs` | zh_CN / en_US / ja_JP 文案与动态 tooltip |
| `DariusAudioSettings.cs` | ModConfig 音量字段（Q/W/E/R/普攻/闪避/语音） |

### 3.4 基础设施

| 文件 | 职责 |
| --- | --- |
| `DariusLog.cs` | 统一日志。写入**共享 Mods 目录**（比 Mod 目录高一层），避免日志句柄污染 Workshop 上传包 |
| `DariusDiagnostics.cs` | 首测用的运行时快照（flight recorder） |
| `DariusModEnvironment.cs` | 以来源无关的方式解析 Mod 物理目录（本地 / Workshop）；`Version` 从 `about\metadata.json` 读取版本 |
| `DariusResourceIds.cs` | 共享资源 GUID 的唯一定义处（`DariusFormalRegistry` 与 `DariusDejaVuRegistry` 都引用它） |
| `DariusModLifecycle.cs` | 区分真正的 `DewMod` 热卸载与普通场景销毁 |
| `Formal/UniversalAnimation/*.cs` | 通用动画重定向运行时：`AnimationLibrary`（加载）、`UniversalRetargeter`（重定向）、`DariusRetargetApi`（技能侧桥）、`ModelUtils`、`RuntimeLog` |

### 3.5 离线工具

| 文件 | 职责 |
| --- | --- |
| `Tools/PrepareDariusPass2Media.ps1` | 用 vgmstream 把 `assets/raw_lol_audio/*.wem` 解码为 `assets/audio/*.wav`；缺少 vgmstream 时自动下载 |
| `Tools/BuildDariusPass5AuthenticVfx.py` | 解析 Riot `PROP` BIN，生成 `assets/lol_vfx/darius_lol_vfx.json` 与贴图/网格载荷 |

## 4. 构建与验证

### 4.1 前置条件（缺一不可）

1. 本机安装 **Shape of Dreams**，且存在 `<游戏目录>\Shape of Dreams_Data\Managed\`。
2. 安装 **.NET SDK 8.0+**。
3. 仓库位于 `<游戏目录>\Mods\DariusPrototype\`（或设置 `SOD_GAME_DIR`）。
4. `<游戏目录>\Mods\TravelerBasicAttackVfxReplication.cs` 存在——**跨 Mod 共享源码，不在本仓库内**（`DariusPrototype.csproj` 用 `..\` 链接引用，缺失会导致 `CS2001`）。
5. 二进制资产齐备（见第 6 节与 `docs/assets.md`）。

### 4.2 构建

```bat
BUILD_DARIUS.bat
```

```powershell
# 等价的直接调用
powershell -NoProfile -ExecutionPolicy Bypass -File .\BuildAndInstall.ps1
```

产物：根目录 `DariusPrototype.dll`（同时生成 `.pdb`）。日志：根目录 `BUILD_LOG.txt`。

### 4.3 构建契约（由真源推导，不要手写第二份）

`BuildAndInstall.ps1` 的校验值**全部从单一真源推导**，不存在需要手工同步的第二份清单：

| 校验项 | 真源 |
| --- | --- |
| Mod 版本 | `about\metadata.json` 的 `modVer`（脚本与运行时共用） |
| Workshop ID | `about\publishedfileid.txt`（只校验格式为数字） |
| 游戏程序集引用 | `DariusPrototype.csproj` 的 `<Reference>` + `HintPath`（自动覆盖 `UnityEngine.UI`） |
| 图标集合 | `Formal\DariusPrototypeIcons.cs` 的 `FileNames` 映射 |
| VFX 贴图集合 | `Formal\DariusMedia.cs` 的 `PreloadAll()` 纹理表 |
| GLB 结构 / 必需动画 | `Formal\DariusTravelerSystem.cs` 的 `DariusSkinSpec` 表 |
| 动画剪辑 | `assets\animations\manifest.json` |
| 旧版音频集合 | `assets\audio\LOL_AUDIO_MANIFEST.json` |
| Pass2 解码完整性 | `assets\raw_lol_audio\**\*.wem` 与同名 WAV 一一对应 |

唯一保留的手写数值集中在脚本顶部的 `$ReleaseGates`：VFX systems `164` / pass5 `117` / 贴图 ≥ `113` / 网格 ≥ `51` / 描述 `8000` 与 `7500` 字节。它们无法从代码或数据推导，属于发布门槛；**不要**把已经可推导的值加回该块。

若任一真源格式变化，脚本会**显式报错**（例如 `Could not locate the FileNames map`），而不是静默跳过校验。改这些格式时必须同步更新脚本里的解析正则。

### 4.4 验证方式

| 层次 | 手段 |
| --- | --- |
| 编译 | `dotnet build`（由 `BuildAndInstall.ps1` 驱动）——**这是唯一的权威编译验证** |
| 结构/契约 | `BuildAndInstall.ps1` 的资产与元数据检查（全部从真源推导） |
| 游戏内 | `docs/MECHA_VFX_HOTFIX*_TEST_CHECKLIST.md` 等人工清单 |
| 静态自检 | 没有仓库内的检查脚本。若无法编译，只能做括号配平、引用/路径存在性、JSON 可解析等自检，并明确声明「未编译验证」 |

> PowerShell 脚本改动可用 `[System.Management.Automation.Language.Parser]::ParseFile()` 做语法自检；脚本里的推导逻辑（正则解析真源）可以单独复制执行，与预期值逐一比对。

> 在没有游戏环境 / 没有 .NET SDK 的机器上，你**无法**完成编译验证。此时应明确说明「未编译验证」，并至少做括号配平、引用存在性、JSON 可解析等静态自检。

## 5. 编码约定

1. **命名**：类型/方法 PascalCase；私有字段 `_camelCase`；类名统一带 `Darius` 前缀（技能类沿用游戏约定 `Ai_*` / `St_*` / `Gem_*`）。
2. **日志**：统一用 `DariusLog`（`Info` / `Exception` 等），不要新增 `Debug.Log` 或 `Console.WriteLine` 作为长期输出。
3. **异常处理**：引导、Harmony 补丁、资源注册路径必须 `try/catch` 并记录；**任何表现层失败都不能阻断 `Hero_Darius` 注册**（这是历史事故的根因）。
4. **反射**：访问游戏私有成员是常态，必须做存在性/null 检查并提供回退路径，不要假设字段名永远不变。
5. **Harmony 补丁**：集中放在 `*Registry` / `*Compatibility` / `*Lifecycle` 类中；`PatchAll` 抛错必须被吞并记录。
6. **性能**：`ConvertDescriptionNodesToText` 等每帧/多次调用的路径必须缓存（现有实现已缓存，勿破坏）。
7. **注释**：解释「为什么」而非「是什么」；涉及 Riot 原语/游戏版本差异的地方必须写明来源与限制。
8. **不要顺手重构**：本仓库文件极大且强耦合，未经要求不要重排、重命名或格式化无关代码。
9. **版本号单一真源**：只改 `about\metadata.json` 的 `modVer`。构建脚本与运行时（`DariusModEnvironment.Version`）都从它读取；**禁止**在任何 `.cs` / `.ps1` / `.bat` 里再写版本字面量。
10. **共享 GUID**：资源 GUID 只在 `Formal/DariusResourceIds.cs` 定义一次，其他文件引用常量；这些值持久化在存档与网络身份中，不得更改。
11. **日志级别**：`DariusLog.Minimum` 或环境变量 `DARIUS_LOG_LEVEL`（`debug|info|warn|error|off`，默认 `debug`）。`EXCEPTION` 不受过滤。

## 6. 资产与版权红线

1. **禁止把二进制资产提交到 Git**。`.gitignore` 已忽略整个 `assets/`、`about/*.png` 与 `Tools/vgmstream/`。`git status` 中不应出现任何 `.glb` / `.wav` / `.wem` / `.tex` / `.skn` / `.png` / `.dll`。
2. **禁止公开分发 Riot 原始资产**。League 资产版权归 Riot Games；本项目依据 Riot 的 Legal Jibber Jabber 政策创作，仅限本地互操作使用。
3. **不要伪造替代 VFX**。Riot 原语不受支持时应显式记录并跳过（fidelity gate），而不是用自制贴图/几何冒充——这是本项目的核心原则。
4. **不要重绘用户提供的资源**。例如 `assets/icons/star_awoo.png` 必须保持与用户原图字节一致，禁止裁剪/改色/风格化。
5. **不要跨皮肤替代语音或音效**：缺少对应资源时宁可留空。
6. 资产来源与离线生成流程见 `docs/assets.md`；新增资产时必须同步更新该文档。

## 7. 常见陷阱

- **`TravelerBasicAttackVfxReplication.cs` 不在仓库里**：`DariusPrototype.cs:38` 与 `DariusSkinSystem.cs` 依赖它。构建前确认它位于 `Mods\` 目录。
- **版本号只有一个真源**：`about\metadata.json` 的 `modVer`。构建脚本与 `DariusModEnvironment.Version` 都读它；`DariusPrototype.cs` / `BuildAndInstall.ps1` / `BUILD_DARIUS.bat` 里原有的版本字面量已删除，不要加回去。
- **`$ReleaseGates` 是脚本里唯一允许的手写数值块**（VFX 发布门槛与描述字节上限）。新增校验应从真源推导，而不是往这个块里加常量。
- **共享 GUID 只在 `Formal/DariusResourceIds.cs` 定义**：改值会破坏存档与网络身份；`DariusFormalRegistry` / `DariusDejaVuRegistry` 引用它而不是复制字面量。
- **日志级别**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`（默认 `debug`，即原有行为）；`EXCEPTION` 始终写入。
- **构建脚本会先删后建**：`BuildAndInstall.ps1` 会删除根目录 `DariusPrototype.dll` / `.pdb` 以及整个 `bin/`、`obj/`，再重新编译。不要手动往这些位置放文件。
- **日志写在共享 Mods 目录**，不在 Mod 目录内。找日志时往上一层看。
- **`assets/` 缺失即构建失败**：校验值从代码与清单推导，删减/改名资产会直接失败；正常情况下**不需要**改脚本。
- **真源格式变化会让脚本显式报错**：图标映射、`PreloadAll()` 纹理表、`DariusSkinSpec` 表、各 manifest 的结构被改动时，必须同步脚本里的解析正则。
- **`Tools/vgmstream/` 是自动下载的第三方二进制**（已忽略）。离线环境需预置 `vgmstream-cli.exe`。
- **SDK 风格 csproj 递归收集 `.cs`**：往 `Formal/` 新增文件即可自动编译；但把 `.cs` 放到被忽略的目录会被静默跳过（不会报错，但也不会编译）。
- **现有编译警告是已知的**：CS0114（`Ai_Darius_NoxianGuillotine.OnDestroy`）、CS0168、CS0618（`FindObjectsOfType` 已废弃）、CS0414。修复它们不是当前目标，但**不要新增**警告。
- **PowerShell 控制台中文可能显示为乱码**：这是控制台代码页问题，文件本身是 UTF-8；用 `read` 工具或 `Get-Content -Encoding UTF8` 查看。
- **`README.md` 不再保存版本历史**：历史进 `CHANGELOG.md`，专项笔记进 `docs/`。

## 8. 变更与提交规范

- 提交信息：`<scope>: <摘要>`，scope 用文件名或模块名，例如 `DariusLolVfxRuntime: 收紧机神血怒气流范围`。
- 一次提交只做一件事；不要把「重命名/格式化」和「逻辑修改」混在一起。
- 提交前自查：
  ```powershell
  git status --short          # 确认没有 dll / wav / glb / 日志混入
  git check-ignore -v <path>  # 确认资产被正确忽略
  ```
- 涉及真源格式（图标映射、`PreloadAll()` 纹理表、`DariusSkinSpec` 表、manifest 结构）的改动，必须在提交信息或 `CHANGELOG.md` 中说明脚本解析正则是否同步。
- 新增文档放 `docs/`，并在 `README.md` 的文档索引中登记。

## 9. 代理工作流检查清单

**开工前**
- [ ] 读 `README.md`（项目全貌）与本节之前的全部内容。
- [ ] 确认改动落在哪一层：C# 逻辑（`Formal/`、`DariusPrototype.cs`）、构建脚本（`*.ps1` / `*.bat`）、离线工具（`Tools/`）、文档（`docs/`）。
- [ ] 若任务涉及资产，先确认资产存在（`assets/` 不入库，可能本机缺失）。

**改动中**
- [ ] 只改与任务相关的文件；不引入新依赖；不使用 C# 10+ 语法。
- [ ] 改动资产/引用时只改真源（csproj / `Formal/` 代码 / manifest）；只有 VFX 发布门槛变化才改 `$ReleaseGates`。
- [ ] 触碰版本号时只改 `about/metadata.json` 的 `modVer`。

**收尾**
- [ ] 能编译就编译；不能编译就明确声明「未编译验证」，并给出静态自检结果（括号配平、引用/路径存在性、JSON 可解析）。
- [ ] 更新受影响的文档（`README.md` 索引、`CHANGELOG.md`、`docs/assets.md`）。
- [ ] `git status` 确认无二进制、无日志、无 `bin/` `obj/` 进入版本控制。
- [ ] 报告改动清单时给出具体文件路径，不要笼统地说「已优化」。

**绝对不要**
- 提交任何二进制资产或构建产物。
- 声称完成了未实际执行的构建或游戏内验证。
- 为了让编译通过而伪造资产、删除资产契约检查、或注释掉校验逻辑。
- 未经要求大规模重构 `Formal/` 下的超大文件。
