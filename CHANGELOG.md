# Changelog

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。
版本号唯一真源是 `about/metadata.json` 的 `modVer`；每次发版更新本文件。

> `0.30.x` 及更早的 Mod 历史（Riot VFX 重建、皮肤、星座、平衡等）见 [docs/archive/CHANGELOG-legacy.md](docs/archive/CHANGELOG-legacy.md)，仅作保留、不再维护。

## [0.31.0-refactor] - 2026-09-09

工程重构版本：不改变玩法与数值，目标是让仓库符合常规工程习惯、把构建交还给 .NET 工具链、压缩发布体积。

### 思路

1. 源码、资产、文档、构建产物各归其位，仓库与游戏安装目录解耦。
2. 构建、打包、部署全部交给 MSBuild，不再用 `.bat` / `.ps1` 做构建或校验。
3. 「同一事实出现多处」的东西（版本号、资产清单、共享 GUID）收敛到单一真源。
4. 发布只发一个自包含的包，并尽量减小它。

### 行动

**目录与构建**

- 源码归入 `src/DariusPrototype/`（原 `Formal/` 扁平化），离线工具归入 `tools/`；`assets/`、`docs/`、`about/` 位置不变。
- 删除全部 shell 构建与校验脚本。`dotnet build` 编译，`-t:PackageMod` 生成 `build/`，`-t:DeployMod` 部署到游戏。
- 游戏路径由 `GameDir` 属性注入（命令行 / `Directory.Build.props` / `SOD_GAME_DIR`），仓库可以放在任何位置。
- `PackageMod` 只接受 Release 配置，并在组包前清理 `build/`；`DeployMod` 用 package snapshot 完整替换安装目录，避免升级后遗留旧 WAV、贴图或 manifest。
- `DeployMod` 拒绝把部署目录指向仓库根，避免清理安装目录时误删源码。
- 构建期只保留两处**存在性**边界检查：游戏程序集与外部共享源码、打包前必需资产。数量与结构校验交给运行时日志，不再维护第二份清单。

**版本与文档**

- 版本号收敛到 `about/metadata.json` 的 `modVer`（本版 `0.31.0-refactor`），代码与构建脚本中不再有版本字面量；README 也不再复制“当前版本”。
- `CHANGELOG.md` 改为 Keep a Changelog 格式；`0.30.x` 及更早移入 `docs/archive/CHANGELOG-legacy.md`，不再维护。
- `AGENTS.md` 重写为可执行的工程手册：结构地图、构建/打包/部署、边界与陷阱、发版规则。

**资产与体积**

- **语音全部转 22.05 kHz 单声道 Ogg Vorbis**；运行时不再启动时解码全部 411 条语音，而是在某条语音第一次被选中时异步解码、缓存并完成该次播放。
- Pass2 Wwise manifest 只有在成功解析后才标记为已加载；启动早期路径未就绪或读取失败时允许后续重试。
- 发布包排除 `assets/raw_lol_audio`、`assets/raw_lol_vfx_pass2` 等提取源，同时补上运行时需要的 `PASS2_MEDIA_MANIFEST.json`。
- 发布体积从约 **225 MB** 降到约 **95 MB**（语音 138 MB → 8 MB），同时移除启动时约 70 MB 的全量语音 PCM 预分配。
- `docs/assets.md` 补充打包内容、体积构成与后续优化选项：模型裁掉未使用动画、贴图无损压缩；Draco/meshopt 因运行时无解码器暂不可行。

**清理**

- 移除外部工具产出的静态校验报告、面向旧交付包的说明与无人读取的 marker。
- 修正 Workshop 描述里的星座数量（26 → 38）与音量滑条数量（6 → 7）。
- 删除仓库根目录的陈旧二进制与历史备份。

### 已知问题

- 缺少仓库外的共享源码 `TravelerBasicAttackVfxReplication.cs` 时无法编译（构建会明确报错）。
- 技能 SFX 仍为 PCM16 WAV（约 21 MB）；语音已压缩并按需解码，技能音效保留 WAV 是为了低延迟。
