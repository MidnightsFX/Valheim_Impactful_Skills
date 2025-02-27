using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ImpactfulSkills.patches
{
    //[HarmonyPatch(typeof(HitData.DamageTypes), nameof(HitData.DamageTypes.GetTooltipString), typeof(Skills.SkillType))]
    //public static class ItemDisplay
    //{
    //    private static void Postfix(HitData.DamageTypes __instance, Skills.SkillType skillType, ref String __result)
    //    {
    //        if (Player.m_localPlayer == null) { return; }

    //        Player.m_localPlayer.GetSkills().GetRandomSkillRange(out var min, out var max, skillType);

    //        List<String> entry_lines = __result.Split('\n').ToList();
    //        List<String> result_lines = new List<string>(entry_lines);
    //        for (int i = 0; i < entry_lines.Count; i++)
    //        {
    //            // Skip all the short lines, we don't care about them
    //            if (entry_lines[i].Length < 17) { continue; }

    //            string line_desc = entry_lines[i].Split(':')[0].Trim();
    //            switch (line_desc)
    //            {
    //                case "$inventory_chop":
    //                    float player_woodcutting_skill_factor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting);
    //                    float player_chop_bonus = (ValConfig.WoodCuttingDmgMod.Value * (player_woodcutting_skill_factor) / 100f);
    //                    result_lines[i] = AddModifierExplainer(entry_lines[i], __instance.m_chop, (__instance.m_chop * player_chop_bonus), min, max);
    //                    break;
    //            }
    //        }
    //    }

    //    private static string AddModifierExplainer(string current_line, float m_dmg_value, float bonus_dmg, float min, float max)
    //    {
    //        string[] line_arr = current_line.Split(' ');
    //        // Change the damage text color to the specified one
    //        float dmg = int.Parse(line_arr[1].Replace("<color=orange>", "").Replace("</color>", ""));
    //        line_arr[1] = $"<color=purple>{(dmg + bonus_dmg).ToString("F1")}</color>";
    //        // Not sure if this will be more confusing or not, maybe just recoloring is enough
    //        // Add the sum of bonus damage
    //        line_arr[1] += $" <color=purple>[+{bonus_dmg.ToString("F1")}]</color>";
    //        line_arr[2] = $"<color=purple>({Mathf.RoundToInt((m_dmg_value + bonus_dmg) * min)}-{Mathf.RoundToInt((m_dmg_value + bonus_dmg) * max)})";
    //        return string.Join(" ", line_arr);
    //    }
    //}
}
