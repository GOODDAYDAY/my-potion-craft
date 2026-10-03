# MagicShelf（魔法架子）

《Potion Craft》的 BepInEx 插件。**当前阶段：阶段 1 行为原型**。

## 它做什么

在**非建造模式**下**点击货架** → 把背包里与货架上**同种**的药水**全部**放到该格。

- **每次点击搬一次**，不做持续自动收取。
- 不设数量上限（默认搬空背包里的同种药水；可用配置限制）。
- 判定"同种"用的是**游戏自己的 `Potion.IsSame()`**，它比较：asset name、自定义标题与描述、效果数组、图标与图片颜色、瓶子、贴纸（角度容差 0.1）、药水基底、以及**实际用掉的材料 `usedComponents`**——这已经是最严格的现成判定。

## 安装

```powershell
# 需要 .NET SDK 8+ 与已安装 BepInEx 5.4.x 的游戏
pwsh -File build.ps1
```

脚本会自动定位游戏目录、构建、并把 `MagicShelf.dll` 复制到 `<游戏>\BepInEx\plugins\`。
指定路径用 `-GameDir "X:\...\Potion Craft"`；只想构建不部署用 `-NoDeploy`。

手动构建：

```powershell
Copy-Item local.props.example local.props     # 填上你的游戏根目录
dotnet build -c Release
```

## 配置

首次运行后生成 `BepInEx\config\gooddayday.potioncraft.magicshelf.cfg`：

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `Enabled` | `true` | 总开关 |
| `OnlySlotUnderCursor` | `false` | `false` = 处理该货架上所有已放药的格子；`true` = 只处理鼠标指向的那一格 |
| `MaxPerClick` | `0` | 单次点击最多搬多少瓶；`0` = 不限制 |
| `SpawnInterval` | `0.02` | 每瓶之间的间隔（秒），避免一次生成大量物理对象掉帧 |
| `VerboseLog` | `true` | 把搬运明细写进日志 |

## 实现方式（关键调用链）

都是从游戏程序集里反编译读出来的，不是猜的：

| 环节 | 用到的游戏 API |
| --- | --- |
| 点击入口 | Harmony Prefix 拦 `BuildableItemFromInventory.OnPrimaryCursorClick()`；游戏原本在该方法里分发到 `IBuildableItemFromInventoryNonBuildModeInteractionController`，但那个组件是 Awake 时 `GetComponent` 缓存的，场景预摆的货架没有、运行时补不上 |
| 找出货架上的药水 | `Managers.Game.ItemContainer.GetComponentsInChildren<PotionItem>()` + `IsItemOnLedge()` / `LedgeAttachedTo` 比对目标货架的 `LedgeController` |
| 读药水定义 | `ItemFromInventory.InventoryItem` 是 **internal**，用 Harmony `AccessTools` 反射读取 |
| 生成药水 | `PotionItem.SpawnNewPotion(position, potion, Managers.Player.InventoryPanel)` |
| 放到架子上 | `PotionItem.OnReleasePrimary(false)`，交给游戏自己的物理/货架吸附逻辑（与存档载入同一条路径） |
| 扣背包 | `Managers.Player.Inventory.RemoveItem(potion, 1)`（会顺带刷新背包 UI） |

## 已知限制与注意事项

- **阶段 1 是原型**：挂在游戏**现有货架**上，还没有独立的"魔法架子"物品、没有商人出售、没有颜色区分。
- 点击的是一整张货架：如果这张架子上有 3 种药，一次点击会把 3 种药在背包里的存量**全部**搬上来（想只处理单格就把 `OnlySlotUnderCursor` 设为 `true`）。
- 手上有东西（正在拖拽）时点击不会触发。
- 自带交互的货架（例如传奇配方架）会被跳过，不会抢占它们的行为。
- 游戏更新后若 `ItemFromInventory.InventoryItem` 改名，插件会在日志里明确报错并停止工作（不会静默出错）。

## 测试方法

1. 启动游戏，载入一个**货架上已放有药水**、且**背包里有同种药水**的存档。
2. 确认 BepInEx 日志里有：`Magic Shelf 原型已加载：非建造模式点击货架 = 把背包里同种药水全部放上去`。
3. 非建造模式下点击那张货架 → 背包里同种药水应被搬到该格并堆在架子上。
4. 日志里会出现 `货架上的【xxx】→ 背包里有 N 瓶待搬运` 与 `魔法架子：本次共搬运 N 瓶药水`。

## 卸载

删除 `<游戏>\BepInEx\plugins\MagicShelf.dll`（配置文件可留可删）。
