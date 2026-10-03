# 阶段 2 设计：独立的"魔法架子"物品

> 状态：**设计定稿，等阶段 1 手感确认后动手**。所有 API 结论都来自对游戏程序集（v2.0.2）的实际反编译，
> 依据记在 [game-api-notes.md](game-api-notes.md) 第八节。

## 一、需求（用户原话拆解）

1. 新增一个物品：**魔法架子**，外观要和游戏里现有架子有区分度（先靠改颜色实现）。
2. 获取方式：**由商人出售**，能在商人的商品里买到。
3. 摆放：和普通家具一样，可以在任意场景（商店 / 房间 / 地下室）里摆。
4. 架子上是固定格子，一个格子对应一种药水。
5. **非建造模式**下**点击**这个架子 → 把背包里所有同类型药水放进对应格子。
6. 数量不设上限。

## 二、技术方案

### 步骤 1：造出这个物品（复用现成家具资产）

```csharp
// 找一个现成家具（例如 WoodenShelf 系列），克隆它
var magicShelf = ScriptableObject.Instantiate(sourceShelfItem);
magicShelf.name = "MagicShelf";                 // 名字就是存档的键，必须唯一且稳定
BuildableInventoryItemRegistry.Register(magicShelf);   // 加进 BuildableInventoryItem 的 allBuildableItems
```

**依据**：

- `BuildableInventoryItem.allBuildableItems` 是**静态字典**，由 `Initialize()` 从 `Items/Furniture/` 资产目录填充；
  代码里**只有 Add、没有 Clear** → 运行时注册不会被冲掉。
- 读档时摆放物是按**名字**还原的：`InventoryItem.GetByName` → `BuildableInventoryItem.GetFirst(false, name)`
  → **只要注册过，存档就认识它，不需要修改存档格式**。

⚠️ 注意：注册必须发生在读档之前（放 `Awake` 里即可，`Initialize()` 只增不清，先后顺序无所谓）。
⚠️ 名字一旦定下就不能改，否则老存档会找不到物品。

### 步骤 2：颜色区分

两条路，优先试第一条（改动最小）：

| 方案 | 做法 | 风险 |
| --- | --- | --- |
| A. 摆放物上色 | 我们的架子预制体上加一个标记组件；`BuildableItemFromInventory.SpawnNewItem` 之后给它的渲染器上色 | 背包里的**图标**仍是原色，需要另外处理 `GetInventoryIcon()` |
| B. 克隆可视化预制体 | 运行时 `Instantiate` 源家具的 `prefab`，改造其渲染器后赋给 `magicShelf.prefab` | 克隆体要 `DontDestroyOnLoad`；需要确认游戏不会对"非资产预制体"有额外假设 |

两者都要实测确认——这是阶段 2 唯一还没验证过的环节。

### 步骤 3：让商人出售

```csharp
var category = new Category();                                   // PotionCraft.ObjectBased.Deliveries
category.deliveries.Add(new Delivery {
    item = magicShelf, appearingChance = 1f, minCount = 1, maxCount = 1,
    applyDiscounts = false, applyExtraCharge = false,
});
npc.trading.deliveriesCategories.Add(category);
```

**依据**：游戏自己就是这么给商人铺货的（见 game-api-notes 8.2 的代码摘录），
而且那段代码里连 `BuildableInventoryItem`（家具）都一起塞进配送列表 → 家具出现在商人商品里是游戏支持的用法。

时机：在 NPC 应用 `TraderSettings`（`ApplyPartTo`）之后注入，避免被覆盖。
若只想让特定商人卖，就按 NPC 模板/名字过滤。

### 步骤 4：点击行为

直接复用阶段 1 已经验证过的搬运逻辑（`ShelfRestocker`），但**不再需要 Harmony 前缀**：

- 在魔法架子的预制体上挂一个实现 `IBuildableItemFromInventoryNonBuildModeInteractionController`
  的组件（四个方法照抄 `LegendaryRecipesShelfController`），点击分发由游戏自己完成；
- 阶段 1 的调查结论：该组件是 `BuildableItemFromInventory` 在 Awake 时 `GetComponent` 缓存的，
  **运行时 `AddComponent` 补不上** → 所以要在**预制体**上就带好（正好走"克隆预制体"这条路）。

若要做到"每个格子记住自己认领的药水类型"（超出当前需求，用户明确选了"每次点击搬一次"），
再叠加 `IBuildableItemFromInventorySerializedDataController` 存自定义数据——**当前不做**。

## 三、分步交付与验收

| 里程碑 | 内容 | 验收标准（可验证） |
| --- | --- | --- |
| M1 | 物品存在、可在建造模式里摆出来 | 建造菜单里能看到"魔法架子"并能摆到房间里 |
| M2 | 颜色与普通架子不同 | 截图对比可见差异 |
| M3 | 点击搬运同种药水 | 复用阶段 1 的自检（`SelfTestOnLoad`）跑出 `✅ PASS` |
| M4 | 商人出售 | 在商人商品里能买到；买到的能正常摆 |
| M5 | 存档回归 | 摆好 → 存档 → 读档：架子还在、位置对、颜色对；**存档与备份无损坏** |

每个里程碑都按仓库既有流程验证：先备份存档（`SavesSteam\`）→ 改代码 → 构建部署 → 进游戏实测 → 日志留证 → 回填文档。

## 四、风险与对策

| 风险 | 等级 | 对策 |
| --- | --- | --- |
| **存档兼容**（最大风险） | 高 | 名字注册机制已确认可行；每次测试前备份存档并逐文件比对 SHA256；出问题就把物品从注册表摘掉，存档里的未知物品由游戏自行忽略 |
| 预制体克隆的副作用 | 中 | 先做最小方案（复用现成架子 + 只上色），不做从零造预制体 |
| 商人商品注入时机 | 中 | 在 `ApplyPartTo` 之后注入；只影响目标 NPC |
| 与其它 mod 冲突 | 低 | 我们的物品名与组件都带独立命名空间；不改动游戏原有资产 |
| 游戏更新 | 中 | 沿用阶段 1 的纪律：改动前先从当前程序集核对类型签名 |

## 五、明确不做的事

- 不修改游戏原有资产（只新增我们自己的克隆体）。
- 不做"每格自动持续收取"（用户选了"每次点击搬一次"）。
- 不动存档格式；不引入外部依赖（除非决定采用 Crucible，届时单独评估）。
