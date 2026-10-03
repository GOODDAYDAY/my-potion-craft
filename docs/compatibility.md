# 兼容性

游戏版本：**2.0.2**（buildid `18969694`，2.0.2 补丁发布于 2025-06-26）。
mod 的上游状态取自 2026-10-03 的 GitHub API 查询。

## 一、矩阵

| Nexus | Mod | 版本 | 状态 | 依据 |
| --- | --- | --- | --- | --- |
| #28 | Auto Haggle | 2.0.0 | ✅ 在用 | 实测加载正常 |
| #30 | Sort Bookmark | 2.0.3 | ✅ 在用 | 实测加载正常 |
| #31 | MoreInformation | 2.0.2 | ✅ 在用 | 实测加载正常 |
| #39 | PotionCraftAutoGarden | 1.1.5 | ✅ 在用 | 实测加载正常；README 声明适配 v2.0.2 |
| #4 | Plentiful Harvest | 2.0.1 | ⛔ 已卸载 | 主动卸载（非崩溃）；且最后更新 2023-06，早于游戏 2.0 |
| #23 | Alchemy Machine Recipes | 1.1.0.0 | ⛔ 未安装 | 作者声明：游戏 v2.0 起该功能已被官方取代，装了会出问题 |
| #25 | Pour Back In | 2.0.1 | ⛔ 已卸载 | 虚表槽位不匹配 → 反射异常，并引发游戏原生崩溃 |
| #32 | Brew From Here | 2.0.1 | ⛔ 已卸载 | 引用的游戏内部类型已被改名 → `TypeLoadException` |

## 二、根因：2.0.2 改动过游戏内部类型

已确认的两处变化：

1. **类型改名**：书签相关状态类型原本是嵌套类型 `Bookmark.MovingState`，在 2.0.2 的
   `PotionCraft.Scripts.dll` 里已不存在该嵌套类型，改成了顶层类型 `BookmarkMovingState`
   （方法签名 `UpdateMovingState(BookmarkMovingState)` 仍然在）。
2. **虚表布局变化**：`PotionCraft.ObjectBased.Stack.StackItem` 的虚方法槽位发生调整，
   按旧布局编译的子类会被 Mono 判定为非法类型。

## 三、两个崩溃案例

### 案例 1：#32 Brew From Here —— TypeLoadException

- 程序集名是 `PotionCraftUsefulRecipeMarks`，dll 名却是 `PotionCraftBrewFromHere.dll`（排查时容易认错）。
- 报错位置：`PotionCraftUsefulRecipeMarks.Scripts.Patches.OldBookmarkOrganizerFailsafePatch`
- 异常类型：`TypeLoadException: ... due to: Could not resolve type with token ... (expected ...)`
- 触发点：游戏启动时扫描类型 → `Settings<T>.TryGetSettingsForInterface()` → 设置界面 `Awake()`
- 根因：mod 里引用的是旧名字 `Bookmark.MovingState`，2.0.2 已改名为 `BookmarkMovingState`。
- 佐证：用 Mono.Cecil 扫描该 dll 的 **179 个类型引用，仅 2 个无法解析**，且都指向此处——
  说明不是"整个 mod 过时"，而是**一处改名**就足以致命。

### 案例 2：#25 Pour Back In —— 反射异常 + 原生崩溃

`Player.log` 原文：

```
ReflectionTypeLoadException: Exception of type 'System.Reflection.ReflectionTypeLoadException' was thrown.
Type PotionCraftPourBackIn.Scripts.UIElements.PotionStackItem has invalid vtable method slot 52 with method PotionCraft.ObjectBased.Stack.StackItem.StackItem:GetItemBoundsForLedge ()
  at (wrapper managed-to-native) System.Reflection.Assembly.GetTypes(System.Reflection.Assembly,bool)
  at System.Reflection.Assembly.GetTypes () ...
  at PotionCraft.Settings.Settings`1[T].TryGetSettingsForInterface[TInterface] ...
  at PotionCraft.ObjectBased.UIElements.GameDifficultySelectButton.Awake () ...
```

含义：Mono 加载 `PotionStackItem` 时发现它的虚表槽位与当前游戏的 `StackItem` 对不上 → 类型非法。

连带后果：游戏自带的调试控制台 `QFSW.QC`（Quantum Console）在后台线程枚举所有类型的方法时
发生**原生崩溃**，Windows 事件日志记录 APPCRASH，崩溃转储位于
`%TEMP%\niceplay games\Potion Craft\Crashes\`。

## 四、判定与处置

1. **定位**：从日志里的类型名前缀找 mod——命名空间通常就是 mod 名
   （`PotionCraftPourBackIn.`、`PotionCraftUsefulRecipeMarks.`）。
2. **处置**：从 `BepInEx\plugins\` 删掉对应 dll 即可，不需要其他操作。
3. **不要试图给旧 mod 打补丁**：这需要针对新 API 重新编译，属于上游作者的工作；
   二进制层面的修改既不可靠也不可维护。

## 五、上游为什么没修

| Mod | 上游最后动作 | 结论 |
| --- | --- | --- |
| Brew From Here | `v2.0.1` / 2025-02-27，此后无提交 | 上游未修 |
| Pour Back In | `v2.0.0.1` / 2024-12-13，此后无提交 | 上游未修 |
| Plentiful Harvest | `2.0.1` / 2023-06-12 | 早于游戏 2.0，上游停滞 |
| Alchemy Machine Recipes | `v1.1.0.0` / 2023-12-13 | 功能已被官方取代，作者主动声明废弃 |

对照：现在能用的 4 个中，AutoGarden 与 2.0.2 补丁**同日**发布，xiaoye97 的两个在补丁后 **2 天**更新。

## 六、经验教训

- **发布时间不能当作兼容性依据**：2024-12 的 Auto Haggle 正常，同一时期发布的 Pour Back In 却崩溃。
  真正决定因素是该 mod patch 了哪些游戏内部类型。
- 唯一可靠的判断方式是**实测 + 看日志**：装完启动一次，检查 `BepInEx\LogOutput.log` 与 `Player.log`。
- 游戏更新（尤其是 2.x 这类大版本内的结构性补丁）后，**先按本文档流程复检一遍**再玩。
