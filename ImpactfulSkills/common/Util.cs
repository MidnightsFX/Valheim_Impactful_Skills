using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ImpactfulSkills.common
{
    internal static class Util
    {
        public static CodeMatcher CreateLabelOffset(this CodeMatcher matcher, out Label label, int offset = 0)
        {
            return matcher.CreateLabelAt(matcher.Pos + offset, out label);
        }

        /// <summary>
        /// Runs a forward match without mutating anything when it fails. A failed
        /// <see cref="CodeMatcher.MatchStartForward"/> leaves Pos at -1, so any chained
        /// RemoveInstruction/Insert throws an out of range exception - and a throwing
        /// transpiler makes HarmonyX discard every patch on the target method, including
        /// other mods'. Bail out through this instead so a stolen anchor only costs us
        /// our own patch.
        /// </summary>
        public static bool TryMatchStartForward(this CodeMatcher matcher, string failureMessage, params CodeMatch[] matches)
        {
            matcher.MatchStartForward(matches);
            if (matcher.IsInvalid) {
                Logger.LogWarning($"{failureMessage} Anchor instruction not found, it was likely consumed by another mod. Skipping this patch.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// <see cref="TryMatchStartForward"/>, but leaving the position on the last matched
        /// instruction instead of the first.
        /// </summary>
        public static bool TryMatchEndForward(this CodeMatcher matcher, string failureMessage, params CodeMatch[] matches)
        {
            matcher.MatchEndForward(matches);
            if (matcher.IsInvalid) {
                Logger.LogWarning($"{failureMessage} Anchor instruction not found, it was likely consumed by another mod. Skipping this patch.");
                return false;
            }
            return true;
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

        public static List<ZNetPeer> ServerGetPeersInArea(Vector3 pos, float radius) {
            var result = new List<ZNetPeer>();
            if (!ZNet.instance || !ZNet.instance.IsServer())
                return result;

            float radiusSqr = radius * radius;
            foreach (ZNetPeer peer in ZNet.instance.m_peers) {
                if (!peer.IsReady() || peer.m_characterID == ZDOID.None)
                    continue;
                if (Utils.DistanceSqr(peer.m_refPos, pos) <= radiusSqr)
                    result.Add(peer);
            }
            return result;
        }
    }
}
