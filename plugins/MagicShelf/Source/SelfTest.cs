using System.Collections;
using System.Collections.Generic;
using PotionCraft.ManagersSystem;
using PotionCraft.ObjectBased;
using PotionCraft.ObjectBased.Potion;
using UnityEngine;
using PotionDef = PotionCraft.ScriptableObjects.Potion.Potion;

namespace MagicShelf
{
    /// <summary>
    /// 端到端自检（开发/验证用，配置里默认关闭）。
    ///
    /// 做法：等存档真正载入后，找一张**真实货架**，直接调用
    /// <c>BuildableItemFromInventory.OnPrimaryCursorClick()</c>——这正是玩家点击时游戏自己会调用的入口，
    /// 因此走的是与实战完全相同的代码路径（Harmony 前缀 → ShelfRestocker → 生成/扣数/落地）。
    ///
    /// 然后核对：背包里同种药水的数量是否降到 0、货架那一格上的药水瓶是否变多，并把结果写进日志。
    /// 若货架上原本没有药水，会先从背包里放一瓶上去作为参照（取数量 ≥ 2 的那种，留一瓶给自检搬）。
    /// </summary>
    internal static class SelfTest
    {
        private static readonly List<LedgeController> Ledges = new List<LedgeController>();

        internal static IEnumerator Run()
        {
            var log = MagicShelfPlugin.Log;
            log.LogInfo("[自检] 启动，等待存档载入……");

            float waited = 0f;
            while (!IsGameplayReady())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 180f)
                {
                    log.LogWarning("[自检] 等待超时：180 秒内没有检测到已载入的存档/房间，跳过自检。");
                    yield break;
                }
                yield return null;
            }

            log.LogInfo("[自检] 存档已载入，准备中……");
            yield return new WaitForSeconds(2f);

            if (Managers.BuildMode != null && Managers.BuildMode.IsBuildModeEnabled)
            {
                log.LogWarning("[自检] 当前处于建造模式，自检只在非建造模式下有意义，跳过。");
                yield break;
            }

            // ---- 1) 找参照药水：货架上已有的瓶子 ----
            LedgeController targetLedge = null;
            PotionItem reference = null;
            foreach (var ledge in CollectLedges())
            {
                var item = FindPotionOn(ledge);
                if (item != null)
                {
                    targetLedge = ledge;
                    reference = item;
                    break;
                }
            }

            // ---- 2) 货架上空着就先自己放一瓶 ----
            if (reference == null)
            {
                log.LogInfo("[自检] 货架上没有现成的药水，先从背包放一瓶作为参照……");
                yield return PlaceSeedPotion(placed => { targetLedge = placed.Key; reference = placed.Value; });
                if (reference == null || targetLedge == null)
                {
                    log.LogWarning("[自检] 无法在货架上放上参照药水（可能需要背包里有 ≥2 瓶同种药水，或当前房间里没有货架），跳过自检。");
                    yield break;
                }
            }

            var shelf = targetLedge.buildableItem;
            if (shelf == null)
            {
                log.LogWarning("[自检] 目标格子没有所属货架，跳过自检。");
                yield break;
            }

            var potion = ShelfRestocker.GetPotionOf(reference);
            if (potion == null)
            {
                log.LogWarning("[自检] 读不到参照瓶的药水定义，跳过自检。");
                yield break;
            }

            var inventory = Managers.Player.Inventory;
            var key = ShelfRestocker.FindInventoryKey(inventory, potion);
            int before = key != null ? inventory.GetItemCount(key) : 0;
            int itemsBefore = CountPotionsOnShelf(shelf);

            log.LogInfo($"[自检] 货架「{shelf.name}」参照药水【{potion.name}】：" +
                        $"背包 {before} 瓶、架上 {itemsBefore} 瓶");

            if (before <= 0)
            {
                log.LogWarning("[自检] 背包里没有与参照瓶同种的药水，无法验证搬运，跳过。");
                yield break;
            }

            // ---- 3) 调用与“玩家点击”完全相同的入口 ----
            log.LogInfo("[自检] 调用 OnPrimaryCursorClick()（等价于玩家点击该货架）……");
            float startedAt = Time.realtimeSinceStartup;
            shelf.OnPrimaryCursorClick();

            // ---- 4) 等待搬运结束：背包清空或数量不再变化 ----
            int last = before;
            float stableFor = 0f;
            while (Time.realtimeSinceStartup - startedAt < 90f)
            {
                yield return new WaitForSeconds(0.5f);
                int now = inventory.GetItemCount(key);
                if (now == 0) { last = 0; break; }
                if (now == last) { stableFor += 0.5f; if (stableFor >= 5f) break; }
                else { stableFor = 0f; last = now; }
            }

            int after = inventory.GetItemCount(key);
            int itemsAfter = CountPotionsOnShelf(shelf);
            float seconds = Time.realtimeSinceStartup - startedAt;

            // ---- 5) 报告 ----
            bool pass = after < before && itemsAfter > itemsBefore;
            log.LogInfo($"[自检] 结果：背包 {before} → {after} 瓶，架上 {itemsBefore} → {itemsAfter} 瓶，耗时 {seconds:F1} 秒");
            if (pass)
            {
                log.LogInfo($"[自检] ✅ PASS —— 点击货架成功搬运 {before - after} 瓶同种药水到货架上");
            }
            else
            {
                log.LogError($"[自检] ❌ FAIL —— 背包 {before} → {after}，架上 {itemsBefore} → {itemsAfter}，" +
                             "预期背包减少且架上增加。请把这段日志连同上面的明细一起反馈。");
            }
        }

        private static bool IsGameplayReady()
        {
            // 注意：光判断 Managers/PlayerInventoryPanel 不够——主菜单场景里它们也在。
            // 必须等到真的出现了“货架格子”（Ledge），才算进了可玩场景。
            return Managers.Player != null
                   && Managers.Player.Inventory != null
                   && Managers.Player.InventoryPanel != null
                   && Managers.Game != null
                   && Managers.Game.ItemContainer != null
                   && Managers.BuildMode != null
                   && PlayerInventoryPanel.Instance != null
                   && CollectLedges().Count > 0;
        }

        /// <summary>收集当前**已激活**的货架格子（未载入的房间里的格子不算）。</summary>
        private static List<LedgeController> CollectLedges()
        {
            Ledges.Clear();
            if (Managers.BuildMode != null && Managers.BuildMode.ledges != null)
            {
                Managers.BuildMode.ledges.ForEachLedge(ledge =>
                {
                    if (ledge != null && ledge.gameObject != null && ledge.gameObject.activeInHierarchy)
                    {
                        Ledges.Add(ledge);
                    }
                });
            }
            return Ledges;
        }

        private static PotionItem FindPotionOn(LedgeController ledge)
        {
            var container = Managers.Game.ItemContainer;
            foreach (var item in container.GetComponentsInChildren<PotionItem>(true))
            {
                if (item != null && item.IsItemOnLedge() && item.LedgeAttachedTo == ledge)
                {
                    return item;
                }
            }
            return null;
        }

        private static int CountPotionsOnShelf(BuildableItemFromInventory shelf)
        {
            var ledges = shelf.GetComponentsInChildren<LedgeController>(true);
            if (ledges == null || ledges.Length == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (var item in Managers.Game.ItemContainer.GetComponentsInChildren<PotionItem>(true))
            {
                if (item == null || !item.IsItemOnLedge())
                {
                    continue;
                }

                foreach (var ledge in ledges)
                {
                    if (item.LedgeAttachedTo == ledge)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        /// <summary>从背包拿一瓶药放到某个空闲格子上，作为自检的参照瓶。</summary>
        private static IEnumerator PlaceSeedPotion(System.Action<KeyValuePair<LedgeController, PotionItem>> onDone)
        {
            var log = MagicShelfPlugin.Log;
            var inventory = Managers.Player.Inventory;
            var ledges = CollectLedges();
            if (ledges.Count == 0)
            {
                log.LogWarning("[自检] 当前房间里没有可用的货架格子。");
                onDone(default);
                yield break;
            }

            // 找一个已初始化、空着、且所属货架正常的格子
            LedgeController freeLedge = null;
            int uninitialized = 0;
            foreach (var ledge in ledges)
            {
                if (ledge.buildableItem == null || ledge.buildableItem.markedAsDestroyed)
                {
                    continue;
                }
                if (!ledge.IsInitialized())
                {
                    uninitialized++;
                    continue;
                }
                if (FindPotionOn(ledge) == null)
                {
                    freeLedge = ledge;
                    break;
                }
            }

            if (freeLedge == null)
            {
                log.LogWarning($"[自检] 找不到可用的空格子（未初始化的格子 {uninitialized} 个，总格子 {ledges.Count} 个）。");
                onDone(default);
                yield break;
            }

            // 找背包里数量 >= 2 的药水（留至少一瓶给自检搬运）
            PotionDef seed = null;
            foreach (var pair in inventory.items)
            {
                var candidate = pair.Key as PotionDef;
                if (candidate != null && pair.Value >= 2)
                {
                    seed = candidate;
                    break;
                }
            }

            if (seed == null)
            {
                log.LogWarning("[自检] 背包里没有 ≥2 瓶的同种药水，无法自动放置参照瓶。");
                onDone(default);
                yield break;
            }

            Bounds ledgeBounds = freeLedge.GetLedgePhysicsColliderBounds();
            log.LogInfo($"[自检] 目标格子「{freeLedge.name}」碰撞体范围 " +
                        $"x[{ledgeBounds.min.x:F2},{ledgeBounds.max.x:F2}] y[{ledgeBounds.min.y:F2},{ledgeBounds.max.y:F2}]");

            Vector2 position = ShelfRestocker.SpawnPositionFor(freeLedge);
            PotionItem spawned;
            try
            {
                spawned = PotionItem.SpawnNewPotion(position, seed, Managers.Player.InventoryPanel);
            }
            catch (System.Exception e)
            {
                log.LogError("[自检] 生成参照瓶失败：" + e);
                onDone(default);
                yield break;
            }

            inventory.RemoveItem(seed, 1);
            spawned.OnReleasePrimary(false);

            bool attached;
            try
            {
                attached = freeLedge.AttachItem(spawned);
            }
            catch (System.Exception e)
            {
                log.LogError("[自检] 挂载参照瓶出错：" + e);
                onDone(default);
                yield break;
            }

            log.LogInfo($"[自检] 参照瓶生成于 ({position.x:F2}, {position.y:F2})，AttachItem 返回 {attached}，" +
                        $"IsItemOnLedge={spawned.IsItemOnLedge()}");

            for (float t = 0f; t < 8f; t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                if (spawned != null && spawned.IsItemOnLedge())
                {
                    log.LogInfo($"[自检] 参照瓶【{seed.name}】已放上货架格子「{freeLedge.name}」");
                    onDone(new KeyValuePair<LedgeController, PotionItem>(spawned.LedgeAttachedTo, spawned));
                    yield break;
                }
            }

            log.LogWarning("[自检] 参照瓶没有吸附到任何格子上（可能掉在地上了）。");
            onDone(default);
        }
    }
}
