# 游戏内部 API 调查笔记

为开发"魔法架子"而做的游戏逆向调查记录。**游戏反编译产物不收录进本仓库**（体积大且属于游戏资产），
这里只记录结论：要打哪个方法、用什么入口、有哪些坑。

游戏版本：**2.0.2**（游戏主菜单左下角显示 `ST2.0.2.0`；Steam buildid 18969694；Unity 2023.1.13f1；
程序集按 **netstandard2.1** 编译——插件项目也用这个目标框架，用 netstandard2.0 会报 `CS1705`）。

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

## 五、把一瓶药放到货架上（吸附条件，踩过坑）

生成一瓶药很简单（`PotionItem.SpawnNewPotion`），但**能不能吸附到格子上**由
`IgnoreCollisionLedgeTopCollider.CanInteractWithCollider()` 决定，共四个条件：

1. `ledge.IsInitialized()` —— 格子必须已初始化；
2. `ledgeTarget.CanInteractWithLedge()` —— 药水的实现要求 `state == BottleState.Idle`
   （枚举里 `Idle = 0`，正好是新生成瓶子的默认值）且不在天平上；
3. 该物品当前不在这个格子上；
4. **几何条件**：物品包围盒 `min.y > 格子物理碰撞体顶边 - 容差`，且横向与格子有重叠。

第 4 条最容易踩——**格子的物理碰撞体是带 offset 的**：

```csharp
// LedgeController 里的真实定义
Bounds GetLedgePhysicsColliderBounds()
    => new Bounds(physicsCollider.transform.position + physicsCollider.offset, physicsCollider.size);
```

直接拿 `ledge.transform.position` 当生成点是**错的**：瓶子会生成在货架下方，永远吸不上来
（本仓库的 MagicShelf 阶段 1 第一次实测就是这么失败的）。

**稳妥做法**：

```csharp
var b = ledge.GetLedgePhysicsColliderBounds();
var pos = new Vector2(b.center.x + Random.Range(-b.extents.x * 0.6f, b.extents.x * 0.6f), b.max.y + 0.35f);
var item = PotionItem.SpawnNewPotion(pos, potion, Managers.Player.InventoryPanel);
item.OnReleasePrimary(false);     // 对"从未被抓取过"的物品只是收尾：设重力/约束/图层
ledge.AttachItem(item);           // 游戏自己的挂载方法：强制吸附，不赌物理触发器
```

补充：`OnReleasePrimary(false)` 对没被抓过的物品**不会**把瓶子传送回背包——它的内部逻辑里
"放回背包"那条分支要求 `Managers.Cursor.grabbedInteractiveItem == this`，未抓取时为 false，
最终只走 `ReleaseToPlayZone()`（设重力、角速度、图层）。

## 六、"同一种药水"的判定

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

## 七、可见性坑（写插件时会撞上）

| 成员 | 可见性 | 绕法 |
| --- | --- | --- |
| `ItemFromInventory.InventoryItem` | `internal` | Harmony `AccessTools.Property(...)` 反射读取 |
| `LedgeController.physicalItemsOnLedge` | `private` | 改为遍历 `Managers.Game.ItemContainer` 下的 `PotionItem`，用 `IsItemOnLedge()` + `LedgeAttachedTo` 反查 |
| `PotionItem.state` | `public` 字段（`BottleState`） | 可直接读，`Idle` 才是可交互状态 |

## 八、存档相关（阶段 2 做"独立物品"时要用）

| 类型 | 作用 |
| --- | --- |
| `BuildZoneObject.objectId` + `BuildZoneObjectIdGenerator.GetGeneratedId()` + `GetById()` | 摆放物的身份与注册 |
| `IBuildableItemFromInventorySerializedDataController` | **给单个摆放物写自定义存档数据的官方扩展点** |
| `SerializedLedgeTargetData` | 记录物品在哪个格子上、第几个位置（`isItemOnLedge` / `ledgePosition` / `itemIndexOnLedge`） |
| `PotionItem.SpawnFromSerializedData(...)` | 读档时重建药水瓶的完整流程（生成 → `ApplySerializedLedgeTargetData` → `OnReleasePrimary`） |

另：Crucible 框架（`RoboPhred/potioncraft-crucible`）提供"给 NPC 商人加商品"与"共享存档数据"的 API，
但**不提供自定义可摆放物品**——这正是阶段 2 的难点所在。

## 九、踩过的版本坑（2.0.2）

- `Bookmark.MovingState`（嵌套类型）→ 已改名为顶层 `BookmarkMovingState`；
  引用旧名的 mod 启动即抛 `TypeLoadException`（案例：Brew From Here）。
- `PotionCraft.ObjectBased.Stack.StackItem` 虚表槽位变化；
  按旧布局编译的子类被判 `invalid vtable method slot`，并连带游戏自带 Quantum Console 原生崩溃（案例：Pour Back In）。
- 结论：**写插件前先从当前程序集核对类型签名**，别照抄旧教程。
