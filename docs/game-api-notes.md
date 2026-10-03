# 游戏内部 API 调查笔记

为开发"魔法架子"而做的游戏逆向调查记录。**游戏反编译产物不收录进本仓库**（体积大且属于游戏资产），
这里只记录结论：要打哪个方法、用什么入口、有哪些坑。

游戏版本：**2.0.2**（buildid 18969694，Unity 2023.1.13f1，程序集按 **netstandard2.1** 编译）。

## 一、工具链

| 工具 | 用途 | 备注 |
| --- | --- | --- |
| .NET SDK 8 | 构建插件 | 插件目标框架必须是 `netstandard2.1`，用 2.0 会报 `CS1705` |
| `ilspycmd` 8.2.0.7535 | 把游戏程序集反编译成 C# | **最新版（11.x）的 NuGet 包是坏的**（缺 `DotnetToolSettings.xml`），装 8.2.0.7535 或手动展开 nupkg |
| Mono.Cecil（BepInEx 自带） | 只看类型签名/成员时更快 | 适合"先定位再反编译" |

反编译对象：`Potion Craft_Data\Managed\` 下的 `PotionCraft.*.dll`。
主力是 `PotionCraft.Scripts.dll`（1699 个类型，游戏逻辑全在这里）。

## 二、核心对象地图

| 概念 | 类型 | 命名空间 |
| --- | --- | --- |
| 货架格子 | `LedgeController` | `PotionCraft.ObjectBased` |
| 能放在格子上的东西 | `ILedgeTarget`（接口） | `PotionCraft.ObjectBased` |
| 药水瓶（实体） | `PotionItem` : `PhysicalItemFromInventory` : `ItemFromInventory` | `PotionCraft.ObjectBased.Potion` |
| 药水定义（数据） | `Potion` : `InventoryItem`（ScriptableObject） | `PotionCraft.ScriptableObjects.Potion` |
| 背包 | `Inventory`（`items` 是 `InventoryItemIntDictionary`） | `PotionCraft.InventorySystem` |
| 背包 UI 面板 | `ItemsPanel` / `InventoryPanel` / `PlayerInventoryPanel` | `PotionCraft.InventorySystem` / `PotionCraft.ObjectBased` |

> **背包本质是一个"物品 → 数量"的字典**：`InventoryItemIntDictionary : SerializableDictionary<InventoryItem, int>`。
> 所以"把背包里所有同种药水搬上去"在数据层就是取一个数量 N。

## 三、常用入口

```csharp
Managers.Player.Inventory                     // 玩家背包（Inventory）
Managers.Player.InventoryPanel                // 玩家背包面板（ItemsPanel）
Managers.Game.ItemContainer                   // 所有实体物品的父物体（Transform）
Managers.BuildMode.IsBuildModeEnabled         // 是否处在建造模式
Managers.Cursor.IsGrabbingItem                // 玩家是否正抓着东西
Managers.Input.controlsProvider.CurrentMouseWorldPosition   // 鼠标世界坐标

// 生成一瓶药水（游戏自己的入口）
PotionItem.SpawnNewPotion(Vector2 position, Potion potion, ItemsPanel itemsPanel)

// 放下（交给游戏的物理/货架吸附逻辑，与存档载入同一条路径）
potionItem.OnReleasePrimary(false)

// 货架挂载 / 卸载
ledgeController.AttachItem(ILedgeTarget)      // 并会调用 item.AttachToLedge(ledge)
ledgeController.GetItemOnLedgeIndex(target)
ledgeController.buildableItem                 // 该格子所属的货架对象（public 字段）
```

## 四、"非建造模式点击"怎么做

游戏留了正式接口：

```csharp
public interface IBuildableItemFromInventoryNonBuildModeInteractionController
{
    void OnPrimaryCursorClickNotInBuildMode();     // 点击
    bool OnPrimaryCursorReleaseNotInBuildMode();   // 松开
    bool CanBeOutlinedNotInBuildMode();            // 是否可高亮
    bool CanBeInteractedNowNotInBuildMode();       // 当前是否可交互
}
```

分发点在 `BuildableItemFromInventory.OnPrimaryCursorClick()`：

```csharp
if (!Managers.BuildMode.IsBuildModeEnabled)
    nonBuildModeInteractionController?.OnPrimaryCursorClickNotInBuildMode();
```

⚠️ **坑**：`nonBuildModeInteractionController` 是 Awake 时用 `GetComponent<接口>()` 缓存下来的。
场景里预摆的货架没有这个组件，**运行时 `AddComponent` 也补不上**（缓存不会刷新）。
所以要么改预制体，要么用 Harmony 拦 `OnPrimaryCursorClick()`——本仓库的 MagicShelf 走的是后者。

现成范例（照抄对象）：

| 类型 | 行为 |
| --- | --- |
| `BedController` | 点床 → 弹"开始新的一天"确认框 |
| `LegendaryRecipesShelfController` | 点传奇配方架 → 开关配方窗口 |
| `GrowingSpotController` | 点种植点；它还实现了 `IBuildableItemFromInventorySerializedDataController`（自定义存档数据的范例） |

## 五、"同一种药水"的判定

游戏自己就有严格判定，**不需要自己发明规则**：

```csharp
// InventoryItem（基类）
public virtual bool IsSame(InventoryItem inventoryItem);
public virtual InventoryItem FindSame<T1, T2>(T1 dictionary);

// InventoryItemIntDictionary.SafeIncreaseValue 等内部就是用 FindSame 合并同种物品
```

`Potion.IsSame()` 逐个比较的字段：

| 比较项 | 说明 |
| --- | --- |
| `name` | 资产名 |
| `CustomDescription` / `CustomTitle` / `IsTitleCustom` | 自定义描述与标题 |
| `effects`（`GetArrayOfEffectTypes`） | 效果数组（按效果分组计数后排序比较） |
| `coloredIcon.icon` + `iconColors` | 图标与图标颜色 |
| `bottle` / `sticker` / `stickerAngle` | 瓶子、贴纸、贴纸角度（容差 0.1） |
| `potionBase` | 药水基底 |
| `usedComponents` | **实际用掉的材料**（`SimpleAlchemySubstanceComponents`，本身实现了 `Equals`） |

**没有**参与比较的：`recipeData.recipeMarks`（配方标记）与炼制路径。

## 六、可见性坑（写插件时会撞上）

| 成员 | 可见性 | 绕法 |
| --- | --- | --- |
| `ItemFromInventory.InventoryItem` | `internal` | Harmony `AccessTools.Property(...)` 反射读取 |
| `LedgeController.physicalItemsOnLedge` | `private` | 改为遍历 `Managers.Game.ItemContainer` 下的 `PotionItem`，用 `IsItemOnLedge()` + `LedgeAttachedTo` 反查 |
| `PotionItem.state` | `public` 字段（`BottleState`） | 可直接读，`Idle` 才是可交互状态 |

## 七、存档相关（阶段 2 做"独立物品"时要用）

| 类型 | 作用 |
| --- | --- |
| `BuildZoneObject.objectId` + `BuildZoneObjectIdGenerator.GetGeneratedId()` + `GetById()` | 摆放物的身份与注册 |
| `IBuildableItemFromInventorySerializedDataController` | **给单个摆放物写自定义存档数据的官方扩展点** |
| `SerializedLedgeTargetData` | 记录物品在哪个格子上、第几个位置（`isItemOnLedge` / `ledgePosition` / `itemIndexOnLedge`） |
| `PotionItem.SpawnFromSerializedData(...)` | 读档时重建药水瓶的完整流程（生成 → `ApplySerializedLedgeTargetData` → `OnReleasePrimary`） |

另：Crucible 框架（`RoboPhred/potioncraft-crucible`）提供"给 NPC 商人加商品"与"共享存档数据"的 API，
但**不提供自定义可摆放物品**——这正是阶段 2 的难点所在。

## 八、踩过的版本坑（2.0.2）

- `Bookmark.MovingState`（嵌套类型）→ 已改名为顶层 `BookmarkMovingState`；
  引用旧名的 mod 启动即抛 `TypeLoadException`（案例：Brew From Here）。
- `PotionCraft.ObjectBased.Stack.StackItem` 虚表槽位变化；
  按旧布局编译的子类被判 `invalid vtable method slot`，并连带游戏自带 Quantum Console 原生崩溃（案例：Pour Back In）。
- 结论：**写插件前先从当前程序集核对类型签名**，别照抄旧教程。
