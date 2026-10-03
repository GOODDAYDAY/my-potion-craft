using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PotionCraft.InventorySystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ObjectBased;
using PotionCraft.ObjectBased.Potion;
using PotionCraft.ScriptableObjects;
using UnityEngine;
using PotionDef = PotionCraft.ScriptableObjects.Potion.Potion;

namespace MagicShelf
{
    /// <summary>
    /// 核心逻辑：点击货架 → 把背包里与架上同种的药水全部放到该格。
    ///
    /// 实现要点（都是从游戏自身实现里读出来的）：
    /// 1. 生成药水用游戏自己的 <c>PotionItem.SpawnNewPotion(position, potion, itemsPanel)</c>；
    /// 2. 放下用 <c>OnReleasePrimary(false)</c>，让游戏自己的物理/货架吸附逻辑接管（与存档载入同一条路径）；
    /// 3. 扣背包用 <c>Inventory.RemoveItem</c>，它会顺带触发 UI 刷新；
    /// 4. “同种”用 <c>Potion.IsSame()</c> —— 游戏自己的严格判定（含 usedComponents）。
    /// </summary>
    internal static class ShelfRestocker
    {
        private sealed class Job
        {
            public PotionItem Reference;
            public PotionDef Potion;
            public int Count;
        }

        /// <summary>尝试接管这次点击。返回 true 表示已处理（调用方应跳过游戏原逻辑）。</summary>
        internal static bool TryRestock(BuildableItemFromInventory shelf)
        {
            if (shelf == null)
            {
                return false;
            }

            if (Managers.BuildMode == null || Managers.BuildMode.IsBuildModeEnabled)
            {
                return false;   // 建造模式不归我们管
            }

            if (Managers.Cursor != null && Managers.Cursor.IsGrabbingItem)
            {
                return false;   // 手上有东西时不处理，避免抢玩家的操作
            }

            if (shelf.GetComponent<IBuildableItemFromInventoryNonBuildModeInteractionController>() != null)
            {
                return false;   // 这个货架自带交互（传奇配方架、床等），不要抢
            }

            var ledges = shelf.GetComponentsInChildren<LedgeController>(true);
            if (ledges == null || ledges.Length == 0)
            {
                return false;   // 不是货架（没有格子）
            }

            if (Managers.Player == null || Managers.Player.Inventory == null)
            {
                return false;
            }

            var jobs = BuildJobs(ledges);
            if (jobs.Count == 0)
            {
                return false;   // 架上没有可作为参照的药水 → 交还游戏
            }

            var host = MagicShelfPlugin.CoroutineHost;
            if (host == null)
            {
                MagicShelfPlugin.Log.LogError("协程宿主不存在，无法搬运");
                return false;
            }

            host.StartCoroutine(Restock(jobs));
            return true;
        }

        private static List<Job> BuildJobs(LedgeController[] ledges)
        {
            var jobs = new List<Job>();
            var inventory = Managers.Player.Inventory;

            var container = Managers.Game != null ? Managers.Game.ItemContainer : null;
            if (container == null)
            {
                return jobs;
            }

            var potionItems = container.GetComponentsInChildren<PotionItem>(true);
            if (potionItems == null || potionItems.Length == 0)
            {
                return jobs;
            }

            LedgeController onlyLedge = null;
            if (MagicShelfPlugin.OnlySlotUnderCursor.Value)
            {
                onlyLedge = FindLedgeNearestCursor(potionItems, ledges);
                if (onlyLedge == null)
                {
                    return jobs;
                }
            }

            var handled = new HashSet<PotionDef>();
            foreach (var item in potionItems)
            {
                if (item == null || !item.IsItemOnLedge())
                {
                    continue;
                }

                var ledge = item.LedgeAttachedTo;
                if (ledge == null || Array.IndexOf(ledges, ledge) < 0)
                {
                    continue;   // 不在这张货架上
                }

                if (onlyLedge != null && ledge != onlyLedge)
                {
                    continue;
                }

                var reference = GetPotionOf(item);
                if (reference == null || !handled.Add(reference))
                {
                    continue;   // 同一种药只处理一次
                }

                var key = FindInventoryKey(inventory, reference);
                if (key == null)
                {
                    continue;
                }

                int count = inventory.GetItemCount(key);
                if (count <= 0)
                {
                    continue;
                }

                jobs.Add(new Job { Reference = item, Potion = key, Count = count });
                if (MagicShelfPlugin.VerboseLog.Value)
                {
                    MagicShelfPlugin.Log.LogInfo($"货架上的【{reference.name}】→ 背包里有 {count} 瓶待搬运");
                }
            }

            return jobs;
        }

        private static LedgeController FindLedgeNearestCursor(PotionItem[] potionItems, LedgeController[] ledges)
        {
            Vector2 cursor = Managers.Input.controlsProvider.CurrentMouseWorldPosition;
            LedgeController best = null;
            float bestDistance = float.MaxValue;

            foreach (var item in potionItems)
            {
                if (item == null || !item.IsItemOnLedge())
                {
                    continue;
                }

                var ledge = item.LedgeAttachedTo;
                if (ledge == null || Array.IndexOf(ledges, ledge) < 0)
                {
                    continue;
                }

                float distance = ((Vector2)item.transform.position - cursor).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = ledge;
                }
            }

            return best;
        }

        /// <summary>
        /// 在背包里找与参照瓶“同种”的那一条记录。
        /// 用游戏自己的 IsSame（比名字、自定义标题/描述、效果数组、图标颜色、瓶子、贴纸、基底、实际用料）。
        /// </summary>
        private static PotionDef FindInventoryKey(Inventory inventory, PotionDef reference)
        {
            foreach (var pair in inventory.items)
            {
                var candidate = pair.Key as PotionDef;
                if (candidate == null)
                {
                    continue;
                }

                if (reference.IsSame(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static PropertyInfo inventoryItemProperty;

        /// <summary>
        /// 取一瓶实体药水对应的药水定义。
        /// 游戏里的 <c>ItemFromInventory.InventoryItem</c> 是 internal，插件无法直接访问，
        /// 因此用 Harmony 的 AccessTools 反射读取（这是 BepInEx 插件的标准做法）。
        /// </summary>
        private static PotionDef GetPotionOf(PotionItem item)
        {
            if (inventoryItemProperty == null)
            {
                inventoryItemProperty = AccessTools.Property(typeof(ItemFromInventory), "InventoryItem");
                if (inventoryItemProperty == null)
                {
                    MagicShelfPlugin.Log.LogError(
                        "找不到 ItemFromInventory.InventoryItem —— 游戏版本可能变了，魔法架子无法工作");
                    return null;
                }
            }

            try
            {
                return inventoryItemProperty.GetValue(item, null) as PotionDef;
            }
            catch (Exception e)
            {
                MagicShelfPlugin.Log.LogError("读取药水定义失败：" + e.Message);
                return null;
            }
        }

        private static IEnumerator Restock(List<Job> jobs)
        {
            var inventory = Managers.Player.Inventory;
            int maxPerClick = MagicShelfPlugin.MaxPerClick.Value;
            float interval = MagicShelfPlugin.SpawnInterval.Value;

            int moved = 0;
            bool capped = false;

            foreach (var job in jobs)
            {
                Vector2 basePosition = job.Reference != null
                    ? (Vector2)job.Reference.transform.position
                    : Vector2.zero;

                int remaining = job.Count;
                while (remaining > 0)
                {
                    if (maxPerClick > 0 && moved >= maxPerClick)
                    {
                        capped = true;
                        break;
                    }

                    if (inventory == null || inventory.GetItemCount(job.Potion) <= 0)
                    {
                        break;   // 背包里已经没有了（可能被别的操作拿走）
                    }

                    Vector2 spawnPosition = basePosition + new Vector2(UnityEngine.Random.Range(-0.12f, 0.12f), 0.35f);

                    PotionItem spawned;
                    try
                    {
                        spawned = PotionItem.SpawnNewPotion(spawnPosition, job.Potion, Managers.Player.InventoryPanel);
                    }
                    catch (Exception e)
                    {
                        MagicShelfPlugin.Log.LogError("生成药水失败，停止本次搬运：" + e);
                        yield break;
                    }

                    inventory.RemoveItem(job.Potion, 1);

                    if (spawned != null)
                    {
                        try
                        {
                            spawned.OnReleasePrimary(false);   // 交给游戏自己的物理/吸附逻辑
                        }
                        catch (Exception e)
                        {
                            MagicShelfPlugin.Log.LogError("放下药水时出错：" + e);
                        }
                    }

                    moved++;
                    remaining--;

                    if (interval > 0f)
                    {
                        yield return new WaitForSeconds(interval);
                    }
                    else
                    {
                        yield return null;
                    }
                }

                if (capped)
                {
                    break;
                }
            }

            if (moved > 0)
            {
                string suffix = capped ? $"（已达到单次上限 {maxPerClick}）" : string.Empty;
                MagicShelfPlugin.Log.LogInfo($"魔法架子：本次共搬运 {moved} 瓶药水{suffix}");
            }
        }
    }
}
