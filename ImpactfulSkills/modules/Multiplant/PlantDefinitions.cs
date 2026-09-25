using HarmonyLib;
using ImpactfulSkills.common;
using System.Collections.Generic;
using UnityEngine;

namespace ImpactfulSkills.modules.Multiplant {

    /// Many of the classes in this file are roughly based on
    /// https://github.com/AdvizeGH/Advize_ValheimMods/tree/main/Advize_PlantEasily
    /// These are modified, but original design and implementation is credited to Advize
    /// This project uses the GNU 3.0 License also and all references to this implementation must do the same

    internal class Plantable {
        public float GrowRadius { get; set; }
        /// <summary>
        /// Worst-case horizontal reach of this prefab's own grow-space colliders from its pivot. For a
        /// plant this covers its whole life — the seedling and whatever it grows into — see GrownExtent.
        /// </summary>
        public float Extent { get; set; }
        public GameObject Refgo { get; set; }
    }

    internal static class PlantDefinitions {
        internal static Dictionary<string, Plantable> PlantableDefinitions = new Dictionary<string, Plantable>();

        /// <summary>
        /// The layers a plant needs clear around it, matching Valheim's own check in
        /// Plant.HaveGrowSpace. We declare this rather than reading Plant.m_spaceMask because that
        /// is a private static which stays 0 until some plant instance happens to run its grow-space
        /// check — and an OverlapSphere against layer mask 0 matches nothing, which reports every
        /// cell as free and lets overlapping plants be placed.
        /// Initialized at declaration so it can never be read before it is assigned.
        /// </summary>
        internal static readonly int plantSpaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        internal static readonly int GhostLayer = LayerMask.NameToLayer("ghost");

        /// <summary>
        /// Slack added on top of the geometric minimum. Small on purpose: it only has to cover float
        /// error in the heightmap positions and PhysX's undefined behaviour at exact tangency. Not a
        /// config entry — the two buffer settings are the tuning knobs, this is the floor they are
        /// not allowed to cut below, and a margin of 0 would reintroduce the tangency case it exists
        /// to prevent.
        /// </summary>
        internal const float SpacingSafetyMargin = 0.05f;

        /// <summary>Largest m_growRadius of any known plant (vanilla: Oak, 3.0). Bounds the neighbour scan.</summary>
        internal static float MaxGrowRadius { get; private set; }

        /// <summary>Largest lifecycle Extent of any known plant (vanilla: the big fir, grown). Bounds the neighbour scan.</summary>
        internal static float MaxExtent { get; private set; }

        internal static void BuildPlantRequirements() {
            PlantableDefinitions.Clear();
            MaxGrowRadius = 0f;
            MaxExtent = 0f;
            if (ZNetScene.instance == null || ZNetScene.instance.m_prefabs == null) {
                Logger.LogWarning("ZNetScene not ready for plant definitions");
                return;
            }

            foreach (GameObject obj in ZNetScene.instance.m_prefabs) {
                Plant plant = obj.GetComponent<Plant>();
                if (plant == null || PlantableDefinitions.ContainsKey(obj.name)) {
                    continue;
                }
                float seedlingExtent = HorizontalExtent(obj);
                float grownExtent = GrownExtent(plant);
                float extent = Mathf.Max(seedlingExtent, grownExtent);
                PlantableDefinitions.Add(obj.name, new Plantable() { GrowRadius = plant.m_growRadius, Extent = extent, Refgo = obj });
                if (plant.m_growRadius > MaxGrowRadius) { MaxGrowRadius = plant.m_growRadius; }
                if (extent > MaxExtent) { MaxExtent = extent; }

                foreach (GameObject grownPlant in plant.m_grownPrefabs) {
                    if (!PlantableDefinitions.ContainsKey(grownPlant.name)) {
                        // Grow radius is inherited from the seedling; the extent is the grown model's own.
                        PlantableDefinitions.Add(grownPlant.name, new Plantable() { Refgo = grownPlant, GrowRadius = plant.m_growRadius, Extent = HorizontalExtent(grownPlant) });
                    }
                }
                Logger.LogDebug($"Added plant cache entry: {obj.name} growRadius={plant.m_growRadius:F2} " +
                                $"extent={extent:F2} (seedling={seedlingExtent:F2} grown={grownExtent:F2}) " +
                                $"required={RequiredDistance(plant.m_growRadius, extent):F2} " +
                                $"spacing={SpacingFor(plant.m_growRadius, extent):F2}");
            }
            Logger.LogInfo($"Loaded {PlantableDefinitions.Count} plantable definitions");
        }

        // ── Spacing arithmetic ─────────────────────────────────────────────────

        /// <summary>
        /// How far a prefab's own collision reaches from its pivot, horizontally, at any yaw.
        ///
        /// This is the term the spacing formula used to be missing. Plant.HaveGrowSpace overlaps a
        /// sphere of m_growRadius against colliders, and OverlapSphere reports a collider whose
        /// SURFACE is inside the sphere — so a neighbour blocks at growRadius + ITS extent, not at
        /// growRadius. Barley's capsule is 0.4 wide against a 0.5 grow radius, so ignoring the
        /// extent underestimated its requirement by a third and the crop was destroyed at maturity.
        ///
        /// Only colliders on a layer inside plantSpaceMask count, which is what keeps Onion's two
        /// child BoxColliders (layer "item", ~1.9 x 1.8) out of the answer while keeping the root
        /// capsule that does block.
        ///
        /// Deliberately does NOT read Collider.bounds: that is a world-space AABB maintained by the
        /// physics scene and is meaningless on a prefab asset that has never been instantiated. Each
        /// shape is reconstructed from its serialized fields instead.
        ///
        /// MUST be given the ZNetScene prefab, never Player.m_placementGhost: SetupPlacementGhost
        /// puts every transform of the ghost on the "ghost" layer, so the mask test below would
        /// reject all of them and silently return 0.
        /// </summary>
        internal static float HorizontalExtent(GameObject prefab, bool includeMeshColliders = true) {
            if (prefab == null) { return 0f; }

            float extent = 0f;
            Vector3 pivot = prefab.transform.position;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true)) {
                if (collider == null || collider.isTrigger) { continue; }
                if (!includeMeshColliders && collider is MeshCollider) { continue; }
                if ((plantSpaceMask & (1 << collider.gameObject.layer)) == 0) { continue; }
                if (!ShapeExtent(collider, out Vector3 localCenter, out float halfWidth)) { continue; }

                // Plants are placed at a random yaw, so a collider offset from the pivot sweeps a
                // circle around it. Take the worst case: the offset's horizontal length plus the
                // shape's own width.
                Vector3 offset = collider.transform.TransformPoint(localCenter) - pivot;
                float reach = new Vector2(offset.x, offset.z).magnitude + halfWidth;
                if (reach > extent) { extent = reach; }
            }
            return extent;
        }

        /// <summary>
        /// Widest reach of anything this plant grows into, at the largest scale Plant.Grow can roll.
        ///
        /// The seedling's own collider is not the whole story. Plant.Grow replaces the seedling with one
        /// of m_grownPrefabs, and a grown crop is a Pickable rather than a Plant, so it blocks a
        /// neighbour's HaveGrowSpace however healthy it is. The grow time is rolled per plant, so a
        /// patch matures one crop at a time, and every neighbour still growing makes its final
        /// grow-space check against the grown crops beside it. Pickable_SeedOnion's capsule is 0.30
        /// against the seedling's 0.18: at the default 0.75 pitch that measuring only the seedling
        /// allowed, the first seed onion to mature put every neighbour at NoSpace, and
        /// m_destroyIfCantGrow destroyed each one when its own turn to grow came.
        ///
        /// Two things are deliberately left out:
        ///  - Plants that attach to a wall (vineberries). Grow puts them at the attach point, not the
        ///    seedling's pivot, and the Vine then spreads on its own, so no reach measured from the
        ///    pivot describes them. Keeping them clear of other vines is m_growRadiusVines' job.
        ///  - Mesh colliders. On the grown trees the mesh is the whole tree, crown included (Birch1's
        ///    bounds are over 6m across), while a neighbour's grow sphere sits at ground level and
        ///    only ever reaches the trunk. The bounds would spread saplings metres too far apart.
        /// </summary>
        internal static float GrownExtent(Plant plant) {
            if (plant == null || plant.m_grownPrefabs == null || plant.m_attachDistance > 0f) { return 0f; }

            // Grow overwrites the grown prefab's root scale with a roll in [m_minScale, m_maxScale].
            // Every offset and size below the root scales with it, so the reach does too.
            float maxScale = Mathf.Max(plant.m_minScale, plant.m_maxScale);
            float extent = 0f;
            foreach (GameObject grown in plant.m_grownPrefabs) {
                if (grown == null) { continue; }
                float rootScale = grown.transform.localScale.x;
                float scale = rootScale > 0f ? maxScale / rootScale : maxScale;
                extent = Mathf.Max(extent, HorizontalExtent(grown, includeMeshColliders: false) * scale);
            }
            return extent;
        }

        /// <summary>
        /// Local centre and worst-case horizontal half width of one collider, scaled the way PhysX
        /// scales it. Returns false for shapes we cannot reconstruct; leaving one out only costs
        /// density, it can never place plants closer together than the previous build did.
        /// </summary>
        private static bool ShapeExtent(Collider collider, out Vector3 localCenter, out float halfWidth) {
            Vector3 scale = collider.transform.lossyScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y), sz = Mathf.Abs(scale.z);

            if (collider is SphereCollider sphere) {
                localCenter = sphere.center;
                halfWidth = sphere.radius * Mathf.Max(sx, Mathf.Max(sy, sz));
                return true;
            }

            if (collider is CapsuleCollider capsule) {
                localCenter = capsule.center;
                // PhysX scales a capsule's radius by the larger of the two axes ACROSS it, and its
                // height by the axis ALONG it. direction: 0 = X, 1 = Y, 2 = Z.
                float acrossA = capsule.direction == 0 ? sy : sx;
                float acrossB = capsule.direction == 2 ? sy : sz;
                float radius = capsule.radius * Mathf.Max(acrossA, acrossB);
                float alongScale = capsule.direction == 0 ? sx : (capsule.direction == 1 ? sy : sz);
                // A Y-aligned capsule (every vanilla crop) is exactly as wide as its radius. One
                // lying along X or Z reaches half its length horizontally instead.
                halfWidth = capsule.direction == 1
                    ? radius
                    : Mathf.Max(capsule.height * alongScale * 0.5f, radius);
                return true;
            }

            if (collider is BoxCollider box) {
                localCenter = box.center;
                float hx = box.size.x * sx * 0.5f;
                float hz = box.size.z * sz * 0.5f;
                halfWidth = Mathf.Sqrt(hx * hx + hz * hz);   // circumradius, so it holds at any yaw
                return true;
            }

            if (collider is MeshCollider mesh && mesh.sharedMesh != null) {
                // Mesh.bounds is the ASSET's own local AABB and is valid without instantiation,
                // unlike Collider.bounds.
                Bounds b = mesh.sharedMesh.bounds;
                localCenter = b.center;
                float mx = b.extents.x * sx;
                float mz = b.extents.z * sz;
                halfWidth = Mathf.Sqrt(mx * mx + mz * mz);
                return true;
            }

            localCenter = Vector3.zero;
            halfWidth = 0f;
            return false;
        }

        /// <summary>Cached horizontal extent for a prefab name, measured lazily for anything the cache missed.</summary>
        internal static float ExtentOf(string prefabName) {
            if (PlantableDefinitions.TryGetValue(prefabName, out Plantable known)) { return known.Extent; }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null) { return 0f; }
            // A modded plant registered after our ZNetScene.Awake pass. Measure once and keep it.
            float extent = Mathf.Max(HorizontalExtent(prefab), GrownExtent(prefab.GetComponent<Plant>()));
            PlantableDefinitions[prefabName] = new Plantable() { Refgo = prefab, Extent = extent };
            if (extent > MaxExtent) { MaxExtent = extent; }
            return extent;
        }

        /// <summary>
        /// Centre-to-centre distance at which a plant of grow radius <paramref name="growRadius"/>
        /// stops seeing a neighbour whose collision reaches <paramref name="neighbourExtent"/> from
        /// its pivot. This is exactly what Plant.HaveGrowSpace's OverlapSphere measures.
        /// </summary>
        internal static float RequiredDistance(float growRadius, float neighbourExtent) {
            return growRadius + neighbourExtent;
        }

        /// <summary>
        /// Cross-species minimum. Blocking is ASYMMETRIC: A can clear B's sphere while B's sphere
        /// still swallows A's collider, and with m_destroyIfCantGrow set on every crop it is then B
        /// that dies. Both directions have to be satisfied, so take the larger.
        /// </summary>
        internal static float RequiredDistance(float growRadiusA, float extentA, float growRadiusB, float extentB) {
            return Mathf.Max(growRadiusA + extentB, growRadiusB + extentA);
        }

        /// <summary>
        /// Grid pitch for one species. The configured buffers are a preference; the second term is a
        /// floor that cannot be configured away, because below it Valheim destroys the crop at
        /// maturity and the preview has no way to show that. Evaluating the floor at runtime is also
        /// what lets the fix reach players whose saved .cfg already pins the old buffer value.
        /// </summary>
        internal static float SpacingFor(float growRadius, float extent) {
            float preferred = growRadius * ValConfig.FarmingMultiPlantDistanceBufferModifier.Value
                              + ValConfig.FarmingMultiPlantBufferSpace.Value;
            return Mathf.Max(preferred, RequiredDistance(growRadius, extent) + SpacingSafetyMargin);
        }

        /// <summary>Build all plantable information when ZNetScene is ready</summary>
        [HarmonyPriority(Priority.VeryLow)]
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        public static class Patch_ZNetScene_Awake {
            private static void Postfix() {
                BuildPlantRequirements();
            }
        }
    }
}
