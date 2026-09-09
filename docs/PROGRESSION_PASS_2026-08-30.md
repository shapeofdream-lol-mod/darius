# 诺手 成长与星座解锁重排（2026-08-30）

- Mod 版本：`0.30.8-balance`
- 技能基础数值：不改。
- 主要伤害技能：继续使用上一轮确定的 **每额外一级 +20% 基础伤害**。
- 熟练度经验：未发现本 Mod 自行加速熟练度/EXP 的逻辑，继续交由游戏原版 `DewProfileStats` 处理。
- 角色专属星座：强制 `isRequiredLevelTotalMastery = false`，按该角色自己的熟练度解锁，避免继承全局星座模板后错误使用账号总熟练度。

## 角色等级成长

每级成长调整为：**+3 AD / +0 AP / +25% 最大生命 / +2 护甲**。
该曲线取值限制在原版 Traveler 已有的成长区间内；不额外添加攻速、移速或技能急速成长。

## 星座解锁层级

统一使用原版角色专属星座常见的熟练度节点：`0 / 3 / 5 / 10 / 15 / 20 / 25 / 30`。
本角色共 38 个星座，分布：Lv0: 2个，Lv3: 2个，Lv5: 7个，Lv10: 10个，Lv15: 7个，Lv20: 5个，Lv25: 3个，Lv30: 2个。

诺克萨斯竞技场、嗷呜！！！均为 30 级；战争热诚/旧版诺克萨斯断头台/霸王血铠 25 级；公理秘术、诺克萨斯之力、瞬发大杀四方等 20 级。

### 完整注册表

| 星座类 | 类别 | 最大等级 | 解锁熟练度 |
|---|---:|---:|---:|
| `Se_Star_Darius_D_Conqueror` | Destruction | 4 | 0 |
| `Se_Star_Darius_D_Triumph` | Destruction | 4 | 3 |
| `Se_Star_Darius_D_Alacrity` | Destruction | 4 | 5 |
| `Se_Star_Darius_D_LastStand` | Destruction | 4 | 10 |
| `Se_Star_Darius_D_AxiomArcanist` | Flexible | 4 | 20 |
| `Se_Star_Darius_D_NoxianArena` | Destruction | 4 | 30 |
| `Se_Star_Darius_L_SecondWind` | Life | 4 | 0 |
| `Se_Star_Darius_L_Overgrowth` | Life | 4 | 5 |
| `Se_Star_Darius_L_Revitalize` | Flexible | 4 | 15 |
| `Se_Star_Darius_L_Conditioning` | Life | 4 | 10 |
| `Se_Star_Darius_L_Unflinching` | Life | 4 | 15 |
| `Se_Star_Darius_I_CripplingStrike` | Flexible | 4 | 5 |
| `Se_Star_Darius_I_Apprehend` | Flexible | 4 | 10 |
| `Se_Star_Darius_I_InstantDecimate` | Flexible | 4 | 20 |
| `Se_Star_Darius_I_WarFervor` | Flexible | 4 | 25 |
| `Se_Star_Darius_I_LegacyCripplingStrike` | Flexible | 4 | 10 |
| `Se_Star_Darius_I_LegacyApprehend` | Flexible | 4 | 15 |
| `Se_Star_Darius_I_LegacyGuillotine` | Flexible | 4 | 25 |
| `Se_Star_Darius_D_BloodRush` | Destruction | 4 | 5 |
| `Se_Star_Darius_D_NoxianMight` | Destruction | 4 | 20 |
| `Se_Star_Darius_D_Dunkmaster` | Destruction | 4 | 15 |
| `Se_Star_Darius_L_BloodPrice` | Life | 4 | 10 |
| `Se_Star_Darius_F_HandOfNoxus` | Imagination | 3 | 15 |
| `Se_Star_Darius_F_NimbusCloak` | Imagination | 3 | 3 |
| `Se_Star_Darius_F_Celerity` | Imagination | 3 | 5 |
| `Se_Star_Darius_F_GatheringStorm` | Imagination | 3 | 15 |
| `Se_Star_Darius_F_CosmicInsight` | Flexible | 3 | 10 |
| `Se_Star_Darius_D_ItemTrinityForce` | Destruction | 4 | 5 |
| `Se_Star_Darius_D_ItemBlackCleaver` | Destruction | 4 | 10 |
| `Se_Star_Darius_D_ItemSpearOfShojin` | Destruction | 4 | 20 |
| `Se_Star_Darius_L_ItemSteraksGage` | Life | 4 | 5 |
| `Se_Star_Darius_L_ItemDeathsDance` | Life | 4 | 15 |
| `Se_Star_Darius_L_ItemOverlordsBloodmail` | Life | 4 | 25 |
| `Se_Star_Darius_I_ItemSunderedSky` | Imagination | 4 | 10 |
| `Se_Star_Darius_I_ItemDeadMansPlate` | Imagination | 4 | 10 |
| `Se_Star_Darius_I_ItemYoumuusGhostblade` | Imagination | 4 | 10 |
| `Se_Star_Darius_F_ItemStridebreaker` | Flexible | 4 | 20 |
| `Se_Star_Darius_F_Awoo` | Flexible | 1 | 30 |

## 兼容性

- 保留原版 Hero 模板的星座槽位解锁/数量逻辑。
- 只改变该角色 StarEffect 的自身熟练度门槛，不改账号全局星座的规则。
- 包内不携带旧根 DLL/PDB；需在实际游戏目录中运行构建脚本，让本机游戏程序集参与编译。
