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

## 自检（开发/验证用）

配置里有个默认关闭的开关：

```ini
[4. 自检（开发用）]
SelfTestOnLoad = true
```

打开后，**载入存档时**插件会自动做一次端到端自检：

1. 找一张真实货架；若架上没有药水，先从背包放一瓶作为参照（取数量 ≥ 2 的那种，留一瓶给自检搬）；
2. 调用 `BuildableItemFromInventory.OnPrimaryCursorClick()`——**这正是玩家点击时游戏自己调用的入口**，
   所以走的是与实战完全相同的代码路径（Harmony 前缀 → 搬运逻辑）；
3. 轮询到搬运结束，核对「背包数量变化 + 架上瓶子数量变化」，在日志里输出 `✅ PASS` 或 `❌ FAIL` 及明细。

日志示例：

```
[自检] 货架「Shelf(Clone)」参照药水【Healing Potion】：背包 12 瓶、架上 1 瓶
[自检] 调用 OnPrimaryCursorClick()（等价于玩家点击该货架）……
[自检] 结果：背包 12 → 0 瓶，架上 1 → 13 瓶，耗时 3.4 秒
[自检] ✅ PASS —— 点击货架成功搬运 12 瓶同种药水到货架上
```

验证完请把 `SelfTestOnLoad` 改回 `false`。自检会真实移动药水，**不要在不想改动的存档上开**。

### 真实鼠标点击模式（可选的更严格验证）

```ini
SelfTestOnLoad = true
SelfTestRealClick = true
```

这个模式下插件**不自己触发**点击，而是：

1. 先在货架上准备好参照瓶（没有就自己放一瓶）；
2. 算出该货架在屏幕上的坐标，打一行 `[自检] CLICKPOINT x y GAMESCREEN w h`；
3. 然后等外部工具把光标移过去**真的点一下**，再核对结果。

注意：**目标货架必须落在当前画面内**（会避开屏幕边缘与右侧面板）。如果日志里出现
`画面外 N 个`，说明镜头没对着货架——先手动把镜头移到货架那边再开自检。
这一模式的用途是连"点击派发"一起验证；日常回归用默认模式就够了。

插件启动时还会打印 Harmony 补丁状态：

```
[Info:Magic Shelf] Harmony 补丁已挂上：BuildableItemFromInventory.OnPrimaryCursorClick
```

补丁没挂上时会打 `LogError`——因为功能静默失效是最难排查的情况。

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
