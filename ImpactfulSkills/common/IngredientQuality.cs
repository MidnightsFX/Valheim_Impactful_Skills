using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ImpactfulSkills {
    /// <summary>
    /// Shared machinery for the two features built on ingredient quality, plus the vanilla fix both of them
    /// stand on.
    ///
    /// A handful of items carry a quality level that is not an upgrade level - the twelve fish (their size),
    /// and the seven base Ashlands weapons that the infusion recipes eat. Vanilla checks whether you can
    /// afford a recipe one quality tier at a time (Player.HaveRequirementItems takes the largest single tier
    /// count, never the sum), but Player.ConsumeResources then removes with itemQuality -1, which matches any
    /// tier in raw inventory order. So the check and the consumption disagree about which item is being spent,
    /// and your best catch - or your fully upgraded Berzerkr axe - goes into a recipe a junk one would have
    /// covered.
    ///
    /// Tier selection here is always the lowest tier that covers the whole cost, mirroring
    /// Player.GetFirstRequiredItem. A rare ingredient is therefore only ever spent when it is the only one you
    /// are carrying, and because both features pick their tier through this same function, what they pay out
    /// and what gets consumed can never disagree. The one exception is a mod that takes over ConsumeResources
    /// entirely (the craft from containers mods): consumption is then theirs, at vanilla's any tier.
    /// </summary>
    internal static class IngredientQuality {

        /// <summary>
        /// What a recipe earns from the quality of what it eats. A recipe is one or the other, never both -
        /// equipment gets crafted better rather than crafted twice.
        /// </summary>
        internal enum CraftMode {
            None,
            /// <summary>More of the result. <see cref="patches.Fishing"/>.</summary>
            Amount,
            /// <summary>The result at a higher quality level. <see cref="patches.Crafting"/>.</summary>
            ItemQuality,
        }

        // Per tier stack counts, reused between tier selections rather than reallocated - the crafting panel asks
        // for a display amount every frame it is open. Indexed by quality, so it needs maxQuality + 1 entries, and
        // it grows on demand so that a mod raising a max quality past vanilla's 5 cannot truncate the scan.
        private static int[] TierCounts = new int[6];

        /// <summary>
        /// Whether either half of the feature is on. The consumption fix below is shared, so it has to run for
        /// either - if it did not, the quality we hand out and the item we take could come from different tiers.
        /// </summary>
        internal static bool AnyFeatureEnabled {
            get { return ValConfig.EnableQualityIngredientScaling.Value || ValConfig.ScaleCraftedEquipmentQuality.Value; }
        }

        /// <summary>
        /// Whether a requirement is one whose tier we track at all. Ungated on purpose: picking the lowest tier
        /// that covers the cost is a correctness fix, and it should apply to the Ashlands weapons just as much as
        /// to fish.
        /// </summary>
        internal static bool HasQuality(Piece.Requirement requirement) {
            if (requirement == null || requirement.m_resItem == null) { return false; }

            return requirement.m_resItem.m_itemData.m_shared.m_maxQuality > 1;
        }

        /// <summary>
        /// Whether a requirement may earn extra output. Gated to fish by default: handing out more of a result is
        /// a much broader change than transferring a quality level, and without the restriction a mod that raises
        /// m_maxQuality on ordinary materials would silently pull half the recipe book into it.
        /// </summary>
        internal static bool CarriesQuality(Piece.Requirement requirement) {
            if (HasQuality(requirement) == false) { return false; }

            ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
            return ValConfig.RestrictQualityScalingToFish.Value == false || shared.m_itemType == ItemDrop.ItemData.ItemType.Fish;
        }

        /// <summary>
        /// Whether a craft at <paramref name="station"/> charges a requirement at all. Valheim 1.0's upgrader
        /// stations spend only the requirements flagged m_upgraderResource and every other station spends only the
        /// rest, but Piece.Requirement.GetAmount still prices an upgrader resource at its full m_amount - so any loop
        /// over m_resources that means to reflect what a craft actually spends has to repeat the filter from
        /// Player.ConsumeResources.
        /// </summary>
        internal static bool IsSpentAt(Piece.Requirement requirement, CraftingStation station) {
            if (requirement == null || requirement.m_resItem == null) { return false; }

            return station != null ? station.m_upgrader == requirement.m_upgraderResource : requirement.m_upgraderResource == false;
        }

        /// <summary>
        /// Which payout a recipe earns at <paramref name="station"/>, and for <see cref="CraftMode.Amount"/> the
        /// index of the single quality bearing requirement to scale by. Only requirements the station actually
        /// charges are counted. Field reads only, so an ineligible recipe drops out within a handful of dereferences
        /// - there is deliberately no cache, since a recipe lookup table would need invalidating on every ObjectDB
        /// reload.
        /// </summary>
        internal static CraftMode Classify(Recipe recipe, CraftingStation station, out int amountIndex) {
            amountIndex = -1;
            if (recipe == null || recipe.m_item == null) { return CraftMode.None; }
            // Vanilla already scales the "any one of these" recipes (fish -> raw fish) through
            // m_qualityResultAmountMultiplier, so those are left entirely alone.
            if (recipe.m_requireOnlyOneIngredient) { return CraftMode.None; }

            Piece.Requirement[] resources = recipe.m_resources;
            if (resources == null) { return CraftMode.None; }

            int qualityRequirements = 0;
            int scalableRequirements = 0;
            int scalableIndex = -1;
            for (int i = 0; i < resources.Length; i++) {
                if (IsSpentAt(resources[i], station) == false || HasQuality(resources[i]) == false) { continue; }
                qualityRequirements++;
                if (CarriesQuality(resources[i]) == false) { continue; }
                scalableRequirements++;
                scalableIndex = i;
            }
            if (qualityRequirements == 0) { return CraftMode.None; }

            // An upgradeable result is equipment, which is the whole test - it avoids enumerating armor, tool,
            // weapon, bow and shield item types, and it extends to anything a mod makes upgradeable. Averaging is
            // well defined over any number of ingredients, so unlike the amount path this one has no ambiguity
            // problem and the twelve fish of the fishing hat are welcome.
            if (recipe.m_item.m_itemData.m_shared.m_maxQuality > 1) {
                return ValConfig.ScaleCraftedEquipmentQuality.Value ? CraftMode.ItemQuality : CraftMode.None;
            }

            if (ValConfig.EnableQualityIngredientScaling.Value == false) { return CraftMode.None; }
            // Two or more quality ingredients leaves no single tier to scale the amount by.
            if (qualityRequirements > 1 || scalableRequirements != 1) { return CraftMode.None; }
            // Vanilla guards its own craft skill bonus on a stackable result for the same reason. Over crafting a
            // non stacking result is safe (Inventory.CanAddItem counts empty slots, so the craft is refused with
            // "$inventory_full" rather than losing items), but it is easy to find surprising, so it can be turned off.
            if (recipe.m_item.m_itemData.m_shared.m_maxStackSize <= 1 && ValConfig.ScaleNonStackingCraftOutputs.Value == false) { return CraftMode.None; }

            amountIndex = scalableIndex;
            return CraftMode.Amount;
        }

        /// <summary>
        /// How many of the selected recipe the crafting panel is currently offering. Replicates
        /// InventoryGui.UpdateRecipe's own expression rather than reading m_multiCrafting, which is only set once
        /// the craft is pressed - so both the amount preview and the quality preview price the same craft the
        /// player is looking at.
        /// </summary>
        internal static int PanelCraftMultiplier() {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_selectedRecipe.ItemData != null) { return 1; }

            return ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick") ? gui.m_multiCraftAmount : 1;
        }

        /// <summary>
        /// The lowest quality tier holding at least <paramref name="required"/> of the item, or 0 when no single
        /// tier covers the cost. One pass over the inventory rather than a CountItems call per tier, but
        /// replicating CountItems' filters exactly (localized name, world level, stack sizes) so that this cannot
        /// disagree with the affordability check in Player.HaveRequirementItems.
        /// </summary>
        internal static int SelectTier(Inventory inventory, ItemDrop.ItemData prototype, int required) {
            if (prototype == null) { return 0; }

            return SelectTier(inventory, prototype.m_shared.m_name, prototype.m_shared.m_maxQuality, required);
        }

        internal static int SelectTier(Inventory inventory, string name, int maxQuality, int required) {
            if (inventory == null || name == null || required <= 0) { return 0; }

            if (maxQuality <= 1) { return 0; }
            if (TierCounts.Length < maxQuality + 1) { TierCounts = new int[maxQuality + 1]; }
            Array.Clear(TierCounts, 0, maxQuality + 1);

            foreach (ItemDrop.ItemData item in inventory.m_inventory) {
                if (item == null || item.m_shared.m_name != name) { continue; }
                if (item.m_worldLevel < Game.m_worldLevel) { continue; }
                if (item.m_quality < 1 || item.m_quality > maxQuality) { continue; }
                TierCounts[item.m_quality] += item.m_stack;
            }

            for (int quality = 1; quality <= maxQuality; quality++) {
                if (TierCounts[quality] >= required) { return quality; }
            }
            return 0;
        }

        /// <summary>
        /// Stands in for the m_inventory.RemoveItem call inside Player.ConsumeResources, taking the lowest quality
        /// tier that covers the whole amount instead of whatever tier comes first in the inventory. The parameters
        /// are that call's instance followed by its arguments, so swapping the call for this one is stack neutral.
        ///
        /// Tier selection is deterministic on inventory contents, so this agrees with what was handed out a few
        /// lines earlier in DoCrafting by construction - nothing between the two touches the ingredient stacks.
        /// Anything without a quality level, a caller that already chose a tier (itemQuality >= 0), or an amount no
        /// single tier covers falls through to vanilla's own removal unchanged.
        /// </summary>
        internal static void RemoveItemAtLowestTier(Inventory inventory, string name, int amount, int itemQuality, bool worldLevelBased) {
            if (AnyFeatureEnabled && itemQuality < 0) {
                // The requirement itself is not on the stack here, but every stack of an item shares one SharedData,
                // so any held stack answers for its max quality.
                ItemDrop.ItemData held = inventory.GetItem(name);
                int tier = held != null ? SelectTier(inventory, name, held.m_shared.m_maxQuality, amount) : 0;
                if (tier > 0) {
                    Logger.LogDebug($"Consuming {amount} {name} from quality tier {tier}.");
                    itemQuality = tier;
                }
            }
            inventory.RemoveItem(name, amount, itemQuality, worldLevelBased);
        }

        /// <summary>
        /// Vanilla takes a single itemQuality for the whole requirement array, so it cannot ask for a quality 5
        /// anglerfish and quality 1 bread dough in one call - passing 5 would match no bread and consume nothing.
        /// This redirects the one removal inside its loop to <see cref="RemoveItemAtLowestTier"/>, which picks a tier
        /// per requirement, and leaves vanilla's loop, upgrader station filter and amounts untouched.
        ///
        /// This has to be a transpiler, not a replacing prefix. The craft from containers mods (CraftFromContainers,
        /// AzuCraftyBoxes) take over ConsumeResources with a prefix that returns false, and HarmonyX still calls every
        /// later prefix after one returns false - so a second replacing prefix charged the whole recipe again from the
        /// player's inventory. Inside the method body, this is skipped along with the rest of vanilla whenever another
        /// mod takes consumption over. Priority.Last lets a mod transpiling the same call (ValheimPlus's craft from
        /// chests) rewrite the vanilla IL first, in which case the anchor is gone and this steps aside.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        internal static class ConsumeQualityIngredientTierPatch {
            private static readonly MethodInfo InventoryRemoveItemMethod = AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) });
            private static readonly MethodInfo RemoveItemAtLowestTierMethod = AccessTools.Method(typeof(IngredientQuality), nameof(RemoveItemAtLowestTier));

            [HarmonyTranspiler]
            [HarmonyPriority(Priority.Last)]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);

                List<CodeInstruction> removals = code.FindAll(instruction => instruction.Calls(InventoryRemoveItemMethod));
                if (removals.Count != 1) {
                    Logger.LogWarning($"Unable to patch ingredient quality consumption. Expected 1 call to Inventory.RemoveItem in Player.ConsumeResources, found {removals.Count}, it was likely replaced by another mod. Skipping this patch, crafts will consume ingredients of any quality tier.");
                    return code;
                }

                removals[0].opcode = OpCodes.Call;
                removals[0].operand = RemoveItemAtLowestTierMethod;
                return code;
            }
        }
    }
}
