using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MagicShelf
{
    /// <summary>
    /// 魔法架子（原型·阶段 1）
    ///
    /// 行为：在**非建造模式**下点击货架 → 把背包里与货架上同种的药水全部放到该格。
    /// 每次点击搬一次，不做持续自动收取。
    ///
    /// “同种”的判定直接使用游戏自己的 <c>Potion.IsSame()</c>：它比较 name、自定义标题/描述、
    /// 效果数组、图标与颜色、瓶子、贴纸（角度容差 0.1）、基底、以及**实际用掉的材料**
    /// （usedComponents）——已是最严格的现成判定，见 docs/ 里的调查记录。
    /// </summary>
    [BepInPlugin(Guid, "Magic Shelf", "0.1.0")]
    public class MagicShelfPlugin : BaseUnityPlugin
    {
        public const string Guid = "gooddayday.potioncraft.magicshelf";

        internal static MagicShelfPlugin Instance;
        internal static ManualLogSource Log;
        internal static MonoBehaviour CoroutineHost;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> OnlySlotUnderCursor;
        internal static ConfigEntry<int> MaxPerClick;
        internal static ConfigEntry<float> SpawnInterval;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind(
                "1. 开关", "Enabled", true,
                "是否启用：非建造模式下点击货架，把背包里与架上同种的药水全部放上去");

            OnlySlotUnderCursor = Config.Bind(
                "2. 行为", "OnlySlotUnderCursor", false,
                "true = 只处理鼠标指向的那一格；false = 处理该货架上所有已放药水的格子");

            MaxPerClick = Config.Bind(
                "2. 行为", "MaxPerClick", 0,
                "单次点击最多搬运多少瓶；0 = 不限制（按你有多少瓶就搬多少瓶）");

            SpawnInterval = Config.Bind(
                "2. 行为", "SpawnInterval", 0.02f,
                "每瓶之间的间隔秒数。越小越快，但一次生成大量物理对象可能掉帧；0 = 每帧一瓶");

            VerboseLog = Config.Bind(
                "3. 调试", "VerboseLog", true,
                "把每次搬运的明细写进 BepInEx 日志");

            var host = new GameObject("MagicShelf.CoroutineHost");
            Object.DontDestroyOnLoad(host);
            CoroutineHost = host.AddComponent<CoroutineRunner>();

            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(ShelfClickPatch));

            Log.LogInfo("Magic Shelf 原型已加载：非建造模式点击货架 = 把背包里同种药水全部放上去");
        }

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }
    }

    /// <summary>只为跑协程而存在的宿主组件。</summary>
    internal sealed class CoroutineRunner : MonoBehaviour
    {
    }
}
