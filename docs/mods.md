# 已启用的 mod 详解

按「需求 → mod」排列。上游源码都以 submodule 形式钉在**与已装二进制对应的 release tag** 上，位于 `upstream/`。

---

## ① 自动讨价还价 —— Auto Haggle

| 项 | 值 |
| --- | --- |
| Nexus | [#28](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/28) |
| 上游 | [xiaoye97/PotionCraft_AutoHaggle](https://github.com/xiaoye97/PotionCraft_AutoHaggle)（MIT） |
| 仓库内源码 | `upstream/PotionCraft_AutoHaggle` @ tag `2.0.0`（commit `bf79b36`） |
| 已装 | `BepInEx\plugins\PotionCraft_AutoHaggle.dll` |
| 配置 | **无**——`BepInEx\config` 下没有它的 cfg，作者未暴露可调项 |

功能：自动讨价还价，装好即生效。上游 README 对功能的描述只有一句 “Auto haggle.”。

---

## ② 整理配方书书签 —— Sort Bookmark

| 项 | 值 |
| --- | --- |
| Nexus | [#30](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/30) |
| 上游 | [xiaoye97/PotionCraft_SortBookmark](https://github.com/xiaoye97/PotionCraft_SortBookmark)（MIT） |
| 仓库内源码 | `upstream/PotionCraft_SortBookmark` @ tag `2.0.3`（commit `040106d`） |
| 已装 | `BepInEx\plugins\PotionCraft_SortBookmark.dll` |
| 配置 | `BepInEx\config\me.xiaoye97.plugin.PotionCraft.SortBookmark.cfg` |

功能：

- 在**配方书界面按 `空格`**，把书签重新排布整齐。
- 空书签统一堆到左上角。
- 想让某个书签不参与排序：在该书签的**药剂描述**里以 `skip` 开头，排序时会跳过它。
- 注意：书签数量**不要超过 148 个**，摆不下时超出的部分会叠在一起。

配置项：

| 键 | 当前值 | 说明 |
| --- | --- | --- |
| `SortHotkey` | `Space` | 触发整理的按键，KeyCode 类型，可改成任意键（`F5`、`Z` 等） |

---

## ③ 自动浇水 / 自动收获 —— PotionCraftAutoGarden

| 项 | 值 |
| --- | --- |
| Nexus | [#39](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/39) |
| 上游 | [ukersn/PotionCraftAutoGarden](https://github.com/ukersn/PotionCraftAutoGarden)（MIT） |
| 仓库内源码 | `upstream/PotionCraftAutoGarden` @ tag `1.1.5`（commit `231dbb8`） |
| 已装 | `BepInEx\plugins\PotionCraftAutoGarden.dll` |
| 配置 | `BepInEx\config\com.ukersn.plugin.AutoGarden.cfg` |
| 兼容性 | 上游 README 明确声明**适配 Potion Craft v2.0.2** |

功能（上游 README 的 5 条）：

1. 一键自动收获 + 浇水植物，并自动收获水晶 → 默认 `F1`
2. 一键自动对屏幕内植物 / 水晶施肥 → 默认 `F2`
3. 每天新的一天自动执行收获、浇水、收水晶 → 默认关闭
4. 自动操作速度可调（快速 / 缓慢）→ 默认缓慢
5. 施肥范围可切换：仅屏幕内 / 所有房间 → 默认仅屏幕内

配置项（**当前值已按仓库需求改过**，改的是 `BepInEx\config\com.ukersn.plugin.AutoGarden.cfg`）：

| 键 | 当前值 | 默认值 | 作用 |
| --- | --- | --- | --- |
| `AutoHarvestWaterOnWakeUp` | `true` | `false` | **每天醒来自动收获植物 + 浇水 + 收水晶**（需求③核心） |
| `EnableQuickOperations` | `true` | `false` | 上述操作瞬间完成；作者推荐与上一项同时开启 |
| `FertilizeAllSeeds` | `true` | `false` | `F2` 施肥范围 = 所有房间的植物与水晶 |
| `AutoHarvestWaterKey` | `F1` | `F1` | 一键收获浇水按键 |
| `AutoFertilizeKey` | `F2` | `F2` | 一键施肥按键 |

注意事项：

- **施肥做不到「每天自动」**：这个 mod 的能力边界就是「每日自动 = 收获+浇水+收水晶；施肥 = 按键触发」。
  已通过 `FertilizeAllSeeds = true` 把一次 `F2` 覆盖到全部房间，这是在现成 mod 框架内最接近的效果。
- 笔记本键盘可能需要 **`Fn` + `F1` / `Fn` + `F2`** 才能触发。
- `EnableQuickOperations` 的代价是执行瞬间可能有轻微卡顿，属于预期行为。

作者另外提到的项目（未安装，仅备注）：[Ukersn's Tweak Wizard](https://github.com/ukersn/Potion-Craft-Ukersn-s-TweakWizard)（无限制种植 + 提升帧数）、
[存档错误修复/编辑器](https://github.com/ukersn/Potion-Craft-Save-File-Error-Fixer-Editor)。

---

## 附加（非三个需求之一）—— MoreInformation

| 项 | 值 |
| --- | --- |
| Nexus | [#31](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/31) |
| 上游 | [xiaoye97/PotionCraft_MoreInformation](https://github.com/xiaoye97/PotionCraft_MoreInformation)（MIT） |
| 仓库内源码 | `upstream/PotionCraft_MoreInformation` @ tag `2.0.2`（commit `2544721`） |
| 已装 | `BepInEx\plugins\PotionCraft_MoreInformation.dll` |
| 配置 | `BepInEx\config\me.xiaoye97.plugin.PotionCraft.MoreInformation.cfg` |

功能（上游 README 的 6 条）：

1. 物品提示里显示物品价值、自己拥有的数量与总价值、药水成本
2. 购买物品时，缺少的药材会提示建议购买
3. 顾客 NPC 的对话中显示他想要的属性
4. 药水与原点的连线更清晰
5. 实时显示药材研磨进度
6. 鼠标覆盖药瓶时瓶子变半透明，能看到被盖住的线

配置项（当前全为 `true`）：

| 键 | 作用 |
| --- | --- |
| `EnableSolventDirectionLine` | 溶剂方向连线 |
| `EnableNPCPotionTips` | NPC 需求属性提示 |
| `EnablePotionTranslucent` | 药瓶半透明 |
| `EnableGrindStatus` | 研磨进度 |
| `EnableSolventDiEnablePriceTooltipsrectionLine` | ⚠️ 键名异常，见下 |

> **已知异常（不影响加载）**：配置文件里有一行的键名是被"合并"过的
> `EnableSolventDiEnablePriceTooltipsrectionLine`，而 dll 里实际存在的是
> `EnableSolventDirectionLine` 与 `EnablePriceTooltips` 两个独立字符串。
> 成因未深究（看起来是运行期字符串拼接/元数据读取的产物）。
> **要改就改文件里现成的那一行、保持键名不动**，不要手工拆成两行——插件读取的是它自己写出的名字。

---

## 相关但未安装的 mod（未实测，仅供将来参考）

| Mod | 作者 | 相关性 |
| --- | --- | --- |
| [PotionCraftBookmarkOrganizer](https://github.com/AndrewFahlgren/PotionCraftBookmarkOrganizer) | AndrewFahlgren | 书签**分组**（Sort Bookmark 只排序不分组）；同作者的另两个 mod 已在 2.0.2 上确认崩溃，装前先备份 |
| [PotionCraft-CustomRooms](https://github.com/TommySoucy/PotionCraft-CustomRooms) | TommySoucy | 自定义房间 |
| [PotionCraft-StorageCellar](https://github.com/TommySoucy/PotionCraft-StorageCellar) | TommySoucy | 可种植矿物的新房间 |
| [potioncraft-crucible](https://github.com/SunsetFi/potioncraft-crucible) | SunsetFi | PotionCraft Modding Framework（自研插件的参考框架） |
| [PotionCraftEnableDev](https://github.com/qe201020335/PotionCraftEnableDev) | qe201020335 | 解锁开发者模式与作弊码 |
| [PotionCraftSaveEditor](https://github.com/foxwhite25/PotionCraftSaveEditor) | foxwhite25 | 存档编辑器 |
