using HarmonyLib;
using PotionCraft.ObjectBased;

namespace MagicShelf
{
    /// <summary>
    /// 拦截货架的点击入口。
    ///
    /// 游戏原本的分发逻辑在 <c>BuildableItemFromInventory.OnPrimaryCursorClick()</c>：
    /// <code>
    /// if (!Managers.BuildMode.IsBuildModeEnabled)
    ///     nonBuildModeInteractionController?.OnPrimaryCursorClickNotInBuildMode();
    /// </code>
    /// 而 <c>nonBuildModeInteractionController</c> 是 Awake 时用 GetComponent 缓存的，
    /// 场景里预摆的货架没有这个组件、运行时也补不上，所以这里用 Prefix 直接接管。
    ///
    /// 返回 false = 这次点击我们处理了，跳过原逻辑；返回 true = 交还给游戏。
    /// </summary>
    [HarmonyPatch(typeof(BuildableItemFromInventory), "OnPrimaryCursorClick")]
    internal static class ShelfClickPatch
    {
        private static bool Prefix(BuildableItemFromInventory __instance)
        {
            if (!MagicShelfPlugin.Enabled.Value)
            {
                return true;
            }

            try
            {
                if (ShelfRestocker.TryRestock(__instance))
                {
                    return false;
                }
            }
            catch (System.Exception e)
            {
                MagicShelfPlugin.Log.LogError("魔法架子处理点击时出错：" + e);
            }

            return true;
        }
    }
}
