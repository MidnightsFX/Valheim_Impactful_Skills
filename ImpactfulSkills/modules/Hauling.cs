using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ImpactfulSkills.patches {
    internal class Hauling {
        public static Skills.SkillType HaulingSkill = 0;
        public static void SetupHaulingSkill() {
            SkillConfig hauling = new SkillConfig();
            hauling.Name = "$skill_Hauling";
            hauling.Description = "$skill_Hauling_description";
            hauling.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/hauling.png");
            hauling.Identifier = "midnightsfx.hauling";
            hauling.IncreaseStep = 0.1f;
            HaulingSkill = SkillManager.Instance.AddSkill(hauling);
        }

        [HarmonyPatch(typeof(Player))]
        private static class PlayerCarryWeightPatch {
            [HarmonyPatch(nameof(Player.GetMaxCarryWeight))]
            private static void Postfix(Player __instance, ref float __result) {
                if (ValConfig.EnableHauling.Value == false || ValConfig.EnableCarryWeightBonus.Value == false) { return; }

                __result += (__instance.GetSkillFactor(HaulingSkill) * ValConfig.HaulingMaxWeightBonus.Value);
            }
        }

        [HarmonyPatch(typeof(Vagon))]
        private static class VagonMassPatch {
            [HarmonyPatch(nameof(Vagon.SetMass))]
            private static void Prefix(ref float mass) {
                // This is only called by the znet view owner of the cart
                if (ValConfig.EnableHauling.Value == false || ValConfig.EnableHaulingCartMassReduction.Value == false || Player.m_localPlayer == null) { return; }

                mass *= (1 - (Player.m_localPlayer.GetSkillFactor(HaulingSkill) * ValConfig.HaulingCartMassReduction.Value));
            }
        }
    }

    /// <summary>
    /// Grants all of hauling's movement XP - pulling a cart, and travelling with a loaded inventory -
    /// from a single scheduled check on the local player, rather than from per frame patches on
    /// Vagon.LateUpdate and Player.RaiseSkill. The game itself uses this pattern for the cart
    /// (Vagon.Awake schedules UpdateMass/UpdateLoadVisualization), and it means the cost of hauling
    /// is one call every few seconds no matter how many carts or players are loaded.
    ///
    /// Because this measures raw position change instead of piggybacking the Run skill, walking counts
    /// as well as running, and being over your carry limit no longer stops XP - it now pays a bonus,
    /// since the XP scales with how loaded you are.
    /// </summary>
    internal class HaulingXPTracker : MonoBehaviour {
        internal static HaulingXPTracker instance;

        // Tracked so that a new Player object - respawn, logout, world change - re-baselines the
        // position instead of counting the gap as distance travelled.
        private Player trackedPlayer;
        private Vector3 lastPosition;

        /// <summary>
        /// Creates the tracker on its own object so that it survives scene loads and does not need a
        /// player to exist yet. Called once from the plugin Awake.
        /// </summary>
        internal static void Create() {
            if (instance != null) { return; }
            GameObject tracker = new GameObject("ImpactfulSkills_HaulingXPTracker");
            UnityEngine.Object.DontDestroyOnLoad(tracker);
            tracker.AddComponent<HaulingXPTracker>();
        }

        private void Awake() {
            instance = this;
            Reschedule();
        }

        internal void Reschedule() {
            CancelInvoke(nameof(CheckHaulingXP));
            float interval = Mathf.Max(1f, ValConfig.HaulingXPCheckInterval.Value);
            Logger.LogDebug($"Scheduling hauling XP checks every {interval} seconds.");
            InvokeRepeating(nameof(CheckHaulingXP), interval, interval);
        }

        /// <summary>
        /// The interval is server synced, so an admin changing it has to re-arm the timer.
        /// </summary>
        internal static void OnIntervalChanged(object sender, EventArgs e) {
            if (instance != null) { instance.Reschedule(); }
        }

        private void CheckHaulingXP() {
            if (ValConfig.EnableHauling.Value == false) { return; }
            Player player = Player.m_localPlayer;
            if (player == null) { trackedPlayer = null; return; }

            // First check for this player object, just record where they are.
            if (trackedPlayer != player) {
                trackedPlayer = player;
                lastPosition = player.transform.position;
                return;
            }

            float distance = Vector3.Distance(lastPosition, player.transform.position);

            // Movement that isn't hauling: boats are voyagers job, and portals aren't travel at all.
            // The distance sanity check catches teleports that finished before this check ran, along
            // with anything else that moved the player faster than they could possibly walk.
            if (player.IsTeleporting() || player.IsDead() || player.InIntro() ||
                player.IsAttachedToShip() || player.GetStandingOnShip() != null ||
                distance > (ValConfig.HaulingXPCheckInterval.Value * 50f)) {
                lastPosition = player.transform.position;
                return;
            }

            // If you haven't moved far enough, don't update the last position. That lets slow or
            // interrupted travel accumulate across checks instead of being discarded, and stops
            // running in circles from paying out. Setting the distance to 0 disables this check.
            Logger.LogDebug($"Checking distance traveled: {distance}");
            if (distance < ValConfig.HaulingXPMinDistance.Value) { return; }
            lastPosition = player.transform.position;

            GrantCartXP(player);
            GrantCarryWeightXP(player);
        }

        /// <summary>
        /// XP for pulling a cart, scaled by the carts mass. Only one cart can be attached at a time
        /// (Vagon.AttachTo detaches everything first) and the instance list only holds the carts loaded
        /// nearby, so this is a very short scan.
        /// </summary>
        private static void GrantCartXP(Player player) {
            Vagon cart = null;
            foreach (Vagon entry in Vagon.m_instances) {
                if (entry != null && entry.IsAttached(player)) {
                    cart = entry;
                    break;
                }
            }
            if (cart == null || cart.m_bodies == null) { return; }

            float totalmass = 0f;
            foreach (var entry in cart.m_bodies) {
                totalmass += entry.mass;
            }
            Logger.LogDebug($"Raising hauling skill: {ValConfig.HaulingXPRate.Value * (totalmass * 0.3f)} = {totalmass} * 0.3 * {ValConfig.HaulingXPRate.Value}");
            player.RaiseSkill(Hauling.HaulingSkill, ValConfig.HaulingXPRate.Value * (totalmass * 0.3f));
        }

        /// <summary>
        /// XP for moving your own goods around, gated on carrying at least a set amount of weight and
        /// scaled by how loaded you are. Being over your carry limit is worth more than being at it,
        /// up to the configured ceiling.
        /// </summary>
        private static void GrantCarryWeightXP(Player player) {
            if (ValConfig.EnableHaulingCarryWeightXP.Value == false) { return; }

            float carried = player.GetInventory().GetTotalWeight();
            if (carried < ValConfig.HaulingCarryWeightXPMinWeight.Value) { return; }

            // GetMaxCarryWeight already includes the hauling bonus applied by PlayerCarryWeightPatch
            // as well as any Megingjord effect, so this ratio matches the on-screen weight bar.
            float maxWeight = player.GetMaxCarryWeight();
            if (maxWeight <= 0f) { return; }
            float loadRatio = Mathf.Min(carried / maxWeight, ValConfig.HaulingMaxLoadRatio.Value);

            float xp = ValConfig.HaulingCarryWeightXPRate.Value * loadRatio;
            Logger.LogDebug($"Raising hauling skill from carried weight: {xp} = {loadRatio} load ratio * {ValConfig.HaulingCarryWeightXPRate.Value}");
            player.RaiseSkill(Hauling.HaulingSkill, xp);
        }
    }
}
