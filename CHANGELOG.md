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
- `PackageMod` 的顺序收敛为 Release preflight → voice/package asset preflight → Build → 清理并重建 `build/`；Debug 配置会在进入编译前直接失败。
- `VerifyVoiceAssets` 固化语音 OGG-only 契约：至少存在 `vo_*.ogg`，发现任何 `vo_*.wav` 即停止打包；package 文件集也显式排除旧 voice WAV。
- `DeployMod` 用 package snapshot 完整替换安装目录，避免升级后遗留旧音频、贴图或 manifest。
- `DeployMod` 拒绝把部署目录指向仓库根，避免清理安装目录时误删源码。
- 构建期只保留必要的配置、格式与存在性边界检查；跨文件静态/release validation 由 .NET 验证工具承担。
- 新增 `.github/workflows/dotnet-ci.yml`：PR、`main` push 与手动触发时在 GitHub-hosted Windows runner 执行云端验证。
- 新增零 NuGet 的 `tools/RepoValidation/`（`net8.0`）：验证 metadata/Workshop、主 csproj/package target、tracked-file 红线、voice source 契约；如果 runner 提供 `assets/` 或 `build/`，自动追加 OGG/WAV header、GLB 基本结构与 package snapshot 检查。
- CI 直接求值主项目 `CheckPackageConfiguration` Release preflight；不伪造 Shape of Dreams/Dew/Unity API 来制造假主 DLL 编译。

**版本与文档**

- 版本号收敛到 `about/metadata.json` 的 `modVer`（本版 `0.31.0-refactor`），代码与构建脚本中不再有版本字面量；README 也不再复制“当前版本”。
- `CHANGELOG.md` 改为 Keep a Changelog 格式；`0.30.x` 及更早移入 `docs/archive/CHANGELOG-legacy.md`，不再维护。
- `README.md` / `AGENTS.md` 记录云端 CI、本地 Release gate 与游戏依赖边界。

**资产与体积**

- **语音运行时与发布格式固定为 OGG-only**：411 条语音使用 22.05 kHz 单声道 Ogg Vorbis，按需异步解码并缓存。
- 删除无实际工作的 voice startup preload hook；启动阶段不再存在语音预加载入口。
- Pass2 Wwise manifest 只有在成功解析后才标记为已加载；启动早期路径未就绪或读取失败时允许后续重试。
- 发布包排除原始 WEM 与 `assets/raw_lol_vfx_pass2` 等提取源，仅保留运行时需要的 `assets/raw_lol_audio/PASS2_MEDIA_MANIFEST.json`。
- 发布体积约 **95 MB**，其中语音约 8 MB；同时移除启动时约 70 MB 的全量语音 PCM 预分配。
- `docs/assets.md` 记录最终资产格式、打包内容、生成约束与体积构成。

**清理**

- 移除外部工具产出的静态校验报告、面向旧交付包的说明与无人读取的 marker。
- 修正 Workshop 描述里的星座数量（26 → 38）与音量滑条数量（6 → 7）。
- 删除仓库根目录的陈旧二进制与历史备份。

### 已知问题

- 普通 GitHub-hosted runner 不包含 Shape of Dreams 游戏程序集、仓库外共享源码和未入库二进制资产，因此不能真实编译/打包主 Mod；完整 Release build/package/deploy 与游戏 smoke test 仍需要本地合法游戏环境。
- 技能 SFX 仍为 PCM16 WAV（约 21 MB）；本轮 OGG-only 约束针对语音资源。
