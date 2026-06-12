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
