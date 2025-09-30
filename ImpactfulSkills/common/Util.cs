using HarmonyLib;
using System.Reflection.Emit;

namespace ImpactfulSkills.common
{
    internal static class Util
    {
        public static CodeMatcher CreateLabelOffset(this CodeMatcher matcher, out Label label, int offset = 0)
        {
            return matcher.CreateLabelAt(matcher.Pos + offset, out label);
        }
    }
}
