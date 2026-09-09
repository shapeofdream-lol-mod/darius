# Changelog

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。
版本号唯一真源是 `about/metadata.json` 的 `modVer`；每次发版更新本文件。

> `0.30.x` 及更早的 Mod 历史（Riot VFX 重建、皮肤、星座、平衡等）见 [docs/archive/CHANGELOG-legacy.md](docs/archive/CHANGELOG-legacy.md)，仅作保留、不再维护。

## [0.31.0] - 2026-09-09

### Added

- 可发布包：`dotnet build -t:PackageMod` 生成 `build/`（DLL + `about/` + 运行时资产）；**发布只发该目录，不发源码**。
- `dotnet build -t:DeployMod` 把包复制到 `<GameDir>\Mods\DariusPrototype`。
- 轻量边界检查：`CheckGameInstall`（游戏程序集、外部共享源码）与 `VerifyPackageAssets`（打包前必需资产存在）。只查存在性，不查数量。
- 日志级别开关：`DARIUS_LOG_LEVEL=debug|info|warn|error|off`（默认 `debug`，行为不变），`EXCEPTION` 不受过滤。

### Changed

- 目录重构：源码移入 `src/DariusPrototype/`（原 `Formal/` 扁平化），离线工具移入 `tools/`；`assets/`、`docs/`、`about/` 位置不变。
- 构建改为纯 MSBuild：`dotnet build src/DariusPrototype/DariusPrototype.csproj`；游戏路径由 `GameDir` 属性注入（`-p:` / `Directory.Build.props` / `SOD_GAME_DIR`）。
- 版本号收敛到 `about/metadata.json` 的 `modVer` 单一真源，代码与 csproj 中不再有版本字面量。
- 共享资源 GUID 提取到 `src/DariusPrototype/DariusResourceIds.cs`（原先在两个注册器中各写一份）。
- 发布包排除 `assets/raw_lol_audio/`、`assets/raw_lol_vfx_pass2/` 等提取源（约 63 MB）。

### Removed

- 全部 shell 构建/校验脚本：`BuildAndInstall.ps1`、`BUILD_DARIUS.bat`、`PREPARE_DARIUS_PASS2_MEDIA.bat`、`Tools/PrepareDariusPass2Media.ps1`。WEM → WAV 改为直接调用 `vgmstream-cli`（见 `docs/assets.md`）。
- 外部工具产出的 `docs/*_STATIC_VALIDATION.txt`、打包说明与 `LOL_CHARACTER_MOD.marker`。
- 根目录陈旧构建产物（DLL/PDB 与历史备份）。

### Fixed

- `about/description.txt` 的星座数量（26 → 38）与音量滑条数量（6 → 7）。

### Known issues

- **未编译**：本版本未在装有游戏的机器上构建或运行，等价性待游戏机验证。
- 缺少仓库外的共享源码 `TravelerBasicAttackVfxReplication.cs` 时无法编译（`CheckGameInstall` 会明确报错）。
- 音频仍为 PCM16 WAV（约 160 MB，占发布体积约 70%）；压缩方案与取舍见 [docs/assets.md](docs/assets.md) 的「体积优化」。
