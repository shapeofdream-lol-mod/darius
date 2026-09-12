# docs/

本目录存放设计说明、平衡性记录、视觉修复明细与人工验证清单。
项目总览见仓库根目录的 [README.md](../README.md)，开发约定见 [AGENTS.md](../AGENTS.md)。

| 文档 | 内容 | 类型 |
| --- | --- | --- |
| [assets.md](assets.md) | 资产来源、离线生成流程与版权说明 | 参考 |
| [archive/CHANGELOG-legacy.md](archive/CHANGELOG-legacy.md) | 0.30.x 及更早的 Mod 版本历史（不再维护） | 归档 |
| [BALANCE_PASS_2026-08-30.md](BALANCE_PASS_2026-08-30.md) | 技能成长与星座平衡调整（2026-08-30） | 设计 |
| [PROGRESSION_PASS_2026-08-30.md](PROGRESSION_PASS_2026-08-30.md) | 角色成长曲线与星座解锁重排，含 38 个 `Se_Star_Darius_*` 注册表 | 设计 |
| [MECHA_VFX_HOTFIX2_CHANGELOG.md](MECHA_VFX_HOTFIX2_CHANGELOG.md) | v0.30.5 机神视觉修复明细 | 版本 |
| [MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md](MECHA_VFX_HOTFIX2_TEST_CHECKLIST.md) | v0.30.5 游戏内验证清单 | 测试 |
| [MECHA_VFX_HOTFIX3_CHANGELOG.md](MECHA_VFX_HOTFIX3_CHANGELOG.md) | v0.30.6 机神视觉修复明细 | 版本 |
| [MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md](MECHA_VFX_HOTFIX3_TEST_CHECKLIST.md) | v0.30.6 游戏内验证清单 | 测试 |

> `BALANCE_PASS_*` / `PROGRESSION_PASS_*` 是带日期的**历史快照**，不保证与当前代码一致；当前事实以源码与 `about/metadata.json` 为准。
>
> 早期的 `*_STATIC_VALIDATION.txt` 已移除：它们由仓库外的工具生成、无法复现，且断言会随代码漂移。
