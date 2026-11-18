using System;
using System.Collections.Generic;
using DinoGenesis.Dinosaurs;
using DinoGenesis.Fossils;
using UnityEngine;

namespace DinoGenesis.World
{
    [DisallowMultipleComponent]
    public sealed class IslandWorld : MonoBehaviour
    {
        private const float HalfExtent = 145f;
        private const float IslandRadius = 127f;
        private const int GridResolution = 129;

        private readonly List<Landmark> landmarks = new List<Landmark>();
        private readonly List<Hill> hills = new List<Hill>();
        private readonly List<Mesh> vegetationMeshes = new List<Mesh>();
        private readonly List<Mesh> worldMeshes = new List<Mesh>();
        private readonly List<Texture2D> worldSurfaceTextures = new List<Texture2D>();
        private GameObject generatedRoot;
        private Mesh terrainMesh;
        private Material terrainMaterial;
        private Material waterMaterial;
        private Material trunkMaterial;
        private Material foliageMaterial;
        private Material rockMaterial;
        private Material researchMaterial;
        private Material marketMaterial;
        private Material zooMaterial;
        private Material signMaterial;
        private Material fossilMarkerMaterial;
        private Material birdMaterial;
        private Material canopyLightMaterial;
        private Material coniferMaterial;
        private Material understoryMaterial;
        private Material pathMaterial;
        private Material glassMaterial;
        private Material metalMaterial;
        private Material canvasMaterial;
        private Material stoneMaterial;
        private Texture2D terrainAlbedoTexture;
        private Texture2D terrainNormalTexture;
        private Vector2 noiseOffset;
        private Vector3 marketPosition;
        private Vector3 researchStationPosition;
        private Vector3 zooPosition;
        private bool hasMarket;
        private bool hasResearchStation;
        private bool hasZoo;

        public int CurrentSeed { get; private set; }
        public int GeneratedObjectCount { get; private set; }
        public int ExcavationSiteCount { get; private set; }
        public int TerrainVertexCount => terrainMesh == null ? 0 : terrainMesh.vertexCount;
        public int VegetationTreeCount { get; private set; }
        public int VegetationBroadleafCount { get; private set; }
        public int VegetationUmbrellaCount { get; private set; }
        public int VegetationConiferCount { get; private set; }
        public int VegetationUnderstoryCount { get; private set; }
        public int VegetationClusterCount { get; private set; }
        public int VegetationMeshVertexCount { get; private set; }
        public int VegetationTreeVariantCount { get; private set; }
        public int VegetationBroadleafVariantCount { get; private set; }
        public int VegetationUmbrellaVariantCount { get; private set; }
        public int VegetationConiferVariantCount { get; private set; }
        public int VegetationBushVariantCount { get; private set; }
        public int VegetationPlantVariantCount { get; private set; }
        public int RockVariantCount { get; private set; }
        public int RockCount { get; private set; }
        public int WorldPathCount { get; private set; }
        public int WorldPathVertexCount { get; private set; }
        public int TerrainSignature { get; private set; }
        public int TerrainSurfaceSignature { get; private set; }
        public Texture2D TerrainAlbedoTexture => terrainAlbedoTexture;
        public Texture2D TerrainNormalTexture => terrainNormalTexture;

        public bool IsNearMarket(Vector3 position, float range)
        {
            if (!hasMarket)
            {
                return false;
            }

            var offset = new Vector2(position.x - marketPosition.x, position.z - marketPosition.z);
            return offset.sqrMagnitude <= range * range;
        }

        public bool IsNearResearchStation(Vector3 position, float range)
        {
            if (!hasResearchStation)
            {
                return false;
            }

            var offset = new Vector2(position.x - researchStationPosition.x, position.z - researchStationPosition.z);
            return offset.sqrMagnitude <= range * range;
        }

        public bool IsNearZoo(Vector3 position, float range)
        {
            if (!hasZoo)
            {
                return false;
            }

            var offset = new Vector2(position.x - zooPosition.x, position.z - zooPosition.z);
            return offset.sqrMagnitude <= range * range;
        }

        public Vector3 GetZooEnclosurePosition(int enclosureIndex)
        {
            if (enclosureIndex < 0 || enclosureIndex > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(enclosureIndex), "The island supports three zoo enclosure sites.");
            }

            var x = 17f + enclosureIndex * 28f;
            var z = 22f;
            return new Vector3(x, SampleHeight(x, z), z);
        }

        private struct Hill
        {
            public Vector2 Center;
            public float Radius;
            public float Height;
        }

        private struct Landmark
        {
            public string Name;
            public Vector3 Position;
        }

        public void Generate(int seed)
        {
            CurrentSeed = seed;
            landmarks.Clear();
            hills.Clear();
            ExcavationSiteCount = 0;
            hasMarket = false;
            hasResearchStation = false;
            hasZoo = false;
            VegetationTreeCount = 0;
            VegetationBroadleafCount = 0;
            VegetationUmbrellaCount = 0;
            VegetationConiferCount = 0;
            VegetationUnderstoryCount = 0;
            VegetationClusterCount = 0;
            VegetationMeshVertexCount = 0;
            VegetationTreeVariantCount = 0;
            VegetationBroadleafVariantCount = 0;
            VegetationUmbrellaVariantCount = 0;
            VegetationConiferVariantCount = 0;
            VegetationBushVariantCount = 0;
            VegetationPlantVariantCount = 0;
            RockVariantCount = 0;
            RockCount = 0;
            WorldPathCount = 0;
            WorldPathVertexCount = 0;

            if (generatedRoot != null)
            {
                ReleaseTerrainMesh();
                ReleaseTerrainTextures();
                ReleaseVegetationMeshes();
                ReleaseWorldMeshes();
                DestroyRuntimeObject(generatedRoot);
            }

            generatedRoot = new GameObject("Generated Island (Seed " + seed + ")");
            generatedRoot.transform.SetParent(transform, false);
            EnsureMaterials();
            PrepareTerrain(seed);
            BuildOcean();
            BuildTerrain();
            BuildResearchStation();
            BuildMarket();
            BuildZooArea();
            BuildPaths();
            BuildVegetation(seed);
            BuildRocks(seed);
            BuildWildBirds(seed);
            BuildDigSiteLandmarks(seed);
            CountGeneratedObjects();
        }

        public float SampleHeight(float x, float z)
        {
            var radialDistance = Mathf.Sqrt(x * x + z * z);
            var radialFactor = radialDistance / IslandRadius;
            var coast = Mathf.SmoothStep(0f, 1f, (radialFactor - 0.78f) / (1.04f - 0.78f));
            var broadNoise = Mathf.PerlinNoise(
                (x + noiseOffset.x) * 0.012f,
                (z + noiseOffset.y) * 0.012f);
            var rollingNoise = Mathf.PerlinNoise(
                (x + noiseOffset.y) * 0.034f,
                (z + noiseOffset.x) * 0.034f);
            var detailNoise = Mathf.PerlinNoise(
                (x + noiseOffset.x * 0.73f) * 0.105f,
                (z + noiseOffset.y * 0.73f) * 0.105f);
            var landHeight = 1.35f +
                             (broadNoise - 0.5f) * 3.2f +
                             (rollingNoise - 0.5f) * 2.4f +
                             (detailNoise - 0.5f) * 0.48f;

            for (var i = 0; i < hills.Count; i++)
            {
                var hill = hills[i];
                var distance = Vector2.Distance(new Vector2(x, z), hill.Center);
                var influence = Mathf.Clamp01(1f - distance / hill.Radius);
                landHeight += hill.Height * influence * influence;
            }

            return Mathf.Lerp(landHeight, -3f, coast);
        }

        public Vector3 GetPlayerSpawnPosition()
        {
            const float spawnX = 0f;
            const float spawnZ = -12f;
            return new Vector3(spawnX, SampleHeight(spawnX, spawnZ) + 0.15f, spawnZ);
        }

        public bool TryGetNearbyLandmark(Vector3 position, float maxDistance, out string name, out float distance)
        {
            name = string.Empty;
            distance = maxDistance;
            var found = false;

            for (var i = 0; i < landmarks.Count; i++)
            {
                var landmark = landmarks[i];
                var landmarkDistance = Vector3.Distance(
                    new Vector3(position.x, 0f, position.z),
                    new Vector3(landmark.Position.x, 0f, landmark.Position.z));
                if (landmarkDistance > distance)
                {
                    continue;
                }

                name = landmark.Name;
                distance = landmarkDistance;
                found = true;
            }

            return found;
        }

        private void PrepareTerrain(int seed)
        {
            var random = new System.Random(seed);
            noiseOffset = new Vector2(random.Next(0, 100000), random.Next(0, 100000));

            for (var i = 0; i < 9; i++)
            {
                var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                var distance = 33f + (float)random.NextDouble() * 65f;
                hills.Add(new Hill
                {
                    Center = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance),
                    Radius = 16f + (float)random.NextDouble() * 26f,
                    Height = 2.5f + (float)random.NextDouble() * 7f
                });
            }
        }

        private void EnsureMaterials()
        {
            if (terrainMaterial != null)
            {
                return;
            }

            terrainMaterial = CreateMaterial("Seeded Island Ground", Color.white);
            terrainMaterial.SetFloat("_Smoothness", 0.08f);
            terrainMaterial.SetFloat("_Metallic", 0f);
            waterMaterial = CreateMaterial("Coastal Water", new Color(0.10f, 0.34f, 0.42f));
            trunkMaterial = CreateMaterial("Tree Bark", new Color(0.29f, 0.20f, 0.13f));
            foliageMaterial = CreateMaterial("Canopy Green", new Color(0.24f, 0.43f, 0.21f));
            canopyLightMaterial = CreateMaterial("Sunlit Canopy", new Color(0.40f, 0.56f, 0.27f));
            coniferMaterial = CreateMaterial("Island Conifer", new Color(0.18f, 0.34f, 0.21f));
            understoryMaterial = CreateMaterial("Fern Undergrowth", new Color(0.30f, 0.45f, 0.24f));
            rockMaterial = CreateMaterial("Basalt", new Color(0.34f, 0.36f, 0.32f));
            researchMaterial = CreateMaterial("Research Station", new Color(0.74f, 0.81f, 0.76f));
            marketMaterial = CreateMaterial("Market Ochre", new Color(0.78f, 0.51f, 0.24f));
            zooMaterial = CreateMaterial("Habitat Timber", new Color(0.48f, 0.37f, 0.25f));
            signMaterial = CreateMaterial("Signage", new Color(0.18f, 0.30f, 0.32f));
            fossilMarkerMaterial = CreateMaterial("Site Marker", new Color(0.78f, 0.53f, 0.23f));
            birdMaterial = CreateMaterial("Island Bird", new Color(0.72f, 0.52f, 0.31f));
            pathMaterial = CreateMaterial("Packed Island Trail", Color.white);
            glassMaterial = CreateMaterial("Laboratory Glazing", new Color(0.40f, 0.65f, 0.69f));
            metalMaterial = CreateMaterial("Field Station Metalwork", new Color(0.28f, 0.36f, 0.34f));
            canvasMaterial = CreateMaterial("Market Canvas", Color.white);
            stoneMaterial = CreateMaterial("Foundation Stone", Color.white);

            ApplySurface(trunkMaterial, WorldSurfaceStyle.WeatheredWood, 1, 0.12f, 0f);
            ApplySurface(zooMaterial, WorldSurfaceStyle.WeatheredWood, 1, 0.1f, 0f);
            ApplySurface(researchMaterial, WorldSurfaceStyle.PaintedPlaster, 1, 0.24f, 0f);
            ApplySurface(signMaterial, WorldSurfaceStyle.DarkMetal, 1, 0.38f, 0.18f);
            ApplySurface(marketMaterial, WorldSurfaceStyle.WovenCanvas, 1, 0.24f, 0f);
            ApplySurface(canvasMaterial, WorldSurfaceStyle.WovenCanvas, 1, 0.3f, 0f);
            ApplySurface(pathMaterial, WorldSurfaceStyle.Gravel, 1, 0.04f, 0f);
            ApplySurface(rockMaterial, WorldSurfaceStyle.VolcanicStone, 1, 0.08f, 0f);
            ApplySurface(stoneMaterial, WorldSurfaceStyle.VolcanicStone, 1, 0.12f, 0f);
            ApplySurface(glassMaterial, WorldSurfaceStyle.DarkMetal, 1, 0.82f, 0.12f);
            ApplySurface(foliageMaterial, WorldSurfaceStyle.CanopyLeaves, 2.5f, 0.14f, 0f);
            ApplySurface(canopyLightMaterial, WorldSurfaceStyle.CanopyLeaves, 2.5f, 0.14f, 0f);
            ApplySurface(coniferMaterial, WorldSurfaceStyle.PineNeedles, 2f, 0.16f, 0f);
            ApplySurface(understoryMaterial, WorldSurfaceStyle.FernLeaves, 2.5f, 0.1f, 0f);
            glassMaterial.SetFloat("_Smoothness", 0.82f);
            glassMaterial.SetFloat("_Metallic", 0.12f);
            metalMaterial.SetFloat("_Smoothness", 0.62f);
            metalMaterial.SetFloat("_Metallic", 0.38f);
        }

        private void ApplySurface(Material material, WorldSurfaceStyle style, float tiling, float smoothness, float metallic)
        {
            ProceduralWorldSurface.Generate(
                style,
                0,
                ProceduralWorldSurface.DefaultResolution,
                out var albedo,
                out var normal);
            worldSurfaceTextures.Add(albedo);
            worldSurfaceTextures.Add(normal);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTextureScale("_BumpMap", Vector2.one * tiling);
            material.SetFloat("_BumpScale", 0.22f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.EnableKeyword("_NORMALMAP");
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible Unity material shader was found.");
            }

            return new Material(shader)
            {
                name = materialName,
                color = color
            };
        }

        private void BuildOcean()
        {
            var ocean = CreatePrimitive("Ocean", PrimitiveType.Cube, generatedRoot.transform, waterMaterial);
            ocean.transform.position = new Vector3(0f, -3.1f, 0f);
            ocean.transform.localScale = new Vector3(900f, 1f, 900f);
        }

        private void BuildTerrain()
        {
            var vertices = new Vector3[GridResolution * GridResolution];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>((GridResolution - 1) * (GridResolution - 1) * 6);
            var vertex = 0;
            var signature = 17;

            for (var zIndex = 0; zIndex < GridResolution; zIndex++)
            {
                var z = Mathf.Lerp(-HalfExtent, HalfExtent, zIndex / (float)(GridResolution - 1));
                for (var xIndex = 0; xIndex < GridResolution; xIndex++)
                {
                    var x = Mathf.Lerp(-HalfExtent, HalfExtent, xIndex / (float)(GridResolution - 1));
                    var height = SampleHeight(x, z);
                    vertices[vertex] = new Vector3(x, height, z);
                    uv[vertex] = new Vector2(
                        xIndex / (float)(GridResolution - 1),
                        zIndex / (float)(GridResolution - 1));
                    if ((xIndex % 16 == 0) && (zIndex % 16 == 0))
                    {
                        signature = unchecked(signature * 31 + Mathf.RoundToInt(height * 1000f));
                    }

                    if (xIndex < GridResolution - 1 && zIndex < GridResolution - 1)
                    {
                        var nextRow = vertex + GridResolution;
                        triangles.Add(vertex);
                        triangles.Add(nextRow);
                        triangles.Add(vertex + 1);
                        triangles.Add(vertex + 1);
                        triangles.Add(nextRow);
                        triangles.Add(nextRow + 1);
                    }

                    vertex++;
                }
            }

            terrainMesh = new Mesh
            {
                name = "Seeded Low-Poly Island Terrain",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uv
            };
            terrainMesh.SetTriangles(triangles, 0);
            terrainMesh.RecalculateNormals();
            terrainMesh.RecalculateTangents();
            terrainMesh.RecalculateBounds();

            ProceduralTerrainSurface.Generate(
                CurrentSeed,
                ProceduralTerrainSurface.DefaultResolution,
                HalfExtent,
                IslandRadius,
                noiseOffset,
                SampleHeight,
                out terrainAlbedoTexture,
                out terrainNormalTexture,
                out var surfaceSignature);
            terrainMaterial.SetTexture("_BaseMap", terrainAlbedoTexture);
            terrainMaterial.SetTexture("_BumpMap", terrainNormalTexture);
            terrainMaterial.SetFloat("_BumpScale", 0.55f);
            terrainMaterial.EnableKeyword("_NORMALMAP");

            var terrain = new GameObject("Island Terrain");
            terrain.transform.SetParent(generatedRoot.transform, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            var collider = terrain.AddComponent<MeshCollider>();
            collider.sharedMesh = terrainMesh;
            TerrainSignature = signature;
            TerrainSurfaceSignature = surfaceSignature;
        }

        private void BuildResearchStation()
        {
            var center = new Vector3(-15f, SampleHeight(-15f, 22f), 22f);
            researchStationPosition = center;
            hasResearchStation = true;
            var building = CreateLandmarkRoot("Research Station", center);
            CreatePrimitive("Stone Plinth", PrimitiveType.Cube, building.transform, stoneMaterial,
                new Vector3(0f, 0.25f, 0f), new Vector3(13f, 0.5f, 10f));
            CreatePrimitive("Laboratory Floor", PrimitiveType.Cube, building.transform, researchMaterial,
                new Vector3(0f, 0.53f, 0f), new Vector3(12.4f, 0.12f, 9.4f));
            CreatePrimitive("Roof", PrimitiveType.Cube, building.transform, metalMaterial,
                new Vector3(0f, 4.1f, 0f), new Vector3(13f, 0.55f, 10f));
            CreatePrimitive("Roof Fascia", PrimitiveType.Cube, building.transform, researchMaterial,
                new Vector3(0f, 3.76f, -4.8f), new Vector3(12.6f, 0.16f, 0.2f));

            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    CreatePrimitive("Support", PrimitiveType.Cube, building.transform, researchMaterial,
                        new Vector3(x * 5.7f, 2.1f, z * 4.2f), new Vector3(0.45f, 3.7f, 0.45f));
                }

                CreatePrimitive("Laboratory Side Window", PrimitiveType.Cube, building.transform, glassMaterial,
                    new Vector3(x * 5.56f, 2.1f, 0f), new Vector3(0.12f, 1.65f, 5.4f));
                CreatePrimitive("Window Mullion", PrimitiveType.Cube, building.transform, metalMaterial,
                    new Vector3(x * 5.47f, 2.1f, 0f), new Vector3(0.18f, 0.12f, 5.5f));
            }

            CreatePrimitive("Rear Laboratory Wall", PrimitiveType.Cube, building.transform, researchMaterial,
                new Vector3(0f, 2.1f, 4.45f), new Vector3(11.5f, 3.1f, 0.32f));
            CreatePrimitive("Front Laboratory Counter", PrimitiveType.Cube, building.transform, zooMaterial,
                new Vector3(0f, 1.0f, -3.2f), new Vector3(5.4f, 0.9f, 0.8f));
            CreatePrimitive("Roof Vent", PrimitiveType.Cylinder, building.transform, metalMaterial,
                new Vector3(3.9f, 4.65f, 2.8f), new Vector3(0.34f, 0.34f, 0.34f));
            CreatePrimitive("Roof Solar Panel", PrimitiveType.Cube, building.transform, signMaterial,
                new Vector3(-3.3f, 4.47f, 1.4f), new Vector3(3.2f, 0.12f, 2.0f));
            for (var i = 0; i < 4; i++)
            {
                CreatePrimitive("Solar Cell", PrimitiveType.Cube, building.transform, glassMaterial,
                    new Vector3(-4.35f + i * 0.72f, 4.55f, 1.4f), new Vector3(0.035f, 0.035f, 1.8f));
            }

            CreateSign(building.transform, "LAB", new Vector3(0f, 2.25f, -4.55f));
        }

        private void BuildMarket()
        {
            var center = new Vector3(0f, SampleHeight(0f, 25f), 25f);
            var building = CreateLandmarkRoot("Island Market", center);
            marketPosition = center;
            hasMarket = true;
            CreatePrimitive("Market Stone Deck", PrimitiveType.Cube, building.transform, stoneMaterial,
                new Vector3(0f, 0.2f, 0f), new Vector3(10f, 0.4f, 8f));
            CreatePrimitive("Market Timber Deck", PrimitiveType.Cube, building.transform, zooMaterial,
                new Vector3(0f, 0.43f, 0f), new Vector3(9.4f, 0.1f, 7.4f));
            CreatePrimitive("Canopy Ridge", PrimitiveType.Cube, building.transform, trunkMaterial,
                new Vector3(0f, 3.96f, 0f), new Vector3(11.3f, 0.16f, 0.2f));

            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    CreatePrimitive("Canopy Post", PrimitiveType.Cylinder, building.transform, trunkMaterial,
                        new Vector3(x * 4.8f, 1.9f, z * 3.6f), new Vector3(0.22f, 1.9f, 0.22f));
                }
            }

            for (var i = 0; i < 5; i++)
            {
                CreatePrimitive("Canopy Awning Panel", PrimitiveType.Cube, building.transform,
                    i % 2 == 0 ? marketMaterial : canvasMaterial,
                    new Vector3(-4.4f + i * 2.2f, 3.7f, 0f), new Vector3(2.2f, 0.18f, 8.5f));
            }

            CreatePrimitive("Market Counter", PrimitiveType.Cube, building.transform, zooMaterial,
                new Vector3(0f, 1.0f, 1.8f), new Vector3(6.6f, 0.9f, 1.0f));
            CreatePrimitive("Market Countertop", PrimitiveType.Cube, building.transform, stoneMaterial,
                new Vector3(0f, 1.48f, 1.8f), new Vector3(6.9f, 0.12f, 1.2f));
            for (var i = 0; i < 3; i++)
            {
                CreatePrimitive("Supply Crate", PrimitiveType.Cube, building.transform, trunkMaterial,
                    new Vector3(-3.4f + i * 1.0f, 0.9f, -2.0f), new Vector3(0.76f, 0.72f, 0.76f));
            }

            CreateSign(building.transform, "MARKET", new Vector3(0f, 2.35f, -3.75f));
        }

        private void BuildZooArea()
        {
            var center = new Vector3(17f, SampleHeight(17f, 22f), 22f);
            zooPosition = center;
            hasZoo = true;
            var building = CreateLandmarkRoot("Zoo Habitat", center);
            const float halfWidth = 12f;
            const float halfDepth = 10f;
            const float fenceHeight = 1.5f;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    CreatePrimitive("Habitat Fence Post", PrimitiveType.Cube, building.transform, zooMaterial,
                        new Vector3(x * halfWidth, fenceHeight * 0.5f, z * halfDepth),
                        new Vector3(0.32f, fenceHeight, 0.32f));
                }
            }

            for (var x = -1; x <= 1; x += 2)
            {
                CreatePrimitive("Habitat Fence Rail", PrimitiveType.Cube, building.transform, zooMaterial,
                    new Vector3(x * halfWidth, fenceHeight * 0.5f, 0f),
                    new Vector3(0.22f, 0.22f, halfDepth * 2f));
            }

            for (var z = -1; z <= 1; z += 2)
            {
                var railLength = z < 0 ? 9f : halfWidth * 2f;
                CreatePrimitive("Habitat Fence Rail", PrimitiveType.Cube, building.transform, zooMaterial,
                    new Vector3(z < 0 ? -7.5f : 0f, fenceHeight * 0.5f, z * halfDepth),
                    new Vector3(railLength, 0.22f, 0.22f));
                if (z < 0)
                {
                    CreatePrimitive("Habitat Fence Rail", PrimitiveType.Cube, building.transform, zooMaterial,
                        new Vector3(7.5f, fenceHeight * 0.5f, z * halfDepth),
                        new Vector3(railLength, 0.22f, 0.22f));
                    CreatePrimitive("Habitat Entry Gate", PrimitiveType.Cube, building.transform, metalMaterial,
                        new Vector3(0f, 0.65f, z * halfDepth), new Vector3(2.6f, 1.3f, 0.14f));
                }
            }

            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    CreatePrimitive("Habitat Corner Cap", PrimitiveType.Cube, building.transform, metalMaterial,
                        new Vector3(x * halfWidth, fenceHeight + 0.08f, z * halfDepth),
                        new Vector3(0.52f, 0.16f, 0.52f));
                }
            }

            CreatePrimitive("Habitat Shade", PrimitiveType.Cube, building.transform, canvasMaterial,
                new Vector3(5f, 3.0f, 3f), new Vector3(6.5f, 0.16f, 5f));
            CreateSign(building.transform, "FUTURE HABITAT", new Vector3(0f, 2.2f, -10.6f));
        }

        private void BuildPaths()
        {
            var pathRoot = new GameObject("Access Trails");
            pathRoot.transform.SetParent(generatedRoot.transform, false);
            var vertices = new List<Vector3>(640);
            var uv = new List<Vector2>(640);
            var triangles = new List<int>(960);

            AddPathRoute(
                new[] { new Vector2(0f, -15f), new Vector2(0f, 0f), new Vector2(0f, 12f) },
                vertices, uv, triangles);
            AddPathRoute(
                new[] { new Vector2(0f, 12f), new Vector2(0f, 20f) },
                vertices, uv, triangles);
            AddPathRoute(
                new[] { new Vector2(0f, 12f), new Vector2(-8f, 12f), new Vector2(-15f, 16f) },
                vertices, uv, triangles);
            AddPathRoute(
                new[] { new Vector2(0f, 12f), new Vector2(8f, 12f), new Vector2(17f, 12f) },
                vertices, uv, triangles);

            var mesh = new Mesh
            {
                name = "Seeded Island Access Trails",
                indexFormat = vertices.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            worldMeshes.Add(mesh);
            var trailObject = new GameObject("Packed Gravel Paths");
            trailObject.transform.SetParent(pathRoot.transform, false);
            trailObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            trailObject.AddComponent<MeshRenderer>().sharedMaterial = pathMaterial;
            WorldPathCount = 4;
            WorldPathVertexCount = mesh.vertexCount;
        }

        private void AddPathRoute(
            Vector2[] controlPoints,
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles)
        {
            const float width = 3.2f;
            const float segmentLength = 1.6f;
            var accumulatedDistance = 0f;
            for (var routeSegment = 0; routeSegment < controlPoints.Length - 1; routeSegment++)
            {
                var start = controlPoints[routeSegment];
                var end = controlPoints[routeSegment + 1];
                var delta = end - start;
                var length = delta.magnitude;
                var steps = Mathf.Max(1, Mathf.CeilToInt(length / segmentLength));
                var direction = delta.normalized;
                var side = new Vector2(-direction.y, direction.x) * (width * 0.5f);
                for (var step = 0; step < steps; step++)
                {
                    var from = Vector2.Lerp(start, end, step / (float)steps);
                    var to = Vector2.Lerp(start, end, (step + 1f) / steps);
                    var baseIndex = vertices.Count;
                    vertices.Add(PathVertex(from - side));
                    vertices.Add(PathVertex(from + side));
                    vertices.Add(PathVertex(to - side));
                    vertices.Add(PathVertex(to + side));
                    var v0 = accumulatedDistance + length * step / steps;
                    var v1 = accumulatedDistance + length * (step + 1f) / steps;
                    uv.Add(new Vector2(0f, v0));
                    uv.Add(new Vector2(1f, v0));
                    uv.Add(new Vector2(0f, v1));
                    uv.Add(new Vector2(1f, v1));
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 3);
                    triangles.Add(baseIndex + 2);
                }

                accumulatedDistance += length;
            }

            Vector3 PathVertex(Vector2 point)
            {
                return new Vector3(point.x, SampleHeight(point.x, point.y) + 0.045f, point.y);
            }
        }

        private void BuildVegetation(int seed)
        {
            var vegetation = new GameObject("Vegetation");
            vegetation.transform.SetParent(generatedRoot.transform, false);
            var generated = ProceduralVegetationGenerator.Generate(
                vegetation.transform,
                seed,
                SampleHeight,
                new[] { trunkMaterial, foliageMaterial, canopyLightMaterial, coniferMaterial, understoryMaterial });
            vegetationMeshes.AddRange(generated.meshes);
            VegetationTreeCount = generated.treeCount;
            VegetationBroadleafCount = generated.broadleafTreeCount;
            VegetationUmbrellaCount = generated.umbrellaTreeCount;
            VegetationConiferCount = generated.coniferTreeCount;
            VegetationUnderstoryCount = generated.understoryCount;
            VegetationClusterCount = generated.clusterCount;
            VegetationMeshVertexCount = generated.vertexCount;
            VegetationTreeVariantCount = generated.uniqueTreeVariantCount;
            VegetationBroadleafVariantCount = generated.uniqueBroadleafVariantCount;
            VegetationUmbrellaVariantCount = generated.uniqueUmbrellaVariantCount;
            VegetationConiferVariantCount = generated.uniqueConiferVariantCount;
            VegetationBushVariantCount = generated.uniqueBushVariantCount;
            VegetationPlantVariantCount = generated.uniquePlantVariantCount;
        }

        private void BuildRocks(int seed)
        {
            var random = new System.Random(unchecked(seed * 7919 + 31));
            var rocks = new GameObject("Rock Formations");
            rocks.transform.SetParent(generatedRoot.transform, false);
            var variants = ProceduralRockGenerator.BuildVariants();
            worldMeshes.AddRange(variants);
            var usedVariants = new bool[variants.Length];

            for (var i = 0; i < 72; i++)
            {
                if (!TryGetNaturalPosition(random, out var position))
                {
                    continue;
                }

                var variant = random.Next(variants.Length);
                usedVariants[variant] = true;
                var rock = new GameObject("Faceted Basalt Variant " + (variant + 1));
                rock.transform.SetParent(rocks.transform, false);
                rock.transform.position = position;
                rock.transform.rotation = Quaternion.Euler(
                    (float)random.NextDouble() * 14f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 14f);
                rock.transform.localScale = new Vector3(
                    0.75f + (float)random.NextDouble() * 2.1f,
                    0.55f + (float)random.NextDouble() * 1.5f,
                    0.75f + (float)random.NextDouble() * 2f);
                rock.AddComponent<MeshFilter>().sharedMesh = variants[variant];
                rock.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
                rock.AddComponent<MeshCollider>().sharedMesh = variants[variant];
                RockCount++;
            }

            for (var i = 0; i < usedVariants.Length; i++)
            {
                if (usedVariants[i])
                {
                    RockVariantCount++;
                }
            }
        }

        private void BuildDigSiteLandmarks(int seed)
        {
            var random = new System.Random(unchecked(seed * 1543 + 53));
            var sites = new GameObject("Future Dig Site Markers");
            sites.transform.SetParent(generatedRoot.transform, false);
            var placed = 0;

            for (var attempt = 0; attempt < 90 && placed < 6; attempt++)
            {
                var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                var distance = 44f + (float)random.NextDouble() * 62f;
                var x = Mathf.Cos(angle) * distance;
                var z = Mathf.Sin(angle) * distance;
                if (SampleHeight(x, z) < -0.1f)
                {
                    continue;
                }

                var center = new Vector3(x, SampleHeight(x, z), z);
                var marker = new GameObject("Excavation Site " + (placed + 1));
                marker.transform.SetParent(sites.transform, false);
                marker.transform.position = center;
                CreatePrimitive("Site Marker Base", PrimitiveType.Cylinder, marker.transform, rockMaterial,
                    new Vector3(0f, 0.12f, 0f), new Vector3(1.2f, 0.12f, 1.2f));
                var markerStone = CreatePrimitive("Site Marker", PrimitiveType.Cube, marker.transform, fossilMarkerMaterial,
                    new Vector3(0f, 0.65f, 0f), new Vector3(0.28f, 0.75f, 0.28f));
                markerStone.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var excavationSite = marker.AddComponent<ExcavationSite>();
                excavationSite.Initialize(seed, placed, markerStone.GetComponent<Renderer>());
                var interactionCollider = marker.AddComponent<SphereCollider>();
                interactionCollider.center = new Vector3(0f, 0.8f, 0f);
                interactionCollider.radius = 1.6f;
                interactionCollider.isTrigger = true;
                landmarks.Add(new Landmark { Name = "Marked excavation site", Position = center });
                placed++;
            }

            ExcavationSiteCount = placed;
        }

        private void BuildWildBirds(int seed)
        {
            var random = new System.Random(unchecked(seed * 6151 + 71));
            var flock = new GameObject("Wild Birds");
            flock.transform.SetParent(generatedRoot.transform, false);

            for (var i = 0; i < 10; i++)
            {
                if (!TryGetNaturalPosition(random, out var position))
                {
                    continue;
                }

                var birdObject = new GameObject("Wild Island Bird " + (i + 1).ToString("D2"));
                birdObject.transform.SetParent(flock.transform, false);
                birdObject.transform.position = position;
                birdObject.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                birdObject.AddComponent<WildBird>().Initialize(this, unchecked(seed * 397 + i), i, birdMaterial);
            }
        }

        private GameObject CreateLandmarkRoot(string label, Vector3 position)
        {
            var root = new GameObject(label);
            root.transform.SetParent(generatedRoot.transform, false);
            root.transform.position = position;
            landmarks.Add(new Landmark { Name = label, Position = position });
            return root;
        }

        private void CreateSign(Transform parent, string label, Vector3 localPosition)
        {
            CreatePrimitive(label + " Sign", PrimitiveType.Cube, parent, signMaterial,
                localPosition, new Vector3(4.5f, 0.8f, 0.2f));
        }

        private bool TryGetNaturalPosition(System.Random random, out Vector3 position)
        {
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var x = (float)(random.NextDouble() * 230.0 - 115.0);
                var z = (float)(random.NextDouble() * 230.0 - 115.0);
                if (x * x + z * z < 40f * 40f || SampleHeight(x, z) < -0.2f)
                {
                    continue;
                }

                position = new Vector3(x, SampleHeight(x, z), z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Material material,
            Vector3 localPosition = default,
            Vector3 localScale = default)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = localScale == default ? Vector3.one : localScale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(instance);
            return instance;
        }

        private static void RemoveCollider(GameObject instance)
        {
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntimeObject(collider);
            }
        }

        private void CountGeneratedObjects()
        {
            var transforms = generatedRoot.GetComponentsInChildren<Transform>(true);
            GeneratedObjectCount = transforms.Length;
        }

        private void OnDestroy()
        {
            ReleaseTerrainMesh();
            ReleaseTerrainTextures();
            ReleaseVegetationMeshes();
            ReleaseWorldMeshes();
            ReleaseWorldSurfaceTextures();
            DestroyMaterial(terrainMaterial);
            DestroyMaterial(waterMaterial);
            DestroyMaterial(trunkMaterial);
            DestroyMaterial(foliageMaterial);
            DestroyMaterial(canopyLightMaterial);
            DestroyMaterial(coniferMaterial);
            DestroyMaterial(understoryMaterial);
            DestroyMaterial(rockMaterial);
            DestroyMaterial(researchMaterial);
            DestroyMaterial(marketMaterial);
            DestroyMaterial(zooMaterial);
            DestroyMaterial(signMaterial);
            DestroyMaterial(fossilMarkerMaterial);
            DestroyMaterial(birdMaterial);
            DestroyMaterial(pathMaterial);
            DestroyMaterial(glassMaterial);
            DestroyMaterial(metalMaterial);
            DestroyMaterial(canvasMaterial);
            DestroyMaterial(stoneMaterial);
        }

        private void ReleaseVegetationMeshes()
        {
            ProceduralVegetationGenerator.DestroyMeshes(vegetationMeshes);
            vegetationMeshes.Clear();
        }

        private void ReleaseWorldMeshes()
        {
            ProceduralVegetationGenerator.DestroyMeshes(worldMeshes);
            worldMeshes.Clear();
        }

        private void ReleaseWorldSurfaceTextures()
        {
            for (var i = 0; i < worldSurfaceTextures.Count; i++)
            {
                DestroyRuntimeObject(worldSurfaceTextures[i]);
            }

            worldSurfaceTextures.Clear();
        }

        private void ReleaseTerrainMesh()
        {
            if (terrainMesh == null)
            {
                return;
            }

            if (generatedRoot != null)
            {
                var meshColliders = generatedRoot.GetComponentsInChildren<MeshCollider>();
                foreach (var meshCollider in meshColliders)
                {
                    meshCollider.sharedMesh = null;
                }

                var meshFilters = generatedRoot.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh == terrainMesh)
                    {
                        meshFilter.sharedMesh = null;
                    }
                }
            }

            DestroyRuntimeObject(terrainMesh);
            terrainMesh = null;
        }

        private void ReleaseTerrainTextures()
        {
            if (terrainMaterial != null)
            {
                terrainMaterial.SetTexture("_BaseMap", null);
                terrainMaterial.SetTexture("_BumpMap", null);
            }

            if (terrainAlbedoTexture != null)
            {
                DestroyRuntimeObject(terrainAlbedoTexture);
            }

            if (terrainNormalTexture != null)
            {
                DestroyRuntimeObject(terrainNormalTexture);
            }

            terrainAlbedoTexture = null;
            terrainNormalTexture = null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                DestroyRuntimeObject(material);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
