using HarmonyLib;
using ImpactfulSkills.common;
using Jotunn.Configs;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace ImpactfulSkills.patches
{
    public static class Voyaging
    {
        public static Skills.SkillType VoyagingSkill = 0;
        public static void SetupSailingSkill()
        {
            SkillConfig voyage = new SkillConfig();
            voyage.Name = "$skill_Voyager";
            voyage.Description = "$skill_Voyager_description";
            // Assets/Custom/Icons/skill_icons/hauling.png
            voyage.Icon = ImpactfulSkills.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/skill_icons/voyager.png");
            voyage.Identifier = "midnightsfx.voyager";
            voyage.IncreaseStep = 0.15f;
            VoyagingSkill = SkillManager.Instance.AddSkill(voyage);
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
        public static class VoyagerSpeedPatch
        {
            private static void Postfix(Ship __instance, ref Vector3 __result) {
                if (ValConfig.EnableVoyager.Value != true || Player.m_localPlayer == null) { return; }

                // NOTE: XP gain is handled in VoyagerXPPatch (Ship.UpdateSail). GetSailForce only
                // runs on the boat's ZDO owner (the driver), so awarding XP here skipped passengers.
                float player_skill = Player.m_localPlayer.GetSkillFactor(VoyagingSkill);
                if (player_skill > 0f) {
                    float bonus = 1f + (player_skill * ValConfig.VoyagerSailingSpeedFactor.Value);
                    // Logger.LogDebug($"Increasing player sailspeed: {bonus}");
                    __result *= bonus;
                }
                if (ValConfig.EnableFriendsRowSpeedBonus.Value && __instance.m_players.Count > 1) {
                    float rowingbonus = 1;
                    foreach (Player friend in __instance.m_players) {
                        if (friend == Player.m_localPlayer) { continue; }
                        rowingbonus += friend.GetSkillFactor(VoyagingSkill) * ValConfig.MaxFriendsRowSpeedBonus.Value;
                    }
                    __result *= rowingbonus;
                }
            }
        }

        // Awards Voyager XP to whoever is locally aboard a moving boat. Hooks Ship.UpdateSail,
        // which Ship.CustomFixedUpdate runs on every client BEFORE its m_nview.IsOwner() return,
        // so passengers (not just the ZDO-owning driver) gain XP. Mirrors Hauling.VagonXPPatch:
        // a throttled position-delta check, which is reliable on non-owner clients (where
        // rigidbody velocity is not).
        [HarmonyPatch(typeof(Ship))]
        public static class VoyagerXPPatch {
            static Vector3 lastPosition = Vector3.zero;
            static float lastTimer = 0f;

            [HarmonyPatch("UpdateSail")]
            private static void Postfix(Ship __instance) {
                if (ValConfig.EnableVoyager.Value == false || Player.m_localPlayer == null) { return; }
                // Only the boat the LOCAL player is actually aboard (driver or passenger).
                if (!__instance.IsPlayerInBoat(Player.m_localPlayer)) { return; }

                if (lastPosition == Vector3.zero || lastTimer == 0) {
                    lastPosition = __instance.transform.position;
                    lastTimer = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup > lastTimer + ValConfig.VoyagerSkillXPCheckFrequency.Value) {
                    lastTimer = Time.realtimeSinceStartup;
                    float distance = Vector3.Distance(lastPosition, __instance.transform.position);
                    Logger.LogDebug($"Checking voyager distance traveled: {distance}");
                    // Threshold avoids idle bob/drift granting XP on an anchored boat.
                    if (distance > 2f) {
                        Logger.LogDebug($"Raising player voyager skill.");
                        Player.m_localPlayer.RaiseSkill(VoyagingSkill, (ValConfig.VoyagerSkillGainRate.Value * 1f));
                        lastPosition = __instance.transform.position;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear))]
        public static class ShipDamageReduction {
            [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_Damage))]
            private static void Prefix(WearNTear __instance, ref HitData hit) {
                if (__instance.m_materialType != WearNTear.MaterialType.Wood || ValConfig.EnableBoatDamageReduction.Value == false) { return; }
                if (Player.m_localPlayer == null || Player.m_localPlayer.GetSkillLevel(VoyagingSkill) < ValConfig.BoatDamageReductionLevel.Value) {  return; }
                Ship ship = __instance.GetComponent<Ship>();
                if (ship == null || !ship.m_players.Contains(Player.m_localPlayer)) { return; }

                float player_skill = Player.m_localPlayer.GetSkillFactor(VoyagingSkill);
                float dmg_reduction = player_skill * ValConfig.VoyagerDamageReductionAmount.Value;
                Logger.LogDebug($"Reducing Ship damage by {dmg_reduction * 100}%");
                hit.m_damage.Modify(1 - dmg_reduction);
            }
        }

        [HarmonyPatch(typeof(ImpactEffect))]
        public static class ShipDamageImpactReduction {
            [HarmonyPatch(typeof(ImpactEffect), nameof(ImpactEffect.Awake))]
            private static void Postfix(ImpactEffect __instance) {
                if (Player.m_localPlayer == null || Player.m_localPlayer.GetSkillLevel(VoyagingSkill) < ValConfig.VoyagerImpactResistanceLevel.Value) {
                    return;
                }
                __instance.m_damageToSelf = false;
            }
        }

        //[HarmonyEmitIL("./dumps")]
        //[HarmonyDebug]
        [HarmonyPatch(typeof(Ship))]
        public static class PaddlingIsFasterPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(Ship.CustomFixedUpdate))]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
            {
                var codeMatcher = new CodeMatcher(instructions);
                if (codeMatcher.TryMatchStartForward("Unable to patch paddle speed improvement.",
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), nameof(Ship.m_body))),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), nameof(Ship.m_body))),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Ldarg_1)
                )) {
                    codeMatcher.Advance(3).InsertAndAdvance(
                        Transpilers.EmitDelegate(PaddleSpeedImprovement)
                    );
                }
                return codeMatcher.Instructions();
            }

            public static Vector3 PaddleSpeedImprovement(Vector3 ship_motion) {
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null) {
                    float player_skill = Player.m_localPlayer.GetSkillLevel(VoyagingSkill);
                    if (player_skill >= ValConfig.VoyagerPaddleSpeedBonusLevel.Value) {
                        Vector3 ship_modified_motion = ship_motion * (1 + ValConfig.VoyagerPaddleSpeedBonus.Value * (player_skill / 100));
                        //Logger.LogInfo($"Improving ship paddle speed: {ship_motion} -> {ship_modified_motion}");
                        return ship_modified_motion;
                    }
                }
                // fallback to the default modification for the method
                return ship_motion;
            }
        }


        // Ship.GetWindAngleFactor gives the sail no wind once it blows within acos(0.8) (~37 degrees) of the bow, ramping back
        // to full by acos(0.75) (~41 degrees). Voyager narrows that dead zone from VoyagerReduceCuttingStart up to level 100.
        private const float VanillaFullWindHeadwind = 0.75f;
        private const float VanillaNoWindHeadwind = 0.8f;
        private static readonly float VanillaCuttingAngle = Mathf.Acos(VanillaFullWindHeadwind) * Mathf.Rad2Deg;

        // 0 at VoyagerReduceCuttingStart, 1 at level 100.
        private static float GetCuttingProgress(float skillLevel) {
            return Mathf.InverseLerp(ValConfig.VoyagerReduceCuttingStart.Value, 100f, skillLevel);
        }

        // The closest the wind can get to the bow (in degrees) while the sail still catches all of it.
        private static float GetCuttingAngle(float cuttingProgress) {
            return Mathf.Lerp(VanillaCuttingAngle, ValConfig.VoyagerCuttingMinAngle.Value, cuttingProgress);
        }

        // Ship.GetWindAngleFactor with its dead zone narrowed to GetCuttingAngle, and the penalty for every other wind angle
        // shrinking over the same levels. headwind is 1 with the wind dead ahead and -1 with it straight behind.
        private static float SkilledWindAngleFactor(float headwind, float cuttingProgress) {
            float angleFactor = Mathf.Min(1f, Mathf.Lerp(0.7f, 1f, 1f - Mathf.Abs(headwind)) + cuttingProgress);
            float cuttingAngle = GetCuttingAngle(cuttingProgress);
            if (cuttingAngle <= 0f) { return angleFactor; }

            // Stretch the wind's angle off the bow so the vanilla dead-zone ramp ends at the narrowed cutting angle.
            float windAngle = Mathf.Acos(Mathf.Clamp(headwind, -1f, 1f)) * Mathf.Rad2Deg;
            float stretchedHeadwind = Mathf.Cos(Mathf.Min(windAngle * VanillaCuttingAngle / cuttingAngle, 180f) * Mathf.Deg2Rad);
            return angleFactor * (1f - Mathf.InverseLerp(VanillaFullWindHeadwind, VanillaNoWindHeadwind, stretchedHeadwind));
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetWindAngleFactor))]
        public static class VoyagerAnglePatch
        {
            private static void Postfix(Ship __instance, ref float __result)
            {
                if (ValConfig.EnableVoyager.Value != true || Player.m_localPlayer == null) { return; }

                float cuttingProgress = GetCuttingProgress(Player.m_localPlayer.GetSkillLevel(VoyagingSkill));
                if (cuttingProgress <= 0f) { return; }

                float headwind = Vector3.Dot(EnvMan.instance.GetWindDir(), -__instance.transform.forward);
                __result = SkilledWindAngleFactor(headwind, cuttingProgress);
            }
        }

        // Shades in the part of the ship HUD's dark headwind arc that Voyager has made sailable, so the arc visibly
        // shrinks toward the bow as the skill grows.
        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateShipHud))]
        public static class VoyagerCuttingHudPatch {
            // The wind ring's sprite (ship_circle_bw) paints its dark arc out to ~44 degrees either side of the bow, a
            // little past the vanilla cutting angle, so the shading runs out to meet the lit part of the ring.
            const float RingDarkArcAngle = 44f;
            // Inner and outer edge of the ring's band as a fraction of the sprite's half-width, measured from ship_circle_bw.
            const float RingInnerRadius = 0.912f;
            const float RingOuterRadius = 0.988f;
            // The ring art is about this bright just outside the dark arc; applied to the ring's tint so the shading matches.
            const float RingLitBrightness = 0.7f;

            static Sprite ringSprite;
            static Image starboardShade;
            static Image portShade;
            static Hud failedHud;

            private static void Postfix(Hud __instance, Player player) {
                float cuttingProgress = 0f;
                if (ValConfig.EnableVoyager.Value && player != null && player.GetControlledShip() != null) {
                    cuttingProgress = GetCuttingProgress(player.GetSkillLevel(VoyagingSkill));
                }
                if (cuttingProgress <= 0f) {
                    // Unity-overloaded null check is false once the Hud is destroyed (e.g. logout).
                    if (starboardShade != null) { starboardShade.enabled = false; }
                    if (portShade != null) { portShade.enabled = false; }
                    return;
                }
                if (starboardShade == null && (failedHud == __instance || !TryCreateShades(__instance))) { return; }

                // Each radial fill starts at the bow and sweeps outward; rotating it by the cutting angle starts it at the
                // edge of the remaining dead zone instead.
                float cuttingAngle = GetCuttingAngle(cuttingProgress);
                float fill = (RingDarkArcAngle - cuttingAngle) / 360f;
                starboardShade.fillAmount = fill;
                starboardShade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -cuttingAngle);
                starboardShade.enabled = true;
                portShade.fillAmount = fill;
                portShade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, cuttingAngle);
                portShade.enabled = true;
            }

            private static bool TryCreateShades(Hud hud) {
                Transform circle = hud.m_shipWindIndicatorRoot.Find("Circle");
                Image circleImage = circle != null ? circle.GetComponent<Image>() : null;
                if (circleImage == null) {
                    Logger.LogWarning("Voyager cutting HUD: could not find the 'Circle' image under the ship wind indicator.");
                    failedHud = hud;
                    return false;
                }

                Color tint = circleImage.color;
                Color shadeColor = new Color(tint.r * RingLitBrightness, tint.g * RingLitBrightness, tint.b * RingLitBrightness, tint.a);
                starboardShade = CreateShade("ImpactfulSkills_VoyagerCuttingStarboard", circle, shadeColor, true);
                portShade = CreateShade("ImpactfulSkills_VoyagerCuttingPort", circle, shadeColor, false);
                return true;
            }

            private static Image CreateShade(string name, Transform circle, Color color, bool clockwise) {
                GameObject shade = new GameObject(name, typeof(RectTransform));
                shade.layer = circle.gameObject.layer;
                RectTransform rect = (RectTransform)shade.transform;
                rect.SetParent(circle.parent, false);
                // Just above the ring, below the wind icon.
                rect.SetSiblingIndex(circle.GetSiblingIndex() + 1);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                Image image = shade.AddComponent<Image>();
                image.sprite = GetRingSprite();
                image.color = color;
                image.raycastTarget = false;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = clockwise;
                image.enabled = false;
                return image;
            }

            // A plain white copy of the ring's band. The ring's own sprite has the dark arc painted in, and tinting an
            // image can only darken it.
            private static Sprite GetRingSprite() {
                if (ringSprite != null) { return ringSprite; }

                const int size = 256;
                float half = size / 2f;
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                        // Roughly one texel of anti-aliasing on each edge of the band.
                        float alpha = Mathf.Clamp01((distance - RingInnerRadius) * half + 0.5f) * Mathf.Clamp01((RingOuterRadius - distance) * half + 0.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                    name = "ImpactfulSkills_VoyagerRing",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return ringSprite;
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
        private class VoyagerNotSoBlindWhileSailingPatch {
            private static void Prefix(ref float radius) {
                if (ValConfig.EnableVoyager.Value == true && Player.m_localPlayer != null && Player.m_localPlayer.m_attachedToShip == true && Player.m_localPlayer.IsAttached()) {
                    radius *= (Player.m_localPlayer.GetSkillFactor(VoyagingSkill) * ValConfig.VoyagerIncreaseExplorationRadius.Value) + 1f;
                }
            }
        }
    }
}
