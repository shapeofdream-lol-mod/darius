# AGENTS.md

> 本文件是 AI 编码代理（Codex / Claude / DeepSeek 等）在本仓库工作时的操作手册。
> 人类贡献者也可把它当作开发约定参考。**动手前请先读第 2、4、7 节。**

---

## 1. 项目速览

- **是什么**：Shape of Dreams（Unity / Steam）的非官方角色 Mod，把 League of Legends 的德莱厄斯作为独立 `Hero_Darius` Traveler 接入游戏。
- **形态**：单个 .NET 类库 `DariusPrototype.dll` + `about/` 元数据 + 不入库的二进制资产。
- **加载方式**：游戏内置 `DewMod` 加载器（`ModBehaviour`）+ Harmony 补丁，全部资源在运行时构建。
- **语言**：C# 9.0 / `netstandard2.1`。
- **规模**：`src/DariusPrototype/` 共 45 个 `.cs`；最大的文件包括 `DariusTravelerSystem.cs`、`DariusConstellations.cs`、`DariusLolVfxRuntime.cs`。
- **没有单元测试框架**。GitHub Actions `.NET CI` 只负责公共 runner 能真实执行的仓库契约；主 DLL 编译和玩法验证仍需要真实游戏环境。

## 2. 技术栈与硬约束

| 约束 | 值 | 说明 |
| --- | --- | --- |
| 目标框架 | `netstandard2.1` | 必须匹配游戏 Unity 运行时 |
| 语言版本 | C# 9.0 | 不要使用 C# 10+ 语法（file-scoped namespace、record struct、全局 using 等） |
| 编译方式 | `NoStdLib` + `DisableImplicitFrameworkReferences` | 所有程序集引用都来自游戏 `Managed` 目录 |
| 依赖 | **零 NuGet 包** | 不允许新增 `PackageReference`；需要游戏程序集时改 csproj 的 `<Reference>` |
| 构建 | `dotnet build`（MSBuild） | 没有 shell 构建脚本；游戏路径通过 `GameDir` 属性注入 |
| CI | GitHub Actions | 只检查 runner 当前真实拥有的输入；不要伪造游戏 API 或资产追求绿色 |
| 平台 | Windows | 游戏与 Steam 安装路径为 Windows 形式；CI 使用 `windows-latest` |
| 编码 | UTF-8 | 新建文件用无 BOM UTF-8、LF |

**不要做的事**：升级 TargetFramework、开启主项目 implicit usings、引入第三方库、把运行时代码拆成新项目、改 `AssemblyName`。

## 3. 源码职责地图

### 3.1 入口与构建

| 文件 | 职责 |
| --- | --- |
| `DariusPrototype.cs` | `DariusPrototypeMod : ModBehaviour` 入口；引导路径异常不得中断核心注册 |
| `DariusPrototype.csproj` | SDK 风格工程、游戏程序集引用、Package/Deploy targets |
| `.github/workflows/dotnet-ci.yml` | PR / `main` push / 手动触发的最小云端仓库验证 |

### 3.2 技能与战斗

| 文件 | 职责 |
| --- | --- |
| `Ai_Darius_Decimate.cs` | Q 大杀四方，服务端执行体 |
| `Ai_Darius_CripplingStrike.cs` | W 致残打击 |
| `Ai_Darius_Apprehend.cs` | E 无情铁手 |
| `Ai_Darius_NoxianGuillotine.cs` | R 诺克萨斯断头台 |
| `St_Darius_*.cs` | 对应技能的 `SkillTrigger` 定义 |
| `St_D_Darius_Hemorrhage.cs` | 被动 Identity `SkillTrigger` |
| `Gem_Darius_Hemorrhage.cs` | 出血 `Gem` |
| `DariusMemoryScaling.cs` | Memory 无限成长曲线 |
| `DariusMemoryEffectBridge.cs` | Memory 直接执行桥 |
| `DariusNativeAttack.cs` | 原生普攻接入 |
| `DariusNativeDisplacement.cs` | Flash / Ghost 原生位移接管 |
| `DariusSummonerSkills.cs` | 召唤师技能槽替换 |
| `DariusSlowHelper.cs` | 减速工具 |
| `DariusHemorrhageHud.cs` | 出血层数 HUD |
| `DariusEnemyClassifier.cs` | 敌人分类 |
| `DariusRInputGuard.cs` | R 输入守卫 |
| `DariusTriggerConfigRuntimeEditor.cs` | 写入 `TriggerConfig` 私有后备字段 |

### 3.3 资源、表现与本地化

| 文件 | 职责 |
| --- | --- |
| `DariusTravelerSystem.cs` | Hero/Skin/EntityModel/资源构建、场景切换后的整代重建 |
| `DariusSkinSystem.cs` | 皮肤与动画层、技能动画桥 |
| `DariusMedia.cs` | VFX 贴图 + 技能 SFX（同步 WAV）+ 语音 OGG（首次选中时异步解码、缓存并播放） |
| `DariusLolVfxRuntime.cs` | Riot `VfxSystemDefinitionData` 运行时解释器 |
| `DariusVoiceRuntime.cs` | 施法语音路由；不得伪造或跨皮肤替代语音 |
| `DariusPrototypeIcons.cs` | 图标加载 |
| `DariusFormalRegistry.cs` | 正式 Memory/Essence 运行时资源注册 |
| `DariusDejaVuRegistry.cs` | Deja Vu 起手装备接入 |
| `DariusRuntimeResourceCompatibility.cs` | 运行时资源 Dew 查找兼容层 |
| `DariusConstellations.cs` | 星效定义 |
| `DariusEquipmentConstellations.cs` | 装备类星效 |
| `DariusConstellationPersistence.cs` | 星座购买/存档持久化 |
| `DariusFormalLocalization.cs` / `DariusEnglishLocalization.cs` / `DariusJapaneseLocalization.cs` | zh_CN / en_US / ja_JP 文案 |
| `DariusAudioSettings.cs` | ModConfig 音量字段 |

### 3.4 基础设施

| 文件 | 职责 |
| --- | --- |
| `DariusLog.cs` | 统一日志，写入共享 Mods 目录 |
| `DariusDiagnostics.cs` | 首测运行时快照 |
| `DariusModEnvironment.cs` | 解析 Mod 物理目录；版本读取 `about/metadata.json` |
| `DariusResourceIds.cs` | 共享资源 GUID 唯一定义处 |
| `DariusModLifecycle.cs` | 区分真正热卸载与普通场景销毁 |
| `src/DariusPrototype/UniversalAnimation/*.cs` | 通用动画重定向运行时 |

### 3.5 离线工具

| 文件 | 职责 |
| --- | --- |
| `tools/BuildDariusPass5AuthenticVfx.py` | 解析 Riot `PROP` BIN，生成 LoL VFX 载荷 |

音频资产来自 `assets/raw_lol_audio/**/*.wem`。技能 SFX 最终为 PCM16 WAV；语音最终只保留 `vo_*.ogg`。具体生成与发布契约见 `docs/assets.md`。

## 4. 构建与验证

### 4.1 本地主 Mod 构建前置条件

1. 本机安装 **Shape of Dreams**，且存在 `<游戏目录>\Shape of Dreams_Data\Managed\`。
2. 安装 **.NET SDK 8.0+**。
3. 设置 `GameDir`（`-p:` / `Directory.Build.props` / `SOD_GAME_DIR`）。
4. `<游戏目录>\Mods\TravelerBasicAttackVfxReplication.cs` 存在。
5. 打包时二进制资产齐备。

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

### 4.3 构建边界与云端 CI

主 csproj 维护真正的打包边界：

- `CheckPackageConfiguration`：`PackageMod` 必须使用 Release；
- `CheckGameInstall`：游戏程序集、外部共享源码；
- `VerifyVoiceAssets`：必须存在 `assets/audio/vo_*.ogg`，并拒绝任何 `vo_*.wav`；
- `VerifyPackageAssets`：其它打包必需资产。

`.NET CI` 不再维护第二套 validator。它只用 workflow 内置 PowerShell/.NET CLI 检查当前公共 runner 能真实验证的内容：metadata/Workshop、tracked binary 红线，以及 `CheckPackageConfiguration` 的 Release/Debug 行为。

因为公共 runner 没有游戏程序集、仓库外共享源码和未入库资产，所以**不保留永远只能 skip 的音频头、GLB、package snapshot 代码**。以后真实输入接入 runner 时，再直接加入真实 Build/Package 检查。

### 4.4 验证层次

| 层次 | 手段 |
| --- | --- |
| 云端仓库验证 | GitHub Actions `.NET CI` |
| 主 DLL 编译 | `dotnet build ... -c Release`；需要真实游戏程序集 |
| Package / Deploy | `PackageMod` / `DeployMod`；需要本地资产与游戏目录 |
| 运行时 | 游戏内人工清单 + `DariusPrototype_runtime.log` |

> 不要创建假 Unity/Dew/Shape of Dreams stub 来制造绿色 CI。

## 5. 编码约定

1. **命名**：类型/方法 PascalCase；私有字段 `_camelCase`；类名统一带 `Darius` 前缀（技能类沿用 `Ai_*` / `St_*` / `Gem_*`）。
2. **日志**：统一用 `DariusLog`，不要新增 `Debug.Log` / `Console.WriteLine` 作为长期输出。
3. **异常处理**：引导、Harmony 补丁、资源注册路径必须捕获并记录；表现层失败不能阻断 `Hero_Darius` 注册。
4. **反射**：访问游戏私有成员必须做存在性/null 检查并提供回退路径。
5. **Harmony 补丁**：集中放在 `*Registry` / `*Compatibility` / `*Lifecycle` 类中。
6. **性能**：高频路径已有缓存时不要破坏。
7. **注释**：解释“为什么”；涉及 Riot 原语/游戏版本差异时写明限制。
8. **不要顺手重构**：未经要求不要重排、重命名或格式化无关大文件。
9. **版本号单一真源**：只改 `about/metadata.json` 的 `modVer`。
10. **共享 GUID**：只在 `src/DariusPrototype/DariusResourceIds.cs` 定义，不得更改既有值。
11. **日志级别**：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`，`EXCEPTION` 不受过滤。

## 6. 资产与版权红线

1. **禁止把二进制资产提交到 Git**。`.gitignore` 已忽略 `assets/`；CI 额外拒绝 tracked `.glb` / `.wav` / `.ogg` / `.wem` / `.tex` / `.skn` / `.png` / `.dll` 等文件。
2. **禁止公开分发 Riot 原始资产**。
3. **不要伪造替代 VFX**；不支持的 Riot 原语应记录并跳过。
4. **不要重绘用户提供的资源**。
5. **不要跨皮肤替代语音或音效**；缺资源时宁可留空。
6. 新增资产时同步更新 `docs/assets.md`。

## 7. 常见陷阱

- **没有 shell 构建脚本**：构建就是 `dotnet build`；不要重新引入 `.bat` / `.ps1` 构建脚本。
- **CI 保持小**：不要为“以后也许有资产/私有 DLL”预建 validator、抽象层或 skip 分支；输入真的存在时再加真实检查。
- **云端 CI 不是主 DLL 的假编译**：不要用 stub 替代 Shape of Dreams/Dew/Unity 程序集。
- **`TravelerBasicAttackVfxReplication.cs` 不在仓库里**。
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
- 提交前至少检查：
  ```powershell
  git status --short
  git check-ignore -v <path>
  ```
- 构建逻辑放 `*.csproj` / `*.targets`；CI 专属、且确实需要的少量仓库检查直接留在 workflow，不另建验证框架。
- 发版时同步 `about/metadata.json` 的 `modVer` 与根目录 `CHANGELOG.md`。
- 新增文档放 `docs/`，并在 README 索引登记。

## 9. 代理工作流检查清单

**开工前**
- [ ] 读 `README.md` 和本文件。
- [ ] 确认改动层：运行时 / 构建 / CI / 离线工具 / 文档。
- [ ] 涉及资产时先确认本机资产是否存在。

**改动中**
- [ ] 只改与任务相关的文件；不引入新依赖；主运行时代码不使用 C# 10+ 语法。
- [ ] 改动资产/引用时改真源（csproj / 源码 / manifest）。
- [ ] 新增 CI 检查前先确认公共 runner 真有对应输入；没有就不加。
- [ ] 版本号只改 `about/metadata.json`。

**收尾**
- [ ] PR 上确认 `.NET CI` 绿色。
- [ ] 有真实游戏环境时运行主 DLL Release build；没有时明确声明未完成游戏程序集编译验证。
- [ ] 发布前运行 `-c Release -t:PackageMod`，确认 package 只含运行时资产且 voice 为 OGG。
- [ ] 更新受影响文档。
- [ ] `git status` 确认无二进制、日志、`bin/`、`obj/` 进入版本控制。

**绝对不要**
- 提交任何二进制资产或构建产物。
- 声称完成未实际执行的主 DLL 编译或游戏内验证。
- 为了 CI 绿色伪造游戏程序集/资产或删掉真实构建契约。
- 未经要求大规模重构 `src/DariusPrototype/` 下的超大文件。
