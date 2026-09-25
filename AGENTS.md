# AGENTS.md

> 本文件是 AI 编码代理（Codex / Claude / DeepSeek 等）在本仓库工作时的操作手册。
> 人类贡献者也可把它当作开发约定参考。**动手前请先读第 2、4、7 节。**

---

## 1. 项目速览

- **是什么**：Shape of Dreams（Unity / Steam）的非官方角色 Mod，把 League of Legends 的德莱厄斯作为独立 `Hero_Darius` Traveler 接入游戏。
- **形态**：单个 .NET 类库 `DariusPrototype.dll` + `about/` 元数据 + 不入库的二进制资产。
- **加载方式**：游戏内置 `DewMod` 加载器（`ModBehaviour`）+ Harmony 补丁，全部资源在运行时构建。
- **语言**：C# 9.0 / `netstandard2.1`。
- **源码组织**：按运行时职责拆分；超过约 200 行只是审查信号，不是自动拆分规则。长但单一职责的状态机可以保持完整。
- **没有单元测试框架**。GitHub Actions `.NET CI` 会使用 metadata-only Shape of Dreams reference pack 真正编译主 DLL；游戏内行为仍需要 runtime smoke 验证。

## 2. 技术栈与硬约束

| 约束 | 值 | 说明 |
| --- | --- | --- |
| 目标框架 | `netstandard2.1` | 必须匹配游戏 Unity 运行时 |
| 语言版本 | C# 9.0 | 不要使用 C# 10+ 语法（file-scoped namespace、record struct、全局 using 等） |
| 编译方式 | `NoStdLib` + `DisableImplicitFrameworkReferences` | 默认本地构建引用游戏 `Managed` DLL；CI 可切换 metadata-only reference pack |
| 运行时依赖 | 不新增第三方运行时包 | 主项目仅在 `UseSodReferencePack=true` 时使用私有编译引用包 `ShapeOfDreams.ReferenceAssemblies`，并设置 `PrivateAssets=all` |
| 构建 | `dotnet build`（MSBuild） | 本地游戏路径通过 `GameDir` 属性注入 |
| CI | GitHub Actions | 使用 GitHub Packages reference pack 编译；不要手写 Unity/Dew stub |
| 平台 | Windows | 游戏与 Steam 安装路径为 Windows 形式；CI 使用 GitHub-hosted Windows runner |
| 编码 | UTF-8 | 新建文件用无 BOM UTF-8、LF |

**不要做的事**：升级 TargetFramework、开启主项目 implicit usings、引入第三方运行时库、把运行时代码拆成新项目、改 `AssemblyName`、为了行数机械拆 partial class。

## 3. 源码职责地图

源码按运行时职责分目录（SDK 风格 csproj 递归收集 `**/*.cs`，移动文件不影响编译；代码处于全局命名空间，不要为此新增 namespace）：

```text
src/DariusPrototype/
├── Core/                 # 引导、统一日志、环境/诊断、共享资源 ID
├── Combat/
│   ├── Skills/           # 技能/战斗支撑（R 输入守卫、TriggerConfig 访问、战斗控制器）
│   │   ├── Abilities/    # Q/W/E/R 执行体与对应 SkillTrigger（Ai_* / St_* / Gem_*）
│   │   └── Effects/      # 出血/被动、Memory 成长、减速、敌人分类、层数 HUD
│   ├── BasicAttack/      # 原生普攻与定向普攻扇区
│   └── Summoners/        # Flash / Ghost / 原生位移
├── Traveler/             # Hero_Darius 注册、模型绑定、生命周期
├── Constellations/       # 星座 + 装备星（含 persistence / presentation）
├── Registration/         # 正式资源注册 / Deja Vu / 运行时查找兼容层
├── Presentation/
│   ├── Vfx/              # 高层表现 facade、LoL VFX interpreter、普攻视觉
│   ├── Model/            # GLB 运行时模型、native model bridge、皮肤动画桥
│   └── Media/            # 贴图/图标、SFX、语音资源加载
├── Localization/         # zh_CN / en_US / ja_JP 文案
├── Native/               # 游戏原生动画/模型 Harmony 补丁
└── UniversalAnimation/   # 通用动画重定向运行时
```

### 3.1 入口与构建

| 文件 | 职责 |
| --- | --- |
| `Core/DariusPrototype.cs` | `DariusPrototypeMod : ModBehaviour` 生命周期/bootstrap；可选表现失败不得中断核心注册 |
| `DariusPrototype.csproj` | SDK 风格工程、真实游戏引用/reference-pack 切换、Package/Deploy targets |
| `.github/workflows/dotnet-ci.yml` | 仓库契约 + reference-pack Release build |
| `tools/SodReferencePack/*` | 从本机真实游戏程序集生成 metadata-only CI 编译引用 |

### 3.2 技能与战斗

| 文件 | 职责 |
| --- | --- |
| `Combat/Skills/Abilities/Ai_Darius_Decimate.cs` | Q 大杀四方服务端执行体 |
| `Combat/Skills/Abilities/Ai_Darius_CripplingStrike.cs` | W 一次性 `AbilityInstance` |
| `Combat/Skills/Abilities/DariusCripplingStrikeRuntime.cs` | W Hero 常驻武器强化状态 |
| `Combat/Skills/Abilities/Ai_Darius_Apprehend.cs` | E 无情铁手执行体 |
| `Combat/Skills/Abilities/Ai_Darius_NoxianGuillotine.cs` | R `AbilityInstance` 执行体 |
| `Combat/Skills/Abilities/St_Darius_*.cs` | 对应技能的 `SkillTrigger` 定义/输入状态 |
| `Combat/Skills/Abilities/St_D_Darius_Hemorrhage.cs` | 被动 Identity `SkillTrigger` |
| `Combat/Skills/Abilities/Gem_Darius_Hemorrhage.cs` | 旧 Gem 兼容适配器 |
| `Combat/Skills/Effects/DariusHemorrhageRuntime.cs` | 出血/Noxian Might Hero 状态机 |
| `Combat/Skills/Effects/DariusMemoryScaling.cs` | Memory 等级成长规则唯一实现 |
| `Combat/Skills/Effects/DariusMemoryEffectBridge.cs` | direct-execution 技能的 Damage/Heal/Cast 归因桥 |
| `Combat/BasicAttack/DariusNativeAttack.cs` | 原生普攻 trigger/instances/binder |
| `Combat/BasicAttack/DariusDirectionalBasicAttack.cs` | 定向普攻几何/状态 |
| `Combat/BasicAttack/DariusDirectionalBasicAttackSectorPatch.cs` | 普攻命中扇区 Harmony 过滤 |
| `Combat/Summoners/DariusNativeDisplacement.cs` | Flash / Ghost 原生位移接管 |
| `Combat/Summoners/DariusSummonerSkills.cs` | 召唤师技能平衡/原生配置派生 |
| `Combat/Summoners/DariusFlashSkill.cs` | Flash trigger/warp |
| `Combat/Summoners/DariusGhostSkill.cs` | Ghost trigger/runtime |
| `Combat/Summoners/DariusSummonerRuntime.cs` | 召唤师方向解析/兼容反射 |
| `Combat/Skills/Effects/DariusSlowHelper.cs` | 减速应用 helper/runtime |
| `Combat/Skills/Effects/DariusHemorrhageHud.cs` | 出血层数 HUD |
| `Combat/Skills/Effects/DariusEnemyClassifier.cs` | 敌人分类 |
| `Combat/Skills/DariusRInputGuard.cs` | R 输入守卫 |
| `Combat/Skills/DariusTriggerConfigRuntimeEditor.cs` | `TriggerConfig` 私有后备字段统一访问 |
| `Combat/Skills/DariusCombatController.cs` | 历史诊断/战斗控制器 |

### 3.3 Traveler、皮肤与表现

| 文件 | 职责 |
| --- | --- |
| `Traveler/DariusTravelerNativeIntegration.cs` | `Hero_Darius` 与 native model/animation guard Harmony patches |
| `Traveler/DariusTravelerRegistry.cs` | Hero/Skin/Attack 运行时资源注册、查找、自愈、卸载；长但共享一个资源状态机，勿按行数继续拆 |
| `Traveler/DariusTravelerModel.cs` | skin model binding + `DariusTravelerModelInstance` 模型/动画状态机 |
| `Traveler/DariusTravelerLifecycle.cs` | 场景/profile repair lifecycle + Traveler localization patch |
| `Presentation/Model/DariusSkinAnimationHooks.cs` | 皮肤动画桥接 |
| `Presentation/Vfx/DariusPrototypeVfx.cs` | Darius 高层表现 facade |
| `Presentation/Vfx/DariusBasicAttackVisualRuntime.cs` | 普攻视觉 runtime |
| `Presentation/Model/DariusGlbRuntimeModel.cs` | GLB 模型/材质/动画解释状态机；长但内聚 |
| `Presentation/Media/DariusMedia.cs` | VFX 贴图 + 技能 SFX + 语音资源加载/cache |
| `Presentation/Vfx/DariusLolVfxRuntime.cs` | Riot `VfxSystemDefinitionData` interpreter/cache；长但内聚 |
| `Presentation/Vfx/DariusLolVfxComponents.cs` | LoL VFX interpreter 使用的短生命周期 MonoBehaviour 组件 |
| `Presentation/Media/DariusVoiceRuntime.cs` | 施法语音路由；不得伪造或跨皮肤替代语音 |
| `Presentation/Media/DariusPrototypeIcons.cs` | 图标加载 |
| `Presentation/Vfx/TravelerBasicAttackVfxReplication.cs` | 仓库内置 Mirror 普攻表现消息/relay；不再依赖仓库外共享源码 |

### 3.4 注册、星座与兼容

| 文件 | 职责 |
| --- | --- |
| `Registration/DariusFormalRegistry.cs` | Memory/AbilityInstance/Identity 正式运行时资源注册 |
| `Registration/DariusDejaVuRegistry.cs` | Deja Vu/profile/content 接入与相关兼容 patch；保持一个候选资源契约 |
| `Registration/DariusRuntimeResourceCompatibility.cs` | DewResources/Harmony 运行时查找兼容层 |
| `Constellations/DariusConstellationDefinitions.cs` | Darius 星座 ID/StarEffect 类型 |
| `Constellations/DariusConstellationRegistry.cs` | 星座运行时资源注册 + reflection hydration |
| `Constellations/DariusConstellationRuntime.cs` | Hero 星座 gameplay 状态机 |
| `Constellations/DariusConstellationPresentation.cs` | 星座本地化 + Lobby UI/Harmony presentation guard |
| `Constellations/DariusConstellationPersistence.cs` | 星座购买/存档 capture/restore/persistence |
| `Constellations/DariusEquipmentConstellationDefinitions.cs` | 装备星 ID/类型/本地化 |
| `Constellations/DariusEquipmentRuntime.cs` | 装备星 gameplay 状态机 |
| `Presentation/Media/DariusAwooAudioRuntime.cs` | Awoo 可选本地音频播放 |
| `Localization/DariusFormalLocalization.cs` / `Localization/DariusEnglishLocalization.cs` / `Localization/DariusJapaneseLocalization.cs` | zh_CN / en_US / ja_JP 文案 |
| `Core/DariusAudioSettings.cs` | ModConfig 音量字段 |

### 3.5 基础设施与离线工具

| 文件 | 职责 |
| --- | --- |
| `Core/DariusLog.cs` | 统一日志 |
| `Core/DariusDiagnostics.cs` | 首测运行时快照 |
| `Core/DariusModEnvironment.cs` | 解析 Mod 物理目录；版本读取 `about/metadata.json` |
| `Core/DariusResourceIds.cs` | 共享资源 GUID 唯一定义处 |
| `src/DariusPrototype/UniversalAnimation/*.cs` | 通用动画重定向运行时 |
| `tools/BuildDariusPass5AuthenticVfx.py` | 解析 Riot `PROP` BIN，生成 LoL VFX 载荷 |
| `tools/SodReferencePack/New-SodReferencePack.ps1` | 从真实 SOD Managed DLL 生成/打包 reference assemblies |

音频资产来自 `assets/raw_lol_audio/**/*.wem`。技能 SFX 最终为 PCM16 WAV；语音最终只保留 `vo_*.ogg`。具体生成与发布契约见 `docs/assets.md`。

## 4. 构建与验证

### 4.1 本地真实游戏构建前置条件

1. 本机安装 **Shape of Dreams**，且存在 `<游戏目录>\Shape of Dreams_Data\Managed\`。
2. 安装 **.NET SDK 8.0+**。
3. 设置 `GameDir`（`-p:` / `Directory.Build.props` / `SOD_GAME_DIR`）。
4. 打包/部署时二进制资产齐备。

`Presentation/Vfx/TravelerBasicAttackVfxReplication.cs` 已在仓库内，不存在额外共享源码前置条件。

### 4.2 构建 / 打包 / 部署

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:PackageMod
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -t:DeployMod
```

`PackageMod`：`CheckPackageConfiguration` → `VerifyVoiceAssets` → `VerifyPackageAssets` → `Build` → 清理并重建 `build/`。

| 目标 | 产物 |
| --- | --- |
| `Build` | `src/DariusPrototype/bin/Release/netstandard2.1/DariusPrototype.dll` |
| `PackageMod` | `build/` snapshot：DLL + `about/` + 运行时资产；raw 提取资产不进包，仅保留 `raw_lol_audio/PASS2_MEDIA_MANIFEST.json` |
| `DeployMod` | 完整替换 `$(ModsDir)\DariusPrototype` |

游戏路径解析顺序：`-p:GameDir=...` → `Directory.Build.props` → `SOD_GAME_DIR`。发布只发 `build/`。

### 4.3 CI reference-pack 编译

主 csproj 有两种引用模式：

- 默认：直接引用 `$(GameManagedDir)` 下实际游戏 DLL；`CheckGameInstall` 生效。
- `-p:UseSodReferencePack=true`：使用 GitHub Packages 中的 `ShapeOfDreams.ReferenceAssemblies` metadata-only 包；`CheckGameInstall` 不运行。

reference pack 由 `tools/SodReferencePack/New-SodReferencePack.ps1` 从本机真实游戏 DLL 通过 Refasmer 生成；不要提交真实游戏 DLL，也不要手写 stub。

`.NET CI` 当前会验证 metadata/Workshop、tracked binary 红线、reference-pack 脚本语法、`CheckPackageConfiguration`，并执行：

```powershell
dotnet build src/DariusPrototype/DariusPrototype.csproj -c Release -p:UseSodReferencePack=true
```

因此**当前 HEAD 的 CI 绿色即表示编译/API gate 已完成**。不要为了重复证明同一件事再要求本地 `dotnet build`。CI 不覆盖未入库资产的 PackageMod，也不能证明游戏内 Unity/Harmony/Mirror 行为。

### 4.4 验证层次

| 层次 | 手段 |
| --- | --- |
| 仓库/编译 gate | GitHub Actions `.NET CI` + SOD reference pack |
| Package / Deploy | `PackageMod` / `DeployMod`；需要本地资产与游戏目录 |
| 运行时 | 游戏内 smoke/checklist + `DariusPrototype_runtime.log` |

> 不要创建假 Unity/Dew/Shape of Dreams stub 来制造绿色 CI。

## 5. 编码约定

1. **命名**：类型/方法 PascalCase；私有字段 `_camelCase`；类名统一带 `Darius` 前缀（技能类沿用 `Ai_*` / `St_*` / `Gem_*`）。
2. **日志**：统一用 `DariusLog`，不要新增 `Debug.Log` / `Console.WriteLine` 作为长期输出。
3. **异常处理**：引导、Harmony 补丁、资源注册路径必须捕获并记录；表现层失败不能阻断 `Hero_Darius` 注册。
4. **反射**：访问游戏私有成员必须做存在性/null 检查并提供回退路径。
5. **Harmony 补丁**：优先按 registry / compatibility / lifecycle / presentation 职责集中，不为一行 patch 单独制造文件。
6. **性能**：高频路径已有缓存时不要破坏。
7. **注释**：解释“为什么”；涉及 Riot 原语/游戏版本差异时写明限制。
8. **Ponytail/YAGNI**：先问代码是否需要存在；优先删除/移动/复用；长文件只有在多职责时拆，单一状态机不要为了行数拆 partial。
9. **不要顺手重构**：未经要求不要重排、重命名或格式化无关大文件。
10. **版本号单一真源**：只改 `about/metadata.json` 的 `modVer`。
11. **共享 GUID**：只在 `Core/DariusResourceIds.cs` 定义，不得更改既有值。
12. **日志级别**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`，`EXCEPTION` 不受过滤。

## 6. 资产与版权红线

1. **禁止把二进制资产提交到 Git**。`.gitignore` 已忽略 `assets/`；CI 额外拒绝 tracked `.glb` / `.wav` / `.ogg` / `.wem` / `.tex` / `.skn` / `.png` / `.dll` 等文件。
2. **禁止公开分发 Riot 原始资产**。
3. **不要伪造替代 VFX**；不支持的 Riot 原语应记录并跳过。
4. **不要重绘用户提供的资源**。
5. **不要跨皮肤替代语音或音效**；缺资源时宁可留空。
6. 新增资产时同步更新 `docs/assets.md`。

## 7. 常见陷阱

- **默认构建就是 `dotnet build`**；reference-pack PowerShell 仅生成 CI 编译引用，不是另一套主构建系统。
- **CI 已是真编译 gate**：不要再说公共 runner 不能编译主 DLL；它通过 metadata-only reference pack 编译，但仍不能证明运行时行为。
- **reference pack 不是 hand-written stub**：它必须从真实游戏程序集生成；游戏版本/API 变化时重新生成并发布新版本。
- **`Presentation/Vfx/TravelerBasicAttackVfxReplication.cs` 已在仓库里**：不要恢复 `SharedSource` 或要求 `<GameDir>\Mods` 下额外源码。
- **长文件不等于坏文件**：`DariusTravelerRegistry`、`DariusLolVfxRuntime`、`DariusGlbRuntimeModel`、`DariusConstellationRuntime` 等共享连续私有状态；没有新职责证据时不要继续拆。
- **不要创建 `DariusUtils`/万能 reflection helper** 只为消除少量形状相似的局部代码；只有规则真正共享时才抽公共层。
- **版本号只有一个真源**：`about/metadata.json` 的 `modVer`。
- **共享 GUID 不得改值**。
- **Package/Deploy 是 snapshot 语义**，都必须使用 Release。
- **语音只使用 OGG**：不要恢复启动时批量预加载或 voice WAV 路径。
- **Pass2 manifest 允许重试**：只有成功解析后才能把 `_pass2PoolsLoaded` 置为 true。
- **日志写在共享 Mods 目录**，不在 Mod 目录内。
- **SDK 风格 csproj 递归收集 `.cs`**。
- **README 不保存版本历史**：历史进 `CHANGELOG.md`，专项笔记进 `docs/`。

## 8. 变更与提交规范

- 提交信息：`<scope>: <摘要>`。
- 一次提交只做一件事；不要把重命名/格式化和逻辑修改混在一起。
- 构建逻辑放 `*.csproj` / `*.targets`；CI 专属、且确实需要的少量仓库检查直接留在 workflow，不另建验证框架。
- 发版时同步 `about/metadata.json` 的 `modVer` 与根目录 `CHANGELOG.md`。
- 新增文档放 `docs/`，并在 README 索引登记。

## 9. 代理工作流检查清单

**开工前**
- [ ] 读 `README.md` 和本文件。
- [ ] 确认改动层：运行时 / 构建 / CI / 离线工具 / 文档。
- [ ] 涉及资产时先确认本机资产是否存在。

**改动中**
- [ ] 只改与任务相关的文件；不引入新运行时依赖；主运行时代码不使用 C# 10+ 语法。
- [ ] 改动资产/引用时改真源（csproj / 源码 / manifest）。
- [ ] 抽公共 helper 前确认至少存在稳定的共享规则，而不是仅仅长得相似。
- [ ] 版本号只改 `about/metadata.json`。

**收尾**
- [ ] PR 上确认 `.NET CI` 绿色；绿色即完成编译/API gate，不重复要求本地 build。
- [ ] gameplay/runtime 改动按风险完成必要的游戏内 smoke；纯文档/CI 变更不强制游戏 smoke。
- [ ] 发布前运行 `-c Release -t:PackageMod`，确认 package 只含运行时资产且 voice 为 OGG。
- [ ] 更新受影响文档。
- [ ] 确认无二进制、日志、`bin/`、`obj/` 进入版本控制。

**绝对不要**
- 提交任何二进制资产或构建产物。
- 声称完成未实际执行的游戏内验证。
- 为了 CI 绿色伪造游戏程序集/资产或删掉真实构建契约。
- 为了满足行数阈值继续拆已经内聚的状态机。