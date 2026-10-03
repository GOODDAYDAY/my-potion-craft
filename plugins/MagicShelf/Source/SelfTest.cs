using System.Collections;
using System.Collections.Generic;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Room;
using PotionCraft.ObjectBased;
using PotionCraft.ObjectBased.Potion;
using PotionCraft.Settings;
using UnityEngine;
using PotionDef = PotionCraft.ScriptableObjects.Potion.Potion;

namespace MagicShelf
{
    /// <summary>
    /// 端到端自检（开发/验证用，配置里默认关闭）。
    ///
    /// 两种模式：
    /// * <c>SelfTestRealClick = false</c>（默认）：直接调用 <c>OnPrimaryCursorClick()</c>——
    ///   这正是玩家点击时游戏自己会调用的入口，走与实战相同的代码路径。
    /// * <c>SelfTestRealClick = true</c>：先算出货架在屏幕上的坐标并打成
    ///   <c>[自检] CLICKPOINT x y</c>，然后等外部工具把光标移过去**真的点一下**。
    ///   这样连"点击派发"这一环也一起验证了（用来验证的货架必须在当前画面内）。
    ///
    /// 若货架上原本没有药水，会先从背包放一瓶作为参照（取数量 ≥ 2 的那种，留一瓶给自检搬）。
    /// </summary>
    internal static class SelfTest
    {
        private static readonly List<LedgeController> Ledges = new List<LedgeController>();

        internal static IEnumerator Run()
        {
            var log = MagicShelfPlugin.Log;
            bool realClick = MagicShelfPlugin.SelfTestRealClick.Value;
            log.LogInfo($"[自检] 启动（模式：{(realClick ? "真实鼠标点击" : "直接调用点击入口")}），等待存档载入……");

            float waited = 0f;
            while (!IsGameplayReady())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 180f)
                {
                    log.LogWarning("[自检] 等待超时：180 秒内没有检测到已载入的房间与货架，跳过自检。");
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

            // ---- 1) 找参照药水：货架上已有的瓶子（真实点击模式下要求货架在画面内）----
            if (realClick)
            {
                // 画面里没有货架就先切房间（走游戏自己的 GoTo，与玩家切房间同一条路径）
                yield return EnsureOnScreenLedge();
            }

            LedgeController targetLedge = null;
            PotionItem reference = null;
            foreach (var ledge in CollectLedges())
            {
                if (!ledge.IsInitialized())
                {
                    continue;
                }

                var item = FindPotionOn(ledge);
                if (item == null)
                {
                    continue;
                }

                if (realClick && !IsClickableOnScreen(ledge))
                {
                    continue;
                }

                targetLedge = ledge;
                reference = item;
                break;
            }

            // ---- 2) 货架上空着就先自己放一瓶 ----
            if (reference == null)
            {
                log.LogInfo("[自检] 货架上没有可用的现成参照药水，先从背包放一瓶……");
                yield return PlaceSeedPotion(realClick, placed =>
                {
                    targetLedge = placed.Key;
                    reference = placed.Value;
                });

                if (reference == null || targetLedge == null)
                {
                    log.LogWarning("[自检] 无法准备参照瓶（可能背包里没有 ≥2 瓶同种药水，或画面内/房间里没有可用货架），跳过自检。");
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

            log.LogInfo($"[自检] 货架「{shelf.name}」参照药水【{potion.name}】：背包 {before} 瓶、架上 {itemsBefore} 瓶");
            if (before <= 0)
            {
                log.LogWarning("[自检] 背包里没有与参照瓶同种的药水，无法验证搬运，跳过。");
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;

            if (realClick)
            {
                // ---- 3a) 真实点击模式：把屏幕坐标打给外部工具，然后等背包数量下降 ----
                Vector3 world = GetClickWorldPoint(shelf, targetLedge);
                var cam = Managers.Game.Cam;
                if (cam == null)
                {
                    log.LogWarning("[自检] 拿不到主相机，跳过。");
                    yield break;
                }

                Vector3 screenPoint = cam.WorldToScreenPoint(world);
                int clickX = Mathf.RoundToInt(screenPoint.x);
                int clickY = Mathf.RoundToInt(Screen.height - screenPoint.y);   // 转成左上角原点的屏幕坐标

                log.LogInfo($"[自检] CLICKPOINT {clickX} {clickY} GAMESCREEN {Screen.width} {Screen.height}");
                log.LogInfo("[自检] 等待真实鼠标点击……");

                while (Time.realtimeSinceStartup - startedAt < 90f)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (inventory.GetItemCount(key) < before)
                    {
                        break;
                    }
                }
            }
            else
            {
                // ---- 3b) 直接调用入口：等价于点击，只是不经过鼠标派发 ----
                log.LogInfo("[自检] 调用 OnPrimaryCursorClick()（等价于玩家点击该货架）……");
                shelf.OnPrimaryCursorClick();

                int last = before;
                float stableFor = 0f;
                while (Time.realtimeSinceStartup - startedAt < 90f)
                {
                    yield return new WaitForSeconds(0.5f);
                    int now = inventory.GetItemCount(key);
                    if (now == 0)
                    {
                        last = 0;
                        break;
                    }
                    if (now == last)
                    {
                        stableFor += 0.5f;
                        if (stableFor >= 5f)
                        {
                            break;
                        }
                    }
                    else
                    {
                        stableFor = 0f;
                        last = now;
                    }
                }
            }

            // ---- 4) 报告 ----
            int after = inventory.GetItemCount(key);
            int itemsAfter = CountPotionsOnShelf(shelf);
            float seconds = Time.realtimeSinceStartup - startedAt;
            bool pass = after < before && itemsAfter > itemsBefore;

            log.LogInfo($"[自检] 结果：背包 {before} → {after} 瓶，架上 {itemsBefore} → {itemsAfter} 瓶，耗时 {seconds:F1} 秒");
            if (pass)
            {
                log.LogInfo($"[自检] ✅ PASS —— {(realClick ? "真实鼠标点击" : "调用点击入口")}成功搬运 {before - after} 瓶同种药水到货架上");
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

        /// <summary>画面内是否存在已初始化的货架格子。</summary>
        private static bool HasOnScreenLedge()
        {
            foreach (var ledge in CollectLedges())
            {
                if (ledge.IsInitialized() && IsClickableOnScreen(ledge))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 真实点击模式专用：当前画面里没有货架时，挨个切到未锁定的房间去找。
        /// 用的是游戏自己的 <c>Managers.Room.GoTo(...)</c>——它同时也是开发者命令 `GoToRoom` 的实现，
        /// 所以是"正规操作"，不会搞乱场景状态。
        /// </summary>
        private static IEnumerator EnsureOnScreenLedge()
        {
            if (HasOnScreenLedge())
            {
                yield break;
            }

            var log = MagicShelfPlugin.Log;
            var rooms = Settings<RoomManagerSettings>.Asset.rooms;
            if (rooms == null)
            {
                yield break;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (room == null || room.IsLocked || !room.IsLoaded)
                {
                    continue;
                }

                var index = room.GetRoomIndex();
                if (index == Managers.Room.CurrentRoomIndex)
                {
                    continue;
                }

                log.LogInfo($"[自检] 当前画面内没有货架，尝试切到房间「{room.name}」……");
                Managers.Room.GoTo(index);

                float waited = 0f;
                while (Managers.Room.CameraMover.IsMoving() && waited < 15f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                yield return new WaitForSeconds(1.5f);

                if (HasOnScreenLedge())
                {
                    log.LogInfo($"[自检] 在房间「{room.name}」找到了画面内的货架");
                    yield break;
                }
            }

            log.LogWarning("[自检] 所有已解锁房间里都没找到画面内的货架。");
            DumpLedgeDiagnostics();
        }

        /// <summary>画面内找不到货架时，把前几个格子的坐标打出来，便于判断是"真的在画面外"还是判据有问题。</summary>
        private static void DumpLedgeDiagnostics()
        {
            var log = MagicShelfPlugin.Log;
            var cam = Managers.Game != null ? Managers.Game.Cam : null;
            if (cam == null)
            {
                return;
            }

            int shown = 0;
            foreach (var ledge in CollectLedges())
            {
                if (shown >= 4)
                {
                    break;
                }

                var bounds = ledge.GetLedgePhysicsColliderBounds();
                Vector3 world = GetClickWorldPoint(ledge.buildableItem, ledge);
                Vector3 sp = cam.WorldToScreenPoint(world);
                log.LogInfo($"[自检] 诊断：格子「{ledge.name}」初始化={ledge.IsInitialized()} " +
                            $"世界坐标({world.x:F2},{world.y:F2}) → 屏幕({sp.x:F0},{sp.y:F0},z={sp.z:F2}) " +
                            $"画布 {Screen.width}x{Screen.height}，格子范围 x[{bounds.min.x:F2},{bounds.max.x:F2}]");
                shown++;
            }
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

        /// <summary>这个格子的点击位置是否在当前画面内（留出边距，避免点到 UI 上）。</summary>
        private static bool IsClickableOnScreen(LedgeController ledge)
        {
            var cam = Managers.Game != null ? Managers.Game.Cam : null;
            if (cam == null)
            {
                return false;
            }

            Vector3 world = GetClickWorldPoint(ledge.buildableItem, ledge);
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f)
            {
                return false;
            }

            const float margin = 150f;   // 避开屏幕边缘与右侧面板
            return sp.x > margin && sp.x < Screen.width - margin
                   && sp.y > margin && sp.y < Screen.height - margin;
        }

        /// <summary>
        /// 点击点：用**格子**的碰撞体来算（货架主碰撞体对桌子之类的物体可能很大或偏移，
        /// 用它算出来的点会跑到屏幕外）。取靠右的位置，避开刚放上去的参照瓶。
        /// </summary>
        private static Vector3 GetClickWorldPoint(BuildableItemFromInventory shelf, LedgeController ledge)
        {
            Bounds bounds = ledge.GetLedgePhysicsColliderBounds();
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, 0.8f);
            return new Vector3(x, bounds.center.y, 0f);
        }

        /// <summary>从背包拿一瓶药放到某个空闲格子上，作为自检的参照瓶。</summary>
        private static IEnumerator PlaceSeedPotion(bool requireOnScreen, System.Action<KeyValuePair<LedgeController, PotionItem>> onDone)
        {
            var log = MagicShelfPlugin.Log;
            var inventory = Managers.Player.Inventory;
            var ledges = CollectLedges();
            if (ledges.Count == 0)
            {
                log.LogWarning("[自检] 当前没有可用的货架格子。");
                onDone(default);
                yield break;
            }

            // 找一个已初始化、空着、所属货架正常的格子
            LedgeController freeLedge = null;
            int uninitialized = 0;
            int offScreen = 0;
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
                if (FindPotionOn(ledge) != null)
                {
                    continue;
                }
                if (requireOnScreen && !IsClickableOnScreen(ledge))
                {
                    offScreen++;
                    continue;
                }
                freeLedge = ledge;
                break;
            }

            if (freeLedge == null)
            {
                log.LogWarning($"[自检] 找不到可用的空格子（未初始化 {uninitialized} 个、画面外 {offScreen} 个、总格子 {ledges.Count} 个）。");
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

            // 参照瓶放在格子靠左的位置，点击点取靠右，避免互相干扰
            Vector2 position = ShelfRestocker.SpawnPositionFor(freeLedge, 0.35f, 0.2f);
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
