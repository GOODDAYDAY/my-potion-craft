# 决策与路线

## 一、原则

**复用优先**：

1. 先找现成 mod（Nexus / GitHub / Thunderstore）。
2. 找到就以上游源码 **submodule** 的形式引入，并钉在**与已装二进制对应的 release tag** 上——
   而不是复制粘贴代码，也不是把二进制塞进仓库。
3. 只有上游确实没有的功能，才自己写插件。

## 二、现状：三个需求都已覆盖

| 需求 | 覆盖者 | 状态 |
| --- | --- | --- |
| ① 自动讨价还价 | Auto Haggle | ✅ |
| ② 整理配方书书签 | Sort Bookmark | ✅ |
| ③ 自动浇水、自动收获 | PotionCraftAutoGarden | ✅ |

**这三项需求仍然全部由现成 mod 覆盖。**

## 二·补、第四个需求触发了自研：魔法架子

需求：**架子上放一瓶药 → 点击货架，把背包里同种的药水全部放到该格**（每次点击搬一次，不设上限）。

- 现成方案排查（Nexus / Thunderstore 25 个包 / GitHub）：**没有**实现这个功能的 mod。
  最接近的 `MattDeDuck/PotionCraftShelves` 是"给卧室/商店/地下室加置物架"；`General Utils` 是作弊合集。
- 因此按"没有现成的才自己写"的原则动手：**[plugins/MagicShelf](../plugins/MagicShelf/)**（BepInEx 5 + Harmony）。
- 分两步交付：
  - **阶段 1（已实现，✅ 已通过端到端实测）**：挂在游戏**现有货架**上，非建造模式点击 → 把背包里同种的药水全部搬上去。
    零新增物品、不碰经济系统、不碰存档结构。
  - **阶段 2（待阶段 1 手感确认后再做）**：做成独立物品——克隆架子预制体、颜色区分、商人出售、存档兼容。
- 逆向调查的结论（该拦哪个方法、有哪些可见性坑、存档怎么接）见 [game-api-notes.md](game-api-notes.md)。

## 三、已知缺口（将来可能触发自研）

| 缺口 | 现状 | 可选方案 |
| --- | --- | --- |
| **每天自动施肥** | AutoGarden 只能按 `F2` 手动触发，配置里没有"每日自动施肥"开关；已用 `FertilizeAllSeeds = true` 让一次按键覆盖全部房间 | ① 首选：给上游提 issue / PR（作者 ukersn 的项目在 GitHub 上）<br>② 备选：自研小插件，在"新的一天开始"事件后自动执行施肥 |
| **书签分组**（把书签归类） | Sort Bookmark 只排序，不分组 | [PotionCraftBookmarkOrganizer](https://github.com/AndrewFahlgren/PotionCraftBookmarkOrganizer)——**未实测**。同作者的 Pour Back In / Brew From Here 已在 2.0.2 上确认崩溃，装之前先备份并看日志 |

## 四、将来自己写插件时的入口（备忘，当前不需要）

**框架**：BepInEx 5.4.x 插件——`BepInPlugin` 特性声明 GUID/名称/版本，用 Harmony 做方法补丁；
目标框架 `netstandard2.0` 或 .NET Framework 4.7.2。

**需要引用游戏程序集**：

- `Potion Craft_Data\Managed\PotionCraft.Scripts.dll`
- `Potion Craft_Data\Managed\PotionCraft.Core.dll`

⚠️ 这两个程序集的 `AssemblyVersion` 是 `0.0.0.0`，**无法用版本号判断兼容性**。

**参考源码**：`upstream/` 里的 4 个 mod。其中 xiaoye97 的 Auto Haggle / Sort Bookmark 体量最小
（dll 分别只有 6 KB / 11 KB），适合当模板；ukersn 的 AutoGarden 有完整的配置项写法可参考。

**参考框架**：[SunsetFi/potioncraft-crucible](https://github.com/SunsetFi/potioncraft-crucible)
（PotionCraft Modding Framework）。

**最重要的一条**：编译前先从**当前**游戏程序集里核对目标类型的真实名字与签名。2.0.2 已经改过类型名
（`Bookmark.MovingState` → `BookmarkMovingState`）和虚表布局，照抄旧教程或旧源码会直接得到
[compatibility.md](compatibility.md) 里记录的那两类崩溃。

## 五、动手前的检查清单

将来真的要写插件时，先按顺序确认：

- [ ] Nexus / GitHub / Thunderstore 上确实没有现成实现（三个来源都搜过）
- [ ] 现有 mod 的作者是否还在维护（最后提交时间、issue 是否有人回）
- [ ] 目标功能的游戏内部类型在 2.0.2 下的真实签名已核对
- [ ] 写完后按 [troubleshooting.md](troubleshooting.md) 的流程实测，并把结论补进本仓库文档
