using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImpactfulSkills.common
{
    internal static class Util
    {
        public static CodeMatcher CreateLabelOffset(this CodeMatcher matcher, out Label label, int offset = 0)
        {
            return matcher.CreateLabelAt(matcher.Pos + offset, out label);
        }

        public static CodeMatcher ExtractLabels(this CodeMatcher matcher, out List<Label> labels)
        {
            labels = matcher.Labels;
            foreach (Label label in labels) {
                Logger.LogDebug($"Extracted label: {label.GetHashCode()}");
            }
            matcher.Labels.Clear();

            return matcher;
        }
    }
}
