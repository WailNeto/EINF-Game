using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DinoGenesis.World
{
    internal static class ProceduralVegetationGenerator
    {
        private const int ClusterCountPerAxis = 5;
        private const int CandidateTreesPerCluster = 70;
        private const float ClusterSpacing = 51f;
        private const float ClusterRadius = 30f;
        private const int MaterialCount = 5;
        private const int TreeVariantCount = 18;
        private const int TreeFormCount = 6;
        private const int BushVariantCount = 6;
        private const int PlantVariantCount = 6;

        internal sealed class GenerationResult
        {
            public readonly List<Mesh> meshes = new List<Mesh>();
            public readonly bool[] treeVariants = new bool[TreeVariantCount];
            public readonly bool[] bushVariants = new bool[BushVariantCount];
            public readonly bool[] plantVariants = new bool[PlantVariantCount];
            public int treeCount;
            public int broadleafTreeCount;
            public int umbrellaTreeCount;
            public int coniferTreeCount;
            public int understoryCount;
            public int clusterCount;
            public int vertexCount;
            public int uniqueTreeVariantCount;
            public int uniqueBroadleafVariantCount;
            public int uniqueUmbrellaVariantCount;
            public int uniqueConiferVariantCount;
            public int uniqueBushVariantCount;
            public int uniquePlantVariantCount;
        }

        private sealed class MeshBatch
        {
            public readonly List<CombineInstance> Instances = new List<CombineInstance>();
        }

        private sealed class ClusterBatch
        {
            public readonly MeshBatch[] High = CreateBatches();
            public readonly MeshBatch[] Low = CreateBatches();
            public int TreeCount;

            private static MeshBatch[] CreateBatches()
            {
                var batches = new MeshBatch[MaterialCount];
                for (var i = 0; i < batches.Length; i++)
                {
                    batches[i] = new MeshBatch();
                }

                return batches;
            }
        }

        public static GenerationResult Generate(
            Transform parent,
            int seed,
            Func<float, float, float> sampleHeight,
            Material[] materials)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (sampleHeight == null)
            {
                throw new ArgumentNullException(nameof(sampleHeight));
            }

            if (materials == null || materials.Length != MaterialCount ||
                Array.Exists(materials, material => material == null))
            {
                throw new ArgumentException("Vegetation requires five valid shared materials.", nameof(materials));
            }

            var result = new GenerationResult();
            Mesh broadleafTrunk = null;
            Mesh branchMesh = null;
            Mesh canopyMesh = null;
            Mesh coniferMesh = null;
            var fernBlades = new Mesh[PlantVariantCount];
            var bushes = new Mesh[BushVariantCount];
            var leafClusters = new Mesh[TreeVariantCount];
            var random = new System.Random(unchecked(seed * 397 + 17));
            var noiseOffset = new Vector2(random.Next(0, 100000), random.Next(0, 100000));

            try
            {
                broadleafTrunk = BuildFrustum(8, 0.17f, 0.105f);
                branchMesh = BuildFrustum(6, 0.095f, 0.035f);
                canopyMesh = BuildIcosphere();
                coniferMesh = BuildCone(8);
                for (var variant = 0; variant < PlantVariantCount; variant++)
                {
                    fernBlades[variant] = BuildFernBlade(variant);
                }

                for (var variant = 0; variant < TreeFormCount * 2; variant++)
                {
                    leafClusters[variant] = BuildLeafCluster(variant % TreeFormCount);
                }

                for (var variant = 0; variant < TreeFormCount; variant++)
                {
                    leafClusters[TreeFormCount * 2 + variant] = BuildNeedleCluster(variant);
                }

                for (var variant = 0; variant < BushVariantCount; variant++)
                {
                    bushes[variant] = BuildBushVariant(variant, canopyMesh, branchMesh, leafClusters[variant]);
                }

                var clusterIndex = 0;
                for (var zIndex = -2; zIndex <= 2; zIndex++)
                {
                    for (var xIndex = -2; xIndex <= 2; xIndex++)
                    {
                        if (xIndex == 0 && zIndex == 0)
                        {
                            continue;
                        }

                        var centerX = xIndex * ClusterSpacing + (float)(random.NextDouble() - 0.5) * 9f;
                        var centerZ = zIndex * ClusterSpacing + (float)(random.NextDouble() - 0.5) * 9f;
                        var origin = new Vector3(centerX, 0f, centerZ);
                        var cluster = new ClusterBatch();
                        var acceptedTrees = new List<Vector2>(CandidateTreesPerCluster);

                        for (var i = 0; i < CandidateTreesPerCluster; i++)
                        {
                            var x = centerX + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius;
                            var z = centerZ + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius;
                            if (x * x + z * z < 47f * 47f)
                            {
                                continue;
                            }

                            var y = sampleHeight(x, z);
                            if (y < -0.2f)
                            {
                                continue;
                            }

                            var patchDensity = Mathf.PerlinNoise(
                                (x + noiseOffset.x) * 0.018f,
                                (z + noiseOffset.y) * 0.018f);
                            if (random.NextDouble() > 0.38 + patchDensity * 0.56 ||
                                IsCrowded(x, z, acceptedTrees, 4.8f))
                            {
                                continue;
                            }

                            acceptedTrees.Add(new Vector2(x, z));
                            var variant = random.Next(TreeVariantCount);
                            result.treeVariants[variant] = true;
                            AddTree(
                                cluster,
                                origin,
                                new Vector3(x, y, z),
                                variant,
                                0.8f + (float)random.NextDouble() * 0.72f,
                                random,
                                broadleafTrunk,
                                branchMesh,
                                canopyMesh,
                                coniferMesh,
                                leafClusters[variant]);
                            cluster.TreeCount++;
                            result.treeCount++;
                            if (variant < TreeFormCount)
                            {
                                result.broadleafTreeCount++;
                            }
                            else if (variant < TreeFormCount * 2)
                            {
                                result.umbrellaTreeCount++;
                            }
                            else
                            {
                                result.coniferTreeCount++;
                            }

                            var shrubCount = random.Next(2, 5);
                            for (var shrub = 0; shrub < shrubCount; shrub++)
                            {
                                var shrubX = x + ((float)random.NextDouble() * 2f - 1f) * 3.1f;
                                var shrubZ = z + ((float)random.NextDouble() * 2f - 1f) * 3.1f;
                                var shrubY = sampleHeight(shrubX, shrubZ);
                                if (shrubX * shrubX + shrubZ * shrubZ < 44f * 44f || shrubY < -0.2f)
                                {
                                    continue;
                                }

                                var bushVariant = random.Next(BushVariantCount);
                                result.bushVariants[bushVariant] = true;
                                AddShrub(
                                    cluster,
                                    origin,
                                    new Vector3(shrubX, shrubY, shrubZ),
                                    bushVariant,
                                    random,
                                    bushes[bushVariant]);
                                result.understoryCount++;
                            }

                            if (random.NextDouble() < 0.82)
                            {
                                var plantVariant = random.Next(PlantVariantCount);
                                result.plantVariants[plantVariant] = true;
                                AddFern(
                                    cluster,
                                    origin,
                                    new Vector3(
                                        x + ((float)random.NextDouble() * 2f - 1f) * 3.5f,
                                        y,
                                        z + ((float)random.NextDouble() * 2f - 1f) * 3.5f),
                                    0.8f + (float)random.NextDouble() * 0.75f,
                                    random,
                                    fernBlades[plantVariant]);
                                result.understoryCount++;
                            }
                        }

                        AddFernsAroundCluster(
                            cluster,
                            origin,
                            centerX,
                            centerZ,
                            random,
                            sampleHeight,
                            fernBlades,
                            random.Next(PlantVariantCount),
                            ref result.understoryCount,
                            result.plantVariants);
                        AddFallenLog(cluster, origin, centerX, centerZ, random, sampleHeight, broadleafTrunk);

                        if (cluster.TreeCount > 0)
                        {
                            BuildClusterObject(parent, cluster, clusterIndex++, origin, materials, result);
                        }
                    }
                }
            }
            catch
            {
                DestroyMeshes(result.meshes);
                throw;
            }
            finally
            {
                DestroyMesh(broadleafTrunk);
                DestroyMesh(branchMesh);
                DestroyMesh(canopyMesh);
                DestroyMesh(coniferMesh);
                DestroyMeshes(fernBlades);
                DestroyMeshes(bushes);
                DestroyMeshes(leafClusters);
            }

            result.uniqueTreeVariantCount = CountVariants(result.treeVariants);
            result.uniqueBroadleafVariantCount = CountVariants(result.treeVariants, 0, TreeFormCount);
            result.uniqueUmbrellaVariantCount = CountVariants(result.treeVariants, TreeFormCount, TreeFormCount);
            result.uniqueConiferVariantCount = CountVariants(result.treeVariants, TreeFormCount * 2, TreeFormCount);
            result.uniqueBushVariantCount = CountVariants(result.bushVariants);
            result.uniquePlantVariantCount = CountVariants(result.plantVariants);
            return result;
        }

        private static void AddTree(
            ClusterBatch cluster,
            Vector3 origin,
            Vector3 position,
            int variant,
            float scale,
            System.Random random,
            Mesh trunk,
            Mesh branch,
            Mesh canopy,
            Mesh conifer,
            Mesh leafCluster)
        {
            var yaw = (float)random.NextDouble() * 360f;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var height = 9.4f * scale;
            var trunkWidth = 0.19f + scale * 0.075f;
            var trunkScale = trunkWidth / 0.17f;
            AddPart(cluster.High, 0, trunk, origin, position, rotation, new Vector3(trunkScale, height, trunkScale));
            AddPart(cluster.Low, 0, trunk, origin, position, rotation, new Vector3(trunkScale, height, trunkScale));

            if (variant < TreeFormCount)
            {
                var form = variant;
                AddButtressRoots(cluster.High, origin, position, rotation, scale, height, branch);
                AddBroadleafCrown(cluster.High, origin, position, rotation, scale, height, branch, canopy, leafCluster, form, true);
                AddBroadleafCrown(cluster.Low, origin, position, rotation, scale, height, branch, canopy, leafCluster, form, false);
                return;
            }

            if (variant < TreeFormCount * 2)
            {
                var form = variant - TreeFormCount;
                AddButtressRoots(cluster.High, origin, position, rotation, scale, height, branch);
                AddUmbrellaCanopy(cluster.High, origin, position, rotation, scale, height, branch, canopy, leafCluster, form, true);
                AddUmbrellaCanopy(cluster.Low, origin, position, rotation, scale, height, branch, canopy, leafCluster, form, false);
                return;
            }

            var coniferForm = variant - TreeFormCount * 2;
            var foliageMaterialIndex = 3;
            var coneLayers = 4 + coniferForm % 3 * 2;
            for (var layer = 0; layer < coneLayers; layer++)
            {
                var fraction = coneLayers == 1 ? 0f : layer / (float)(coneLayers - 1);
                var width = scale * Mathf.Lerp(1.25f + coniferForm * 0.12f, 0.22f + coniferForm * 0.025f, fraction);
                var layerHeight = height * (0.28f + fraction * 0.62f);
                var coneHeight = height * (0.61f - fraction * 0.18f);
                AddPart(
                    cluster.High,
                    foliageMaterialIndex,
                    conifer,
                    origin,
                    position + Vector3.up * layerHeight,
                    rotation,
                    new Vector3(width, coneHeight, width));
            }

            AddConiferBranches(
                cluster.High,
                origin,
                position,
                rotation,
                scale,
                height,
                branch,
                leafCluster,
                coniferForm);
            AddPart(
                cluster.Low,
                foliageMaterialIndex,
                conifer,
                origin,
                position + Vector3.up * height * 0.58f,
                rotation,
                new Vector3(scale * (1.08f + coniferForm * 0.09f), height * 0.76f, scale * (1.08f + coniferForm * 0.09f)));
        }

        private static void AddButtressRoots(
            MeshBatch[] batches,
            Vector3 origin,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float height,
            Mesh branch)
        {
            var rootLength = 1.4f + scale * 1.1f;
            var rootWidth = 0.34f + scale * 0.08f;
            var rootWidthScale = rootWidth / 0.095f;
            for (var root = 0; root < 4; root++)
            {
                var angle = root * 90f + 45f;
                var rootRotation = rotation * Quaternion.Euler(0f, angle, -63f);
                AddPart(
                    batches,
                    0,
                    branch,
                    origin,
                    position + rotation * new Vector3(0f, height * 0.06f, 0f),
                    rootRotation,
                    new Vector3(rootWidthScale, rootLength, rootWidthScale));
            }
        }

        private static void AddBroadleafCrown(
            MeshBatch[] batches,
            Vector3 origin,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float height,
            Mesh branch,
            Mesh canopy,
            Mesh leafCluster,
            int variant,
            bool detailed)
        {
            var material = scale > 1.15f ? 2 : 1;
            var crownHeight = new[] { 0.68f, 0.79f, 0.84f, 0.73f, 0.76f, 0.9f }[variant];
            var crown = position + Vector3.up * height * crownHeight;
            var canopyWidth = scale * (variant == 0 ? 1.65f : variant == 1 ? 2.15f : variant == 2 ? 1.38f :
                variant == 3 ? 1.82f : variant == 4 ? 1.28f : 1.95f);
            var canopyDepth = scale * (variant == 2 || variant == 5 ? 2.2f : variant == 4 ? 1.38f : 1.75f);
            var canopyThickness = scale * (variant == 0 ? 1.35f : variant == 2 || variant == 4 ? 0.85f : 1.05f);
            AddPart(
                batches,
                material,
                canopy,
                origin,
                crown,
                rotation,
                new Vector3(canopyWidth * (detailed ? 0.38f : 1f),
                    canopyThickness * (detailed ? 0.55f : 1f),
                    canopyDepth * (detailed ? 0.38f : 1f)));
            if (!detailed)
            {
                return;
            }

            var lobeCount = 7 + variant % 5;
            for (var lobe = 0; lobe < lobeCount; lobe++)
            {
                var angle = lobe * (360f / lobeCount) + variant * 17f;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var offset = new Vector3(
                    direction.x * scale * (variant == 3 ? 1.15f : variant == 4 ? 1.3f : 1.55f),
                    scale * Mathf.Sin((lobe + 1) * (1.7f + variant * 0.13f)) * 0.22f,
                    direction.z * scale * (variant == 2 || variant == 5 ? 1.75f : 1.4f));
                var lobeScale = new Vector3(scale * ((variant == 0 || variant == 4) ? 0.72f : 0.82f),
                    scale * (variant == 2 || variant == 5 ? 0.58f : 0.68f), scale * 0.76f);
                AddPart(
                    batches,
                    (lobe % 4 == 0 || scale > 1.38f) ? 2 : material,
                    canopy,
                    origin,
                    crown + rotation * offset,
                    rotation * Quaternion.Euler(0f, angle * 0.38f, 0f),
                    lobeScale);
                AddPart(
                    batches,
                    lobe % 3 == 0 ? 2 : material,
                    leafCluster,
                    origin,
                    crown + rotation * (offset + direction * scale * 0.72f),
                    rotation * Quaternion.Euler(0f, angle, 0f),
                    new Vector3(scale * 1.55f, scale * 1.48f, scale * 1.55f));

                if (lobe % 2 == 0 || variant == 2)
                {
                    var branchLength = scale * (2.2f + variant * 0.1f);
                    var branchRotation = rotation * Quaternion.Euler(0f, angle, -58f);
                    var branchBase = position + rotation * new Vector3(0f, height * (crownHeight - 0.14f), 0f);
                    AddPart(
                        batches,
                        0,
                        branch,
                        origin,
                        branchBase,
                        branchRotation,
                        new Vector3(scale * 0.82f, branchLength, scale * 0.82f));

                    var foliagePosition = branchBase + branchRotation * Vector3.up * branchLength * 0.78f;
                    AddPart(
                        batches,
                        (lobe % 3 == 0) ? 2 : material,
                        leafCluster,
                        origin,
                        foliagePosition,
                        branchRotation * Quaternion.Euler(5f, 0f, 18f),
                        new Vector3(scale * 1.55f, scale * 1.45f, scale * 1.5f));
                }
            }

            AddPart(
                batches,
                material,
                canopy,
                origin,
                crown + Vector3.up * scale * 0.72f,
                rotation * Quaternion.Euler(0f, 21f, 0f),
                new Vector3(scale * 1.42f, scale * 1.15f, scale * 1.35f));
        }

        private static void AddUmbrellaCanopy(
            MeshBatch[] batches,
            Vector3 origin,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float height,
            Mesh branch,
            Mesh canopy,
            Mesh leafCluster,
            int variant,
            bool detailed)
        {
            var crownHeight = height * 0.74f;
            var canopyRadius = scale * (3.0f + variant * 0.16f);
            var crown = position + Vector3.up * crownHeight;
            if (!detailed)
            {
                AddPart(
                    batches,
                    1,
                    canopy,
                    origin,
                    crown,
                    rotation,
                    new Vector3(canopyRadius * 1.1f, scale * 0.78f, canopyRadius));
                return;
            }

            var branches = 7 + variant % 5;
            for (var branchIndex = 0; branchIndex < branches; branchIndex++)
            {
                var angle = branchIndex * (360f / branches);
                var branchRotation = rotation * Quaternion.Euler(0f, angle, -71f);
                var branchLength = canopyRadius * (0.68f + variant * 0.018f + 0.12f * Mathf.Sin(branchIndex * 1.9f));
                var branchBase = position + rotation * new Vector3(0f, crownHeight * 0.92f, 0f);
                AddPart(
                    batches,
                    0,
                    branch,
                    origin,
                    branchBase,
                    branchRotation,
                    new Vector3(scale * 0.9f, branchLength, scale * 0.9f));

                var endpoint = branchBase + branchRotation * Vector3.up * branchLength * 0.82f;
                var lobeScale = new Vector3(scale * 0.95f, scale * 0.46f, scale * 0.9f);
                AddPart(
                    batches,
                    branchIndex % 3 == 0 ? 2 : 1,
                    canopy,
                    origin,
                    endpoint,
                    branchRotation,
                    lobeScale);
                AddPart(
                    batches,
                    branchIndex % 3 == 1 ? 2 : 1,
                    leafCluster,
                    origin,
                    endpoint + Vector3.up * scale * 0.1f,
                    branchRotation * Quaternion.Euler(0f, 25f, 14f),
                    new Vector3(scale * 1.65f, scale * 1.45f, scale * 1.65f));
            }

            AddPart(
                batches,
                1,
                canopy,
                origin,
                crown,
                rotation,
                new Vector3(canopyRadius * 0.85f, scale * 0.75f, canopyRadius * 0.82f));
        }

        private static void AddConiferBranches(
            MeshBatch[] batches,
            Vector3 origin,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float height,
            Mesh branch,
            Mesh needles,
            int variant)
        {
            var tiers = variant % 3 == 0 ? 4 : variant % 3 == 1 ? 6 : 8;
            var branchesPerTier = 5 + variant % 3;
            for (var tier = 0; tier < tiers; tier++)
            {
                var tierHeight = height * (0.2f + tier * (0.7f / tiers));
                var length = scale * Mathf.Lerp(2.0f + variant * 0.09f, 0.45f + variant * 0.04f, tier / (float)(tiers - 1));
                for (var branchIndex = 0; branchIndex < branchesPerTier; branchIndex++)
                {
                    var angle = branchIndex * (360f / branchesPerTier) + tier * 29f;
                    var branchRotation = rotation * Quaternion.Euler(0f, angle, -58f - variant * 2f);
                    AddPart(
                        batches,
                        0,
                        branch,
                        origin,
                        position + rotation * new Vector3(0f, tierHeight, 0f),
                        branchRotation,
                        new Vector3(scale * 0.55f, length, scale * 0.55f));
                    if (branchIndex % 2 == 0)
                    {
                        var tip = position + rotation * new Vector3(0f, tierHeight, 0f) +
                                  branchRotation * Vector3.up * length * 0.82f;
                        AddPart(
                            batches,
                            3,
                            needles,
                            origin,
                            tip,
                            branchRotation * Quaternion.Euler(0f, branchIndex * 37f, 18f),
                            new Vector3(scale * 1.55f, scale * 1.55f, scale * 1.55f));
                    }
                }
            }
        }

        private static void AddShrub(
            ClusterBatch cluster,
            Vector3 origin,
            Vector3 position,
            int variant,
            System.Random random,
            Mesh shrub)
        {
            var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            var scale = 0.72f + (float)random.NextDouble() * 0.78f;
            var materialIndex = variant % 3 == 0 ? 2 : 1;
            AddPart(
                cluster.High,
                materialIndex,
                shrub,
                origin,
                position + Vector3.up * scale * (variant % 2 == 0 ? 0.56f : 0.68f),
                rotation,
                new Vector3(scale * (variant % 2 == 0 ? 1.22f : 0.92f), scale * 0.78f, scale * 1.08f));
            AddPart(
                cluster.Low,
                materialIndex,
                shrub,
                origin,
                position + Vector3.up * scale * 0.62f,
                rotation,
                new Vector3(scale * 1.45f, scale * 0.76f, scale * 1.18f));
        }

        private static void AddFern(
            ClusterBatch cluster,
            Vector3 origin,
            Vector3 position,
            float scale,
            System.Random random,
            Mesh blade)
        {
            var yaw = (float)random.NextDouble() * 360f;
            var baseRotation = Quaternion.Euler(0f, yaw, 0f);
            var leaves = 5 + random.Next(0, 3);
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                var angle = leaf * (360f / leaves) + (float)random.NextDouble() * 35f;
                var lean = 14f + (float)random.NextDouble() * (scale > 1.25f ? 38f : 28f);
                var length = scale * (0.92f + (float)random.NextDouble() * 0.72f);
                var rotation = baseRotation * Quaternion.Euler(lean, angle, 0f);
                AddPart(
                    cluster.High,
                    4,
                    blade,
                    origin,
                    position + Vector3.up * 0.08f,
                    rotation,
                    new Vector3(scale * 0.52f, length, scale * 0.46f));
                if (leaf % 2 == 0)
                {
                    AddPart(
                        cluster.Low,
                        4,
                        blade,
                        origin,
                        position + Vector3.up * 0.08f,
                        rotation,
                        new Vector3(scale * 0.42f, length * 0.76f, scale * 0.38f));
                }
            }
        }

        private static void AddFernsAroundCluster(
            ClusterBatch cluster,
            Vector3 origin,
            float centerX,
            float centerZ,
            System.Random random,
            Func<float, float, float> sampleHeight,
            Mesh[] blades,
            int firstVariant,
            ref int understoryCount,
            bool[] plantVariants)
        {
            for (var i = 0; i < 42; i++)
            {
                var x = centerX + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius;
                var z = centerZ + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius;
                if (x * x + z * z < 47f * 47f)
                {
                    continue;
                }

                var height = sampleHeight(x, z);
                if (height < -0.2f)
                {
                    continue;
                }

                var variant = (firstVariant + i) % blades.Length;
                plantVariants[variant] = true;
                AddFern(
                    cluster,
                    origin,
                    new Vector3(x, height, z),
                    0.72f + (float)random.NextDouble() * 0.82f,
                    random,
                    blades[variant]);
                understoryCount++;
            }
        }

        private static bool IsCrowded(float x, float z, List<Vector2> acceptedTrees, float minimumSpacing)
        {
            var minimumSpacingSquared = minimumSpacing * minimumSpacing;
            var position = new Vector2(x, z);
            for (var i = 0; i < acceptedTrees.Count; i++)
            {
                if ((acceptedTrees[i] - position).sqrMagnitude < minimumSpacingSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFallenLog(
            ClusterBatch cluster,
            Vector3 origin,
            float centerX,
            float centerZ,
            System.Random random,
            Func<float, float, float> sampleHeight,
            Mesh trunk)
        {
            var x = centerX + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius * 0.55f;
            var z = centerZ + ((float)random.NextDouble() * 2f - 1f) * ClusterRadius * 0.55f;
            if (x * x + z * z < 47f * 47f || sampleHeight(x, z) < -0.2f)
            {
                return;
            }

            var position = new Vector3(x, sampleHeight(x, z) + 0.22f, z);
            var yaw = (float)random.NextDouble() * 180f;
            var rotation = Quaternion.Euler(
                (float)random.NextDouble() * 8f,
                yaw,
                86f + (float)random.NextDouble() * 8f);
            var length = 2.2f + (float)random.NextDouble() * 2.6f;
            AddPart(cluster.High, 0, trunk, origin, position, rotation, new Vector3(0.7f, length, 0.7f));
            AddPart(cluster.Low, 0, trunk, origin, position, rotation, new Vector3(0.7f, length, 0.7f));
        }

        private static void AddPart(
            MeshBatch[] batches,
            int materialIndex,
            Mesh mesh,
            Vector3 origin,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            batches[materialIndex].Instances.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(position - origin, rotation, scale)
            });
        }

        private static void BuildClusterObject(
            Transform parent,
            ClusterBatch cluster,
            int index,
            Vector3 origin,
            Material[] materials,
            GenerationResult result)
        {
            var clusterObject = new GameObject("Forest Cluster " + (index + 1).ToString("D2"));
            clusterObject.transform.SetParent(parent, false);
            clusterObject.transform.position = origin;

            var highObject = new GameObject("Near Detail");
            highObject.transform.SetParent(clusterObject.transform, false);
            var highMesh = BuildCombinedMesh(cluster.High, result.meshes, out var highMaterials, materials);
            AddMeshRenderer(highObject, highMesh, highMaterials);

            var lowObject = new GameObject("Distant Detail");
            lowObject.transform.SetParent(clusterObject.transform, false);
            var lowMesh = BuildCombinedMesh(cluster.Low, result.meshes, out var lowMaterials, materials);
            AddMeshRenderer(lowObject, lowMesh, lowMaterials);

            var lodGroup = clusterObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[]
            {
                new LOD(0.12f, new[] { highObject.GetComponent<Renderer>() }),
                new LOD(0.006f, new[] { lowObject.GetComponent<Renderer>() })
            });
            lodGroup.RecalculateBounds();

            result.clusterCount++;
            result.vertexCount += highMesh.vertexCount + lowMesh.vertexCount;
        }

        private static Mesh BuildCombinedMesh(
            MeshBatch[] batches,
            List<Mesh> ownedMeshes,
            out Material[] usedMaterials,
            Material[] materials)
        {
            var submeshMeshes = new List<Mesh>();
            var materialList = new List<Material>();
            try
            {
                for (var i = 0; i < batches.Length; i++)
                {
                    if (batches[i].Instances.Count == 0)
                    {
                        continue;
                    }

                    var instanceVertexCount = CalculateVertexCount(batches[i].Instances);
                    var submesh = new Mesh
                    {
                        name = "Vegetation Material Batch",
                        indexFormat = instanceVertexCount > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                    };
                    submesh.CombineMeshes(batches[i].Instances.ToArray(), true, true, false);
                    submesh.RecalculateBounds();
                    submeshMeshes.Add(submesh);
                    materialList.Add(materials[i]);
                }

                if (submeshMeshes.Count == 0)
                {
                    throw new InvalidOperationException("A forest cluster did not generate any renderable vegetation.");
                }

                var finalMesh = new Mesh
                {
                    name = "Combined Forest Cluster",
                    indexFormat = CalculateVertexCount(submeshMeshes) > 65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
                };
                var combine = new CombineInstance[submeshMeshes.Count];
                for (var i = 0; i < combine.Length; i++)
                {
                    combine[i] = new CombineInstance { mesh = submeshMeshes[i], transform = Matrix4x4.identity };
                }

                finalMesh.CombineMeshes(combine, false, true, false);
                finalMesh.RecalculateBounds();
                ownedMeshes.Add(finalMesh);
                usedMaterials = materialList.ToArray();
                return finalMesh;
            }
            finally
            {
                for (var i = 0; i < submeshMeshes.Count; i++)
                {
                    DestroyMesh(submeshMeshes[i]);
                }
            }
        }

        private static int CalculateVertexCount(List<Mesh> meshes)
        {
            var count = 0;
            for (var i = 0; i < meshes.Count; i++)
            {
                count = checked(count + meshes[i].vertexCount);
            }

            return count;
        }

        private static int CalculateVertexCount(List<CombineInstance> instances)
        {
            var count = 0;
            for (var i = 0; i < instances.Count; i++)
            {
                count = checked(count + instances[i].mesh.vertexCount);
            }

            return count;
        }

        private static void AddMeshRenderer(GameObject target, Mesh mesh, Material[] materials)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Mesh BuildFrustum(int sides, float bottomRadius, float topRadius)
        {
            var vertices = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i <= sides; i++)
            {
                var angle = i * Mathf.PI * 2f / sides;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                vertices[i] = new Vector3(cosine * bottomRadius, 0f, sine * bottomRadius);
                vertices[sides + 1 + i] = new Vector3(cosine * topRadius, 1f, sine * topRadius);
                uv[i] = new Vector2(i / (float)sides, 0f);
                uv[sides + 1 + i] = new Vector2(i / (float)sides, 1f);
            }

            var triangles = new int[sides * 6];
            for (var i = 0; i < sides; i++)
            {
                var bottom = i;
                var nextBottom = i + 1;
                var top = sides + 1 + i;
                var nextTop = top + 1;
                var offset = i * 6;
                triangles[offset] = bottom;
                triangles[offset + 1] = top;
                triangles[offset + 2] = nextBottom;
                triangles[offset + 3] = nextBottom;
                triangles[offset + 4] = top;
                triangles[offset + 5] = nextTop;
            }

            var mesh = new Mesh { name = "Procedural Bark Frustum", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCone(int sides)
        {
            var vertices = new Vector3[sides * 4];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 6];
            for (var i = 0; i < sides; i++)
            {
                var angle = i * Mathf.PI * 2f / sides;
                var nextAngle = (i + 1) * Mathf.PI * 2f / sides;
                var vertexOffset = i * 4;
                vertices[vertexOffset] = Vector3.zero;
                vertices[vertexOffset + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[vertexOffset + 2] = Vector3.up;
                vertices[vertexOffset + 3] = new Vector3(Mathf.Cos(nextAngle), 0f, Mathf.Sin(nextAngle));
                uv[vertexOffset] = new Vector2((i + 0.5f) / sides, 0f);
                uv[vertexOffset + 1] = new Vector2(i / (float)sides, 0f);
                uv[vertexOffset + 2] = new Vector2((i + 0.5f) / sides, 1f);
                uv[vertexOffset + 3] = new Vector2((i + 1f) / sides, 0f);
                var triangleOffset = i * 6;
                triangles[triangleOffset] = vertexOffset + 1;
                triangles[triangleOffset + 1] = vertexOffset + 2;
                triangles[triangleOffset + 2] = vertexOffset + 3;
                triangles[triangleOffset + 3] = vertexOffset;
                triangles[triangleOffset + 4] = vertexOffset + 3;
                triangles[triangleOffset + 5] = vertexOffset + 1;
            }

            var mesh = new Mesh
            {
                name = "Procedural Low-Poly Conifer Tier",
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildNeedleCluster(int variant)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var needleCount = 15 + variant * 2;
            var length = 0.34f + variant * 0.025f;
            for (var needle = 0; needle < needleCount; needle++)
            {
                var angle = needle * (Mathf.PI * 2f / needleCount);
                var direction = new Vector3(
                    Mathf.Cos(angle),
                    0.3f + (needle % 4) * 0.14f,
                    Mathf.Sin(angle)).normalized;
                var side = Vector3.Cross(direction, Vector3.up).normalized * (0.016f + variant * 0.001f);
                if (side.sqrMagnitude < 0.0001f)
                {
                    side = Vector3.right * 0.02f;
                }

                var center = direction * (0.025f + (needle % 3) * 0.018f);
                var tip = center + direction * length;
                var baseIndex = vertices.Count;
                vertices.Add(center - side);
                vertices.Add(center + side);
                vertices.Add(tip);
                vertices.Add(center - side);
                vertices.Add(center + side);
                vertices.Add(tip);
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.zero, Vector2.right, Vector2.up });
                triangles.AddRange(new[]
                {
                    baseIndex, baseIndex + 1, baseIndex + 2,
                    baseIndex + 3, baseIndex + 5, baseIndex + 4
                });
            }

            var mesh = new Mesh
            {
                name = "Procedural Needle Spray Variant " + (variant + 1),
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildFernBlade(int variant)
        {
            var width = new[] { 0.26f, 0.38f, 0.2f, 0.46f, 0.31f, 0.16f }[variant];
            var tipHeight = new[] { 1f, 0.88f, 1.08f, 0.76f, 1.16f, 1.24f }[variant];
            var serration = variant % 2 == 0 ? 1f : 0.78f;
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-width * 0.62f, 0.25f, 0f),
                new Vector3(width * 0.62f, 0.25f, 0f),
                new Vector3(-width, 0.53f, 0f),
                new Vector3(width, 0.53f, 0f),
                new Vector3(-width * 0.76f, 0.77f, 0f),
                new Vector3(width * 0.76f, 0.77f, 0f),
                new Vector3(0f, tipHeight, 0.035f * serration)
            };
            var triangles = new[]
            {
                0, 1, 2,
                1, 3, 2, 2, 3, 4,
                3, 5, 4, 4, 5, 6,
                5, 7, 6
            };
            var doubleSidedVertices = new Vector3[triangles.Length * 2];
            var doubleSidedTriangles = new int[doubleSidedVertices.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var reverseOffset = triangles.Length + i;
                for (var vertex = 0; vertex < 3; vertex++)
                {
                    var sourceIndex = triangles[i + vertex];
                    var frontIndex = i + vertex;
                    var backIndex = reverseOffset + vertex;
                    doubleSidedVertices[frontIndex] = vertices[sourceIndex];
                    doubleSidedVertices[backIndex] = vertices[sourceIndex];
                    doubleSidedTriangles[frontIndex] = frontIndex;
                    doubleSidedTriangles[backIndex] = reverseOffset + (vertex == 0 ? 0 : 3 - vertex);
                }
            }

            var mesh = new Mesh
            {
                name = "Procedural Fern Blade Variant " + (variant + 1),
                vertices = doubleSidedVertices,
                uv = BuildFernBladeUv(doubleSidedVertices),
                triangles = doubleSidedTriangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] BuildFernBladeUv(Vector3[] vertices)
        {
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                uv[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].y);
            }

            return uv;
        }

        private static Mesh BuildLeafCluster(int variant)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var leafCount = 17 + variant % 6;
            var length = 0.2f + (variant % 3) * 0.035f;
            var width = 0.055f + (variant % 2) * 0.012f;
            for (var leaf = 0; leaf < leafCount; leaf++)
            {
                var sprig = leaf % 4;
                var sprigAngle = sprig * (Mathf.PI * 0.5f) + variant * 0.31f;
                var along = (leaf / 4) / Mathf.Max(1f, Mathf.Ceil(leafCount / 4f));
                var direction = new Vector3(
                    Mathf.Cos(sprigAngle) * (0.62f + along * 0.22f),
                    0.45f + along * 0.45f,
                    Mathf.Sin(sprigAngle) * (0.62f + along * 0.22f)).normalized;
                var center = direction * (0.08f + along * 0.14f);
                direction = Quaternion.Euler(
                    (float)Math.Sin(leaf * 1.7f + variant) * 17f,
                    (float)Math.Cos(leaf * 2.1f + variant) * 12f,
                    0f) * direction;
                var side = Vector3.Cross(direction, Vector3.up).normalized * width;
                if (side.sqrMagnitude < 0.01f)
                {
                    side = Vector3.right * width;
                }
                var baseIndex = vertices.Count;
                var points = new[]
                {
                    center,
                    center + direction * length * 0.36f - side * 0.64f,
                    center + direction * length * 0.36f + side * 0.64f,
                    center + direction * length * 0.72f - side * 0.45f,
                    center + direction * length * 0.72f + side * 0.45f,
                    center + direction * length
                };
                vertices.AddRange(points);
                vertices.AddRange(points);
                var uvCoordinates = new[]
                {
                    new Vector2(0.5f, 0f),
                    new Vector2(0.08f, 0.34f),
                    new Vector2(0.92f, 0.34f),
                    new Vector2(0.12f, 0.7f),
                    new Vector2(0.88f, 0.7f),
                    new Vector2(0.5f, 1f)
                };
                uv.AddRange(uvCoordinates);
                uv.AddRange(uvCoordinates);
                var front = new[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 };
                for (var index = 0; index < front.Length; index += 3)
                {
                    triangles.Add(baseIndex + front[index]);
                    triangles.Add(baseIndex + front[index + 1]);
                    triangles.Add(baseIndex + front[index + 2]);
                    triangles.Add(baseIndex + 6 + front[index]);
                    triangles.Add(baseIndex + 6 + front[index + 2]);
                    triangles.Add(baseIndex + 6 + front[index + 1]);
                }
            }

            var mesh = new Mesh
            {
                name = "Six-Leaf Cluster Variant " + (variant + 1),
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildBushVariant(int variant, Mesh canopy, Mesh branch, Mesh leafCluster)
        {
            var instances = new List<CombineInstance>();
            var canopyCount = 4 + variant;
            for (var leaf = 0; leaf < canopyCount; leaf++)
            {
                var angle = leaf * 2.39996f + variant * 0.37f;
                var ring = 0.48f + 0.16f * (leaf % 3);
                var height = 0.38f + (leaf % (2 + variant % 3)) * 0.19f;
                var scale = variant == 4 ? 0.43f : 0.34f + (leaf % 2) * 0.12f;
                var leafPosition = new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
                var leafRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                var leafScale = new Vector3(scale * (0.8f + variant % 2 * 0.4f), scale, scale);
                instances.Add(new CombineInstance
                {
                    mesh = leaf % 2 == 0 ? leafCluster : canopy,
                    transform = Matrix4x4.TRS(leafPosition, leafRotation, leafScale)
                });
            }

            if (variant != 4)
            {
                var twigCount = 3 + variant % 3;
                for (var twig = 0; twig < twigCount; twig++)
                {
                    var angle = twig * (360f / twigCount) + variant * 11f;
                    instances.Add(new CombineInstance
                    {
                        mesh = branch,
                        transform = Matrix4x4.TRS(
                            Vector3.up * 0.12f,
                            Quaternion.Euler(0f, angle, -28f - (twig % 2) * 12f),
                            new Vector3(0.36f, 0.85f + variant * 0.06f, 0.36f))
                    });
                }
            }

            var mesh = new Mesh
            {
                name = "Procedural Bush Variant " + (variant + 1),
                indexFormat = instances.Count * canopy.vertexCount > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            try
            {
                mesh.CombineMeshes(instances.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                return mesh;
            }
            catch
            {
                DestroyMesh(mesh);
                throw;
            }
        }

        private static int CountVariants(bool[] variants)
        {
            return CountVariants(variants, 0, variants.Length);
        }

        private static int CountVariants(bool[] variants, int start, int count)
        {
            var total = 0;
            for (var i = start; i < start + count; i++)
            {
                if (variants[i])
                {
                    total++;
                }
            }

            return total;
        }

        private static Mesh BuildIcosphere()
        {
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            var vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0).normalized,
                new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized,
                new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized,
                new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized,
                new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized,
                new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized,
                new Vector3(-t, 0, 1).normalized
            };
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var midpointCache = new Dictionary<long, int>();
            var originalFaces = faces.ToArray();
            faces.Clear();
            for (var i = 0; i < originalFaces.Length; i += 3)
            {
                var a = originalFaces[i];
                var b = originalFaces[i + 1];
                var c = originalFaces[i + 2];
                var ab = GetMidpoint(a, b, vertices, midpointCache);
                var bc = GetMidpoint(b, c, vertices, midpointCache);
                var ca = GetMidpoint(c, a, vertices, midpointCache);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            var canopyVertices = vertices.ToArray();
            var canopyUv = new Vector2[canopyVertices.Length];
            for (var i = 0; i < canopyVertices.Length; i++)
            {
                canopyUv[i] = new Vector2(
                    0.5f + canopyVertices[i].x * 0.5f,
                    0.5f + canopyVertices[i].z * 0.5f);
            }

            var mesh = new Mesh
            {
                name = "Procedural Low-Poly Canopy",
                vertices = canopyVertices,
                uv = canopyUv,
                triangles = faces.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int GetMidpoint(int first, int second, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            var key = ((long)minimum << 32) + (uint)maximum;
            if (cache.TryGetValue(key, out var index))
            {
                return index;
            }

            index = vertices.Count;
            vertices.Add((vertices[first] + vertices[second]).normalized);
            cache.Add(key, index);
            return index;
        }

        internal static void DestroyMeshes(IReadOnlyList<Mesh> meshes)
        {
            for (var i = 0; i < meshes.Count; i++)
            {
                DestroyMesh(meshes[i]);
            }
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
