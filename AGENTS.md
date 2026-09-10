# AGENTS.md

> 本文件是 AI 编码代理（Codex / Claude / DeepSeek 等）在本仓库工作时的操作手册。
> 人类贡献者也可把它当作开发约定参考。**动手前请先读第 2、4、7 节。**

---

## 1. 项目速览

- **是什么**：Shape of Dreams（Unity / Steam）的非官方角色 Mod，把 League of Legends 的德莱厄斯作为独立 `Hero_Darius` Traveler 接入游戏。
- **形态**：单个 .NET 类库 `DariusPrototype.dll` + 一个 `about\` 元数据目录 + 一组二进制资产（不入库）。
- **加载方式**：游戏内置 `DewMod` 加载器（`ModBehaviour`）+ Harmony 补丁，全部资源在运行时构建。
- **语言**：C# 9.0 / `netstandard2.1`。
- **规模**：`src/DariusPrototype/` 共 45 个 `.cs`（40 个在根目录 + 5 个在 `UniversalAnimation/`）、约 106 万字符；最大的三个文件是 `DariusTravelerSystem.cs`（~192 KB）、`DariusConstellations.cs`（~106 KB）、`DariusLolVfxRuntime.cs`（~84 KB）。
- **没有测试框架、没有 CI**。验证 = `dotnet build` 编译 + 游戏内人工清单。

## 2. 技术栈与硬约束

| 约束 | 值 | 说明 |
| --- | --- | --- |
| 目标框架 | `netstandard2.1` | 必须匹配游戏 Unity 运行时 |
| 语言版本 | C# 9.0 | 不要使用 C# 10+ 语法（file-scoped namespace、record struct、全局 using 等） |
| 编译方式 | `NoStdLib` + `DisableImplicitFrameworkReferences` | 所有程序集引用都来自游戏 `Managed` 目录 |
| 依赖 | **零 NuGet 包** | 不允许新增 NuGet/PackageReference；需要新程序集时改 csproj 的 `<Reference>` |
| 构建 | `dotnet build`（MSBuild） | **没有 shell 构建脚本**；游戏路径通过 `GameDir` 属性注入 |
| 平台 | Windows | 游戏与 Steam 安装路径为 Windows 形式 |
| 编码 | UTF-8 | 仓库内 BOM 状态不统一（如 `DariusTravelerSystem.cs` 有 BOM）；新建文件用**无 BOM UTF-8**，行尾 LF |

**不要做的事**：升级 TargetFramework、开启 implicit usings、引入第三方库、把代码拆成新项目、改 `AssemblyName`（`about\metadata.json` 与 csproj 都写死了 `DariusPrototype.dll`）。

## 3. 源码职责地图

### 3.1 入口与构建

| 文件 | 职责 |
| --- | --- |
| `DariusPrototype.cs` | `DariusPrototypeMod : ModBehaviour` 入口。`Awake` 早期引导 → `Start` 中 `harmony.PatchAll()`、注册生命周期/兼容层、启动自愈协程。**引导路径的异常必须被捕获且不得中断核心注册**（见文件内注释） |
| `DariusPrototype.csproj` | SDK 风格工程，默认递归包含 `**/*.cs`。新增 `.cs` 无需登记；但**不要**在 `assets/`、`docs/` 下放 `.cs` |

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
| `DariusMedia.cs` | 运行时媒体加载：VFX 贴图 + 技能 SFX（同步 WAV）+ 语音 OGG（首次选中时异步解码、缓存并播放） |
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
| `src/DariusPrototype/UniversalAnimation/*.cs` | 通用动画重定向运行时：`AnimationLibrary`（加载）、`UniversalRetargeter`（重定向）、`DariusRetargetApi`（技能侧桥）、`ModelUtils`、`RuntimeLog` |

### 3.5 离线工具

| 文件 | 职责 |
| --- | --- |
| `tools/BuildDariusPass5AuthenticVfx.py` | 解析 Riot `PROP` BIN，生成 `assets/lol_vfx/darius_lol_vfx.json` 与贴图/网格载荷 |

音频资产来自 `assets/raw_lol_audio/**/*.wem`。技能 SFX 最终为 PCM16 WAV；语音最终只保留 `vo_*.ogg`。具体生成与发布契约见 `docs/assets.md`。

## 4. 构建与验证

### 4.1 前置条件（缺一不可）

1. 本机安装 **Shape of Dreams**，且存在 `<游戏目录>\Shape of Dreams_Data\Managed\`。
2. 安装 **.NET SDK 8.0+**。
3. 设置 `GameDir`（`-p:` / `Directory.Build.props` / `SOD_GAME_DIR`）；仓库本身可以放在任何位置，但不要直接把仓库当作 `<GameDir>\Mods\DariusPrototype` 安装目录。
4. `<游戏目录>\Mods\TravelerBasicAttackVfxReplication.cs` 存在——**跨 Mod 共享源码，不在本仓库内**（csproj 通过 `$(ModsDir)\TravelerBasicAttackVfxReplication.cs` 链接；缺失时 `CheckGameInstall` 目标直接报错）。
5. 二进制资产齐备（打包需要；见第 6 节与 `docs/assets.md`）。

### 4.2 构建 / 打包 / 部署

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release                   # 只编译
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:PackageMod     # 清理并组装 build/
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:DeployMod      # 打包 + 完整替换游戏安装目录
```

| 目标 | 产物 |
| --- | --- |
| `Build` | `src/DariusPrototype/bin/Release/netstandard2.1/DariusPrototype.dll` |
| `PackageMod` | 干净的 `build/` snapshot：DLL + `about/` + 运行时资产；raw 提取资产不进包，仅保留运行时需要的 `raw_lol_audio/PASS2_MEDIA_MANIFEST.json` |
| `DeployMod` | 先清理 `$(ModsDir)\DariusPrototype`，再用 `build/` 完整替换；部署目标不得是仓库根 |

游戏路径解析顺序：`-p:GameDir=...` → `Directory.Build.props`（本地，已 gitignore）→ 环境变量 `SOD_GAME_DIR`。解析不到时 `CheckGameInstall` 给出明确报错。`ModsDir` 默认 `$(GameDir)\Mods`。

**发布只发 `build/`**，不发仓库。`PackageMod` 只接受 Release 配置；voice WAV 被明确排除。

### 4.3 构建只做轻量边界检查

构建**不校验资产数量**：raw 提取资产不进包，`PASS2_MEDIA_MANIFEST.json` 是运行时所需的唯一例外。资产完整性由运行时日志负责（`DariusMedia.PreloadAll()`、`DariusTravelerSystem` 的皮肤规格、`DariusLolVfxRuntime` 的 manifest 校验）。构建期只做存在性检查：

- `CheckGameInstall`：游戏程序集、外部共享源码；
- `VerifyPackageAssets`：打包前必需资产（`about/metadata.json`、`assets/models/darius.glb`、`assets/lol_vfx/darius_lol_vfx.json`、`assets/audio/flash.ogg`、`assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`）。

版本号唯一真源仍是 `about\metadata.json` 的 `modVer`；csproj 里**不要**加 `<Version>`。发布体积构成与压缩取舍见 `docs/assets.md` 第 3.5 节。

### 4.4 验证方式

| 层次 | 手段 |
| --- | --- |
| 编译 | `dotnet build` —— **唯一的权威编译验证** |
| 运行时 | 游戏内人工清单 `docs/MECHA_VFX_HOTFIX*_TEST_CHECKLIST.md` + 共享 Mods 目录下的 `DariusPrototype_runtime.log` |
| 静态自检 | 没有仓库内的检查脚本。无法编译时只能做括号配平、引用/路径存在性、JSON 可解析等自检，并明确声明「未编译验证」 |

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
9. **版本号单一真源**：只改 `about\metadata.json` 的 `modVer`。运行时（`DariusModEnvironment.Version`）从它读取；**禁止**在任何 `.cs` / `.csproj` 里再写版本字面量。
10. **共享 GUID**：资源 GUID 只在 `src/DariusPrototype/DariusResourceIds.cs` 定义一次，其他文件引用常量；这些值持久化在存档与网络身份中，不得更改。
11. **日志级别**：`DariusLog.Minimum` 或环境变量 `DARIUS_LOG_LEVEL`（`debug|info|warn|error|off`，默认 `debug`）。`EXCEPTION` 不受过滤。

## 6. 资产与版权红线

1. **禁止把二进制资产提交到 Git**。`.gitignore` 已忽略整个 `assets/`、`about/*.png` 与 `tools/vgmstream/`。`git status` 中不应出现任何 `.glb` / `.wav` / `.ogg` / `.wem` / `.tex` / `.skn` / `.png` / `.dll`。
2. **禁止公开分发 Riot 原始资产**。League 资产版权归 Riot Games；本项目依据 Riot 的 Legal Jibber Jabber 政策创作，仅限本地互操作使用。
3. **不要伪造替代 VFX**。Riot 原语不受支持时应显式记录并跳过（fidelity gate），而不是用自制贴图/几何冒充——这是本项目的核心原则。
4. **不要重绘用户提供的资源**。例如 `assets/icons/star_awoo.png` 必须保持与用户原图字节一致，禁止裁剪/改色/风格化。
5. **不要跨皮肤替代语音或音效**：缺少对应资源时宁可留空。
6. 资产来源与离线生成流程见 `docs/assets.md`；新增资产时必须同步更新该文档。

## 7. 常见陷阱

- **没有 shell 构建脚本**：构建就是 `dotnet build`；游戏路径用 `GameDir`（`-p:` / `Directory.Build.props` / `SOD_GAME_DIR`）注入。不要再引入 `.bat` / `.ps1` 构建或校验脚本。
- **`TravelerBasicAttackVfxReplication.cs` 不在仓库里**：csproj 从 `$(ModsDir)` 链接它，缺失时 `CheckGameInstall` 直接报错。
- **版本号只有一个真源**：`about\metadata.json` 的 `modVer`。运行时 `DariusModEnvironment.Version` 读它；任何 `.cs` / `.csproj` / README 顶部都不要再复制“当前版本”。
- **共享 GUID 只在 `src/DariusPrototype/DariusResourceIds.cs` 定义**：改值会破坏存档与网络身份。
- **日志级别**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`（默认 `debug`）；`EXCEPTION` 始终写入。
- **`PackageMod` / `DeployMod` 都是 snapshot 语义**：Package 先清理 `build/`，Deploy 先清理安装目录，因此删除资源就是删除资源，不允许依赖旧安装残留。两个 target 都必须用 `-c Release`。
- **语音只使用 OGG**：运行时只查 `vo_*.ogg`，首次选中时异步解码并缓存；不要把 voice WAV 加回运行时或 package。
- **Pass2 manifest 允许重试**：只有成功解析后才能把 `_pass2PoolsLoaded` 置为 true，启动早期路径未准备好时不能永久锁死。
- **构建只做存在性边界检查**：数量/结构校验在运行时日志里，不在构建期；不要为此新增脚本。
- **日志写在共享 Mods 目录**，不在 Mod 目录内。找日志时往上一层看。
- **SDK 风格 csproj 递归收集 `.cs`**：往 `src/DariusPrototype/` 新增文件即可自动编译；但把 `.cs` 放到被忽略的目录会被静默跳过。
- **现有编译警告是已知的**：CS0114（`Ai_Darius_NoxianGuillotine.OnDestroy`）、CS0168、CS0618（`FindObjectsOfType` 已废弃）、CS0414。修复它们不是当前目标，但**不要新增**警告。
- **PowerShell 控制台中文可能显示为乱码**：这是控制台代码页问题，文件本身是 UTF-8；用 `read` 工具或 `Get-Content -Encoding UTF8` 查看。
- **`README.md` 不再保存版本历史**：历史进 `CHANGELOG.md`，专项笔记进 `docs/`。

## 8. 变更与提交规范

- 提交信息：`<scope>: <摘要>`，scope 用文件名或模块名，例如 `DariusLolVfxRuntime: 收紧机神血怒气流范围`。
- 一次提交只做一件事；不要把「重命名/格式化」和「逻辑修改」混在一起。
- 提交前自查：
  ```powershell
  git status --short
  git check-ignore -v <path>
  ```
- 不要新增 `.bat` / `.ps1` 构建或校验脚本；构建逻辑一律放 `*.csproj` / `*.targets`。
- 发版时同步两处：`about\metadata.json` 的 `modVer` 与仓库根 `CHANGELOG.md`（Keep a Changelog 格式）。`docs/archive/CHANGELOG-legacy.md` 是 0.30.x 及更早的历史，**不再维护**。
- 新增文档放 `docs/`，并在 `README.md` 的文档索引中登记。

## 9. 代理工作流检查清单

**开工前**
- [ ] 读 `README.md`（项目全貌）与本节之前的全部内容。
- [ ] 确认改动落在哪一层：C# 逻辑（`src/DariusPrototype/`）、构建定义（`*.csproj`）、离线工具（`tools/`）、文档（`docs/`）。
- [ ] 若任务涉及资产，先确认资产存在（`assets/` 不入库，可能本机缺失）。

**改动中**
- [ ] 只改与任务相关的文件；不引入新依赖；不使用 C# 10+ 语法。
- [ ] 改动资产/引用时改真源（csproj / `src/DariusPrototype/` 代码 / manifest）；不要新增 shell 脚本。
- [ ] 触碰版本号时只改 `about/metadata.json` 的 `modVer`。

**收尾**
- [ ] 能编译就编译；不能编译就明确声明「未编译验证」，并给出静态自检结果（括号配平、引用/路径存在性、JSON 可解析）。
- [ ] 发布前跑一次 `-c Release -t:PackageMod`，确认 `build/` 不含 raw 提取资产（运行时 manifest 除外）且不含 voice WAV。
- [ ] 更新受影响的文档（`README.md` 索引、`CHANGELOG.md`、`docs/assets.md`）。
- [ ] `git status` 确认无二进制、无日志、无 `bin/` `obj/` 进入版本控制。
- [ ] 报告改动清单时给出具体文件路径，不要笼统地说「已优化」。

**绝对不要**
- 提交任何二进制资产或构建产物。
- 声称完成了未实际执行的构建或游戏内验证。
- 为了让编译通过而伪造资产、删除资产契约检查、或注释掉校验逻辑。
- 未经要求大规模重构 `src/DariusPrototype/` 下的超大文件。
