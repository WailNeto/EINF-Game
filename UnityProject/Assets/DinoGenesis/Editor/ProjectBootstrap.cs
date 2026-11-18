using System;
using System.Collections.Generic;
using System.IO;
using DinoGenesis;
using DinoGenesis.Economy;
using DinoGenesis.Fossils;
using DinoGenesis.Player;
using DinoGenesis.Research;
using DinoGenesis.Dinosaurs;
using DinoGenesis.Zoo;
using DinoGenesis.World;
using DinoGenesis.Persistence;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DinoGenesis.Editor
{
    public static class ProjectBootstrap
    {
        private const string AssetRoot = "Assets/DinoGenesis";
        private const string RendererPath = AssetRoot + "/Settings/DinoGenesisRenderer.asset";
        private const string PipelinePath = AssetRoot + "/Settings/DinoGenesisPipeline.asset";
        private const string ScenePath = AssetRoot + "/Scenes/Main.unity";
        private const string FossilCatalogPath = AssetRoot + "/Data/DefaultFossilCatalog.asset";
        private const string ResearchCatalogPath = AssetRoot + "/Data/DefaultResearchCatalog.asset";
        private const string DinosaurCatalogPath = AssetRoot + "/Data/DefaultDinosaurCatalog.asset";

        [MenuItem("Dino Genesis/Configure Project")]
        public static void ConfigureProject()
        {
            EnsureFolder(AssetRoot);
            EnsureFolder(AssetRoot + "/Settings");
            EnsureFolder(AssetRoot + "/Scenes");
            EnsureFolder(AssetRoot + "/Data");
            var fossilCatalog = GetOrCreateFossilCatalog();
            var researchCatalog = GetOrCreateResearchCatalog();
            var dinosaurCatalog = GetOrCreateDinosaurCatalog();

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "DinoGenesisRenderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "DinoGenesisPipeline";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            pipeline.shadowDistance = 35f;
            EnableSoftShadows(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            PlayerSettings.companyName = "Dino Genesis";
            PlayerSettings.productName = "Dino Genesis";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.dinogenesis.game");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

            CreateStartupScene();
            ConfigurePhase1Scene(fossilCatalog, researchCatalog, dinosaurCatalog);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateConfiguration();
            ValidatePhase1Scene();

            Debug.Log("Dino Genesis Phase 4 configured: seeded island, excavation, economy, research, and laboratory.");
        }

        [MenuItem("Dino Genesis/Run Phase 1 Smoke Test")]
        public static void RunPhase1SmokeTest()
        {
            ConfigurePhase1Scene(GetOrCreateFossilCatalog(), GetOrCreateResearchCatalog(), GetOrCreateDinosaurCatalog());
            ValidatePhase1Scene();
            var testObject = new GameObject("Phase1 Smoke Test World");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                world.Generate(24017);
                var firstSignature = world.TerrainSignature;
                var firstSurfaceSignature = world.TerrainSurfaceSignature;
                var firstObjectCount = world.GeneratedObjectCount;
                var firstTreeCount = world.VegetationTreeCount;
                var firstBroadleafCount = world.VegetationBroadleafCount;
                var firstUmbrellaCount = world.VegetationUmbrellaCount;
                var firstConiferCount = world.VegetationConiferCount;
                var firstUnderstoryCount = world.VegetationUnderstoryCount;
                var firstVegetationClusterCount = world.VegetationClusterCount;
                var firstVegetationVertexCount = world.VegetationMeshVertexCount;
                var firstWorldPathVertexCount = world.WorldPathVertexCount;
                var firstSpawn = world.GetPlayerSpawnPosition();
                var albedo = world.TerrainAlbedoTexture;
                if (albedo == null || albedo.width != ProceduralTerrainSurface.DefaultResolution ||
                    albedo.height != ProceduralTerrainSurface.DefaultResolution ||
                    world.TerrainNormalTexture == null ||
                    world.TerrainNormalTexture.width != ProceduralTerrainSurface.DefaultResolution)
                {
                    throw new InvalidOperationException("The generated terrain does not have the expected albedo and normal surfaces.");
                }

                var terrainColors = new HashSet<Color32>();
                var texturePixels = albedo.GetPixels32();
                var greenGroundSamples = 0;
                var groundSamples = 0;
                for (var y = 0; y < albedo.height; y += 16)
                {
                    for (var x = 0; x < albedo.width; x += 16)
                    {
                        var color = texturePixels[y * albedo.width + x];
                        terrainColors.Add(color);
                        if (color.g > color.r + 12 && color.g > color.b)
                        {
                            greenGroundSamples++;
                        }

                        groundSamples++;
                    }
                }

                if (terrainColors.Count < 20)
                {
                    throw new InvalidOperationException("The generated terrain surface lacks natural color variation.");
                }

                if (greenGroundSamples < groundSamples * 0.2f)
                {
                    throw new InvalidOperationException("The generated island ground lacks sufficiently vivid grass color.");
                }

                var normalPixels = world.TerrainNormalTexture.GetPixels32();
                var tangentNormals = new HashSet<Color32>();
                for (var y = 8; y < albedo.height; y += 16)
                {
                    for (var x = 8; x < albedo.width; x += 16)
                    {
                        var pixel = normalPixels[y * albedo.width + x];
                        var nx = pixel.r / 127.5f - 1f;
                        var ny = pixel.g / 127.5f - 1f;
                        var nz = pixel.b / 127.5f - 1f;
                        var magnitude = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (Mathf.Abs(pixel.r - pixel.a) > 1 || magnitude < 0.94f || magnitude > 1.06f)
                        {
                            throw new InvalidOperationException(
                                "The terrain normal texture is not valid for URP RGB/DXT5nm normal decoding.");
                        }

                        tangentNormals.Add(pixel);
                    }
                }

                if (tangentNormals.Count < 10)
                {
                    throw new InvalidOperationException("Terrain slope normal details are missing or flat.");
                }

                var terrainTransform = world.transform.Find("Generated Island (Seed 24017)/Island Terrain");
                var terrainMesh = terrainTransform != null ? terrainTransform.GetComponent<MeshFilter>()?.sharedMesh : null;
                var terrainRenderer = terrainTransform != null ? terrainTransform.GetComponent<MeshRenderer>() : null;
                if (terrainMesh == null || terrainMesh.subMeshCount != 1 ||
                    terrainMesh.uv.Length != terrainMesh.vertexCount ||
                    terrainMesh.tangents.Length != terrainMesh.vertexCount ||
                    terrainRenderer == null ||
                    terrainRenderer.sharedMaterial.GetTexture("_BaseMap") != albedo ||
                    terrainRenderer.sharedMaterial.GetTexture("_BumpMap") != world.TerrainNormalTexture)
                {
                    throw new InvalidOperationException("Terrain UVs, tangents, and generated ground maps are not connected correctly.");
                }

                world.Generate(24017);
                if (world.TerrainSignature != firstSignature ||
                    world.TerrainSurfaceSignature != firstSurfaceSignature ||
                    world.GeneratedObjectCount != firstObjectCount ||
                    world.VegetationTreeCount != firstTreeCount ||
                    world.VegetationBroadleafCount != firstBroadleafCount ||
                    world.VegetationUmbrellaCount != firstUmbrellaCount ||
                    world.VegetationConiferCount != firstConiferCount ||
                    world.VegetationUnderstoryCount != firstUnderstoryCount ||
                    world.VegetationClusterCount != firstVegetationClusterCount ||
                    world.VegetationMeshVertexCount != firstVegetationVertexCount ||
                    world.WorldPathVertexCount != firstWorldPathVertexCount)
                {
                    throw new InvalidOperationException("The same world seed did not reproduce identical terrain output.");
                }

                world.Generate(24018);
                if (world.TerrainSignature == firstSignature ||
                    world.TerrainSurfaceSignature == firstSurfaceSignature)
                {
                    throw new InvalidOperationException("Different world seeds produced the same terrain or surface signature.");
                }

                if (world.TerrainVertexCount < 10000 || world.GeneratedObjectCount < 80 || world.GeneratedObjectCount > 1200)
                {
                    throw new InvalidOperationException(
                        $"Unexpected world geometry or complexity: {world.TerrainVertexCount} terrain vertices, " +
                        $"{world.GeneratedObjectCount} generated objects.");
                }

                if (world.ExcavationSiteCount != 6)
                {
                    throw new InvalidOperationException(
                        $"Expected six marked excavation sites but generated {world.ExcavationSiteCount}.");
                }

                if (world.VegetationTreeCount < 200 ||
                    world.VegetationBroadleafCount < 80 ||
                    world.VegetationUmbrellaCount < 25 ||
                    world.VegetationConiferCount < 10 ||
                    world.VegetationUnderstoryCount < 500 ||
                    world.VegetationTreeVariantCount != 18 ||
                    world.VegetationBroadleafVariantCount != 6 ||
                    world.VegetationUmbrellaVariantCount != 6 ||
                    world.VegetationConiferVariantCount != 6 ||
                    world.VegetationBushVariantCount != 6 ||
                    world.VegetationPlantVariantCount != 6 ||
                    world.VegetationClusterCount < 16 ||
                    world.VegetationClusterCount > 24 ||
                    world.VegetationMeshVertexCount <= 0 ||
                    world.VegetationMeshVertexCount > 2000000)
                {
                    throw new InvalidOperationException(
                        $"Unexpected forest output: {world.VegetationTreeCount} trees " +
                        $"({world.VegetationBroadleafCount} broadleaf, {world.VegetationUmbrellaCount} umbrella, " +
                        $"{world.VegetationConiferCount} conifers), {world.VegetationUnderstoryCount} understory plants, " +
                        $"{world.VegetationTreeVariantCount} tree ({world.VegetationBroadleafVariantCount} broadleaf, " +
                        $"{world.VegetationUmbrellaVariantCount} umbrella, {world.VegetationConiferVariantCount} conifer), " +
                        $"{world.VegetationBushVariantCount} bush, and " +
                        $"{world.VegetationPlantVariantCount} plant variants, " +
                        $"{world.VegetationClusterCount} clusters, " +
                        $"and {world.VegetationMeshVertexCount} LOD vertices.");
                }

                if (world.RockCount < 60 || world.RockVariantCount != 6)
                {
                    throw new InvalidOperationException(
                        $"Expected six faceted rock variants and at least 60 rocks; generated " +
                        $"{world.RockVariantCount} variants and {world.RockCount} rocks.");
                }

                var rockRoot = world.transform.Find("Generated Island (Seed 24018)/Rock Formations");
                var rockFilters = rockRoot != null
                    ? rockRoot.GetComponentsInChildren<MeshFilter>(true)
                    : Array.Empty<MeshFilter>();
                var rockColliders = rockRoot != null
                    ? rockRoot.GetComponentsInChildren<MeshCollider>(true)
                    : Array.Empty<MeshCollider>();
                var rockForms = new HashSet<string>();
                if (rockFilters.Length != world.RockCount || rockColliders.Length != world.RockCount)
                {
                    throw new InvalidOperationException("The generated rock formations are missing their meshes or colliders.");
                }

                for (var i = 0; i < rockFilters.Length; i++)
                {
                    var filter = rockFilters[i];
                    var renderer = filter.GetComponent<MeshRenderer>();
                    var collider = filter.GetComponent<MeshCollider>();
                    if (filter.sharedMesh == null || filter.sharedMesh.vertexCount < 180 ||
                        renderer == null || renderer.sharedMaterial.GetTexture("_BumpMap") == null ||
                        collider == null || collider.sharedMesh != filter.sharedMesh)
                    {
                        throw new InvalidOperationException("A basalt rock is missing its faceted geometry, rough normal map, or collider.");
                    }

                    rockForms.Add(filter.gameObject.name);
                }

                if (rockForms.Count != 6)
                {
                    throw new InvalidOperationException("The island did not generate all six distinct rock forms.");
                }

                var vegetationRoot = world.transform.Find("Generated Island (Seed 24018)/Vegetation");
                var vegetationLods = vegetationRoot != null
                    ? vegetationRoot.GetComponentsInChildren<LODGroup>(true)
                    : Array.Empty<LODGroup>();
                if (vegetationLods.Length != world.VegetationClusterCount)
                {
                    throw new InvalidOperationException("Vegetation clusters are missing their distance LOD groups.");
                }

                foreach (var lodGroup in vegetationLods)
                {
                    var lods = lodGroup.GetLODs();
                    if (lods.Length != 2 || lods[0].renderers.Length != 1 || lods[1].renderers.Length != 1)
                    {
                        throw new InvalidOperationException("A vegetation cluster must provide near and distant mesh LODs.");
                    }

                    var highFilter = lods[0].renderers[0].GetComponent<MeshFilter>();
                    var lowFilter = lods[1].renderers[0].GetComponent<MeshFilter>();
                    var highRenderer = lods[0].renderers[0] as MeshRenderer;
                    var lowRenderer = lods[1].renderers[0] as MeshRenderer;
                    if (highFilter == null || lowFilter == null || highRenderer == null || lowRenderer == null ||
                        highFilter.sharedMesh == null || lowFilter.sharedMesh == null ||
                        highFilter.sharedMesh.vertexCount <= lowFilter.sharedMesh.vertexCount ||
                        highFilter.sharedMesh.uv.Length != highFilter.sharedMesh.vertexCount ||
                        lowFilter.sharedMesh.uv.Length != lowFilter.sharedMesh.vertexCount ||
                        highFilter.sharedMesh.subMeshCount != highRenderer.sharedMaterials.Length ||
                        lowFilter.sharedMesh.subMeshCount != lowRenderer.sharedMaterials.Length)
                    {
                        throw new InvalidOperationException("Vegetation LOD meshes or their material slots are invalid.");
                    }
                }

                var leafSurfaceFound = false;
                foreach (var lodGroup in vegetationLods)
                {
                    var nearRenderer = lodGroup.GetLODs()[0].renderers[0];
                    foreach (var material in nearRenderer.sharedMaterials)
                    {
                        if (material.name != "Canopy Green" && material.name != "Sunlit Canopy" &&
                            material.name != "Island Conifer" && material.name != "Fern Undergrowth")
                        {
                            continue;
                        }

                        var leafAlbedo = material.GetTexture("_BaseMap") as Texture2D;
                        var leafNormal = material.GetTexture("_BumpMap") as Texture2D;
                        if (leafAlbedo == null || leafNormal == null ||
                            !material.IsKeywordEnabled("_NORMALMAP") ||
                            new HashSet<Color32>(leafAlbedo.GetPixels32()).Count < 20)
                        {
                            throw new InvalidOperationException("A forest foliage material is missing its textured albedo or normal detail.");
                        }

                        leafSurfaceFound = true;
                    }
                }

                if (!leafSurfaceFound)
                {
                    throw new InvalidOperationException("Forest LODs are missing textured broadleaf, fern, or conifer materials.");
                }

                if (vegetationRoot.GetComponentInChildren<Collider>(true) != null)
                {
                    throw new InvalidOperationException("Merged vegetation must not add gameplay-blocking colliders.");
                }

                var pathsRoot = world.transform.Find("Generated Island (Seed 24018)/Access Trails");
                var pathsFilter = pathsRoot != null ? pathsRoot.GetComponentInChildren<MeshFilter>() : null;
                var pathsRenderer = pathsRoot != null ? pathsRoot.GetComponentInChildren<MeshRenderer>() : null;
                var pathTexture = pathsRenderer != null ? pathsRenderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D : null;
                var pathNormal = pathsRenderer != null ? pathsRenderer.sharedMaterial.GetTexture("_BumpMap") as Texture2D : null;
                if (world.WorldPathCount != 4 || world.WorldPathVertexCount < 150 ||
                    pathsFilter == null || pathsFilter.sharedMesh == null ||
                    pathsFilter.sharedMesh.vertexCount != world.WorldPathVertexCount ||
                    pathsRenderer == null || pathTexture == null || pathNormal == null ||
                    pathTexture.width != ProceduralWorldSurface.DefaultResolution ||
                    pathTexture.wrapMode != TextureWrapMode.Repeat ||
                    !pathsRenderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP"))
                {
                    throw new InvalidOperationException(
                        "The island access routes or their seamless gravel albedo/normal maps are incomplete.");
                }

                var pathVertices = pathsFilter.sharedMesh.vertices;
                var pathNormals = pathsFilter.sharedMesh.normals;
                var upwardFacingVertices = 0;
                for (var i = 0; i < pathVertices.Length; i++)
                {
                    if (Mathf.Abs(pathVertices[i].y - world.SampleHeight(pathVertices[i].x, pathVertices[i].z) - 0.045f) > 0.001f)
                    {
                        throw new InvalidOperationException("The generated access path is not following the island terrain.");
                    }

                    if (pathNormals[i].y > 0.8f)
                    {
                        upwardFacingVertices++;
                    }
                }

                if (upwardFacingVertices < pathNormals.Length * 0.9f)
                {
                    throw new InvalidOperationException("The generated trail mesh has incorrectly oriented surface triangles.");
                }

                var trailColors = new HashSet<Color32>(pathTexture.GetPixels32());
                if (trailColors.Count < 20)
                {
                    throw new InvalidOperationException("The generated packed-gravel surface lacks visual texture variation.");
                }

                var laboratory = world.transform.Find(
                    "Generated Island (Seed 24018)/Research Station/Laboratory Side Window");
                var habitatGate = world.transform.Find(
                    "Generated Island (Seed 24018)/Zoo Habitat/Habitat Entry Gate");
                var marketCounter = world.transform.Find(
                    "Generated Island (Seed 24018)/Island Market/Market Counter");
                if (laboratory == null || laboratory.GetComponent<MeshRenderer>() == null ||
                    laboratory.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap") == null ||
                    habitatGate == null || marketCounter == null)
                {
                    throw new InvalidOperationException(
                        "The research, market, or habitat landmarks are missing their new world-building details.");
                }

                var graphicsPipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Linear ||
                    RenderSettings.fogEndDistance <= RenderSettings.fogStartDistance ||
                    graphicsPipeline == null || !graphicsPipeline.supportsSoftShadows)
                {
                    throw new InvalidOperationException(
                        "The island atmosphere is missing distance haze or URP soft-shadow support.");
                }

                if (world.TryGetNearbyLandmark(firstSpawn, 50f, out _, out _) == false)
                {
                    throw new InvalidOperationException("Starting area landmarks are not within walking distance.");
                }

                if (firstSpawn.y <= 0f)
                {
                    throw new InvalidOperationException("The player spawn is not above the island terrain.");
                }

                ValidateMenuInputGating();

                Debug.Log(
                    $"Phase 1 smoke test passed: {world.TerrainVertexCount} terrain vertices, " +
                    $"{world.GeneratedObjectCount} generated objects, {world.VegetationTreeCount} forest trees " +
                    $"({world.VegetationBroadleafCount} broadleaf, {world.VegetationUmbrellaCount} umbrella, " +
                    $"{world.VegetationConiferCount} conifer), {world.VegetationUnderstoryCount} understory plants, " +
                    $"{world.VegetationTreeVariantCount} tree forms ({world.VegetationBroadleafVariantCount} broadleaf, " +
                    $"{world.VegetationUmbrellaVariantCount} umbrella, {world.VegetationConiferVariantCount} conifer), " +
                    $"{world.VegetationBushVariantCount} bush and {world.VegetationPlantVariantCount} plant forms, " +
                    $"{world.VegetationMeshVertexCount} forest LOD vertices in {world.VegetationClusterCount} clusters, " +
                    $"{world.RockCount} rocks across {world.RockVariantCount} forms, " +
                    $"{world.WorldPathCount} textured paths, " +
                    $"reproducible seed {firstSignature}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 2 Smoke Test")]
        public static void RunPhase2SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            ConfigurePhase1Scene(fossilCatalog, GetOrCreateResearchCatalog(), GetOrCreateDinosaurCatalog());
            ValidatePhase1Scene();

            var testObject = new GameObject("Phase2 Smoke Test World");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                world.Generate(24017);
                var initialSignature = world.TerrainSignature;
                var initialObjectCount = world.GeneratedObjectCount;
                var sites = world.GetComponentsInChildren<ExcavationSite>();
                var firstSiteId = sites.Length > 0 ? sites[0].SiteId : string.Empty;
                if (sites.Length != 6)
                {
                    throw new InvalidOperationException($"Expected six excavation sites but generated {sites.Length}.");
                }

                var siteIds = new HashSet<string>();
                foreach (var site in sites)
                {
                    if (!siteIds.Add(site.SiteId) || site.Progress != 0f || site.IsComplete ||
                        site.RequiredWorkSeconds < 6.5f || site.RequiredWorkSeconds > 10f)
                    {
                        throw new InvalidOperationException("An excavation site has invalid or duplicate initial state.");
                    }

                    var siteCollider = site.GetComponent<SphereCollider>();
                    if (siteCollider == null || !siteCollider.isTrigger)
                    {
                        throw new InvalidOperationException("An excavation site is missing its non-blocking interaction trigger.");
                    }
                }

                if (!fossilCatalog.IsValid || fossilCatalog.Fossils.Count < 4)
                {
                    throw new InvalidOperationException("The fossil catalog is missing valid rarity definitions.");
                }

                var rarities = new HashSet<FossilRarity>();
                var saleValues = new HashSet<int>();
                var researchValues = new HashSet<int>();
                var fossilIds = new HashSet<string>();
                foreach (var fossil in fossilCatalog.Fossils)
                {
                    if (!rarities.Add(fossil.Rarity) ||
                        !saleValues.Add(fossil.SaleValue) ||
                        !researchValues.Add(fossil.ResearchValue) ||
                        !fossilIds.Add(fossil.Id))
                    {
                        throw new InvalidOperationException("The fossil catalog must contain distinct rarities, IDs, sale values, and research values.");
                    }
                }

                if (rarities.Count != 4)
                {
                    throw new InvalidOperationException("The fossil catalog must cover all four rarity tiers.");
                }

                var scenePlayer = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
                var economy = testObject.AddComponent<EconomySystem>();
                economy.Bind(fossilCatalog);
                var excavation = testObject.AddComponent<ExcavationSystem>();
                excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                var firstSite = sites[0];
                var halfWork = firstSite.RequiredWorkSeconds * 0.5f;
                if (excavation.TryExcavate(firstSite, halfWork) ||
                    Mathf.Abs(firstSite.Progress - 0.5f) > 0.001f ||
                    excavation.Discoveries.Count != 0)
                {
                    throw new InvalidOperationException("Partial excavation did not preserve progress without granting a fossil.");
                }

                if (!excavation.TryExcavate(firstSite, firstSite.RequiredWorkSeconds) ||
                    !firstSite.IsComplete ||
                    excavation.Discoveries.Count != 1)
                {
                    throw new InvalidOperationException("Completing an excavation did not grant exactly one fossil.");
                }

                var expectedFossil = fossilCatalog.GetForSite(firstSite.WorldSeed, firstSite.SiteIndex);
                if (excavation.LastDiscovery.SiteId != firstSite.SiteId ||
                    excavation.LastDiscovery.Definition.Id != expectedFossil.Id)
                {
                    throw new InvalidOperationException("Excavation did not award the deterministic fossil for its site.");
                }

                if (excavation.TryExcavate(firstSite, firstSite.RequiredWorkSeconds) || excavation.Discoveries.Count != 1)
                {
                    throw new InvalidOperationException("A completed excavation site awarded a duplicate fossil.");
                }

                var cameraObject = new GameObject("Phase2 Site Raycast Camera");
                cameraObject.transform.SetParent(testObject.transform, false);
                cameraObject.transform.position = sites[1].transform.position + new Vector3(0f, 1.3f, -3f);
                cameraObject.transform.rotation = Quaternion.LookRotation(
                    sites[1].transform.position + Vector3.up * 0.8f - cameraObject.transform.position);
                Physics.SyncTransforms();
                if (!Physics.Raycast(cameraObject.transform.position, cameraObject.transform.forward, out var hit, 4f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) ||
                    hit.collider.GetComponentInParent<ExcavationSite>() != sites[1])
                {
                    throw new InvalidOperationException("The crosshair ray cannot target an excavation-site marker.");
                }

                world.Generate(24017);
                var regeneratedSites = world.GetComponentsInChildren<ExcavationSite>();
                excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                if (world.TerrainSignature != initialSignature ||
                    world.GeneratedObjectCount != initialObjectCount ||
                    regeneratedSites.Length != 6 ||
                    regeneratedSites[0].SiteId != firstSiteId ||
                    regeneratedSites[0].Progress != 1f ||
                    !regeneratedSites[0].IsComplete ||
                    excavation.Discoveries.Count != 1 ||
                    economy.FossilInventory.Count != 1)
                {
                    throw new InvalidOperationException("Regenerating the same seed did not restore previously claimed sites and finds.");
                }

                world.Generate(24018);
                if (world.TerrainSignature == initialSignature)
                {
                    throw new InvalidOperationException("A different seed produced the same terrain signature.");
                }

                Debug.Log(
                    $"Phase 2 smoke test passed: {sites.Length} sites, partial and completed work, " +
                    $"one deterministic fossil ({expectedFossil.DisplayName}), duplicate prevention, raycast targeting, and seed reset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 3 Smoke Test")]
        public static void RunPhase3SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            ConfigurePhase1Scene(fossilCatalog, GetOrCreateResearchCatalog(), GetOrCreateDinosaurCatalog());
            ValidatePhase1Scene();

            var testObject = new GameObject("Phase3 Economy Smoke Test");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                world.Generate(24017);
                var scenePlayer = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
                var economy = testObject.AddComponent<EconomySystem>();
                economy.Bind(fossilCatalog);
                var excavation = testObject.AddComponent<ExcavationSystem>();
                excavation.Bind(world, scenePlayer, fossilCatalog, economy);

                if (economy.Coins != 0 || economy.EquippedTool != ExcavationTool.Hands ||
                    economy.TryPurchaseNextTool(out _) || economy.Coins < 0 ||
                    economy.Transactions.Count != 0)
                {
                    throw new InvalidOperationException("The economy did not start at zero or allowed an unaffordable purchase.");
                }

                var sites = world.GetComponentsInChildren<ExcavationSite>();
                foreach (var site in sites)
                {
                    if (!excavation.TryExcavate(site, site.RequiredWorkSeconds) || !site.IsComplete)
                    {
                        throw new InvalidOperationException("An excavation did not add its completed fossil to the inventory.");
                    }
                }

                if (economy.FossilInventory.Count != sites.Length ||
                    economy.FossilHistory.Count != sites.Length ||
                    economy.Transactions.Count != sites.Length)
                {
                    throw new InvalidOperationException("The inventory did not record exactly one discovery per excavated site.");
                }

                var expectedCoins = 0;
                var fossilSiteIds = new List<string>();
                for (var i = 0; i < economy.FossilInventory.Count; i++)
                {
                    var find = economy.FossilInventory[i];
                    expectedCoins = checked(expectedCoins + find.Definition.SaleValue);
                    fossilSiteIds.Add(find.SiteId);
                }

                var firstSale = economy.FossilInventory[0];
                if (!economy.TrySellFossil(firstSale.SiteId, out var firstSaleValue) ||
                    firstSaleValue != firstSale.Definition.SaleValue ||
                    economy.IsInInventory(firstSale.SiteId))
                {
                    throw new InvalidOperationException("Selling a fossil did not credit its value and remove it from inventory.");
                }

                var balanceAfterSale = economy.Coins;
                if (economy.TrySellFossil(firstSale.SiteId, out _) ||
                    economy.Coins != balanceAfterSale ||
                    economy.TryAddFossil(firstSale) ||
                    economy.Coins < 0)
                {
                    throw new InvalidOperationException("A sold fossil could be duplicated or sold a second time.");
                }

                for (var i = 1; i < fossilSiteIds.Count; i++)
                {
                    if (!economy.TrySellFossil(fossilSiteIds[i], out _))
                    {
                        throw new InvalidOperationException("An available fossil could not be sold.");
                    }
                }

                if (economy.Coins != expectedCoins || economy.FossilInventory.Count != 0)
                {
                    throw new InvalidOperationException("The marketplace balance did not match the fossil sale ledger.");
                }

                if (!economy.TryPurchaseNextTool(out _) ||
                    economy.EquippedTool != ExcavationTool.Shovel ||
                    economy.ExcavationSpeedMultiplier != 2f)
                {
                    throw new InvalidOperationException("The shovel purchase did not deduct funds or increase digging speed.");
                }

                if (!economy.TryPurchaseNextTool(out _) ||
                    economy.EquippedTool != ExcavationTool.ImprovedShovel ||
                    economy.ExcavationSpeedMultiplier != 3.5f)
                {
                    throw new InvalidOperationException("The improved shovel purchase did not deduct funds or increase digging speed.");
                }

                var balanceAfterTools = economy.Coins;
                if (economy.TryPurchaseNextTool(out _) ||
                    economy.Coins != balanceAfterTools ||
                    economy.Coins < 0 ||
                    economy.Transactions.Count != sites.Length + sites.Length + 2)
                {
                    throw new InvalidOperationException("Tool purchases allowed invalid spending or were not recorded correctly.");
                }

                var ledgerBalance = 0;
                var discoveredCount = 0;
                for (var i = 0; i < economy.Transactions.Count; i++)
                {
                    var transaction = economy.Transactions[i];
                    if (transaction.Type == EconomyTransactionType.FossilDiscovered)
                    {
                        discoveredCount++;
                    }
                    else if (transaction.Type == EconomyTransactionType.FossilSold)
                    {
                        ledgerBalance = checked(ledgerBalance + transaction.Amount);
                    }
                    else if (transaction.Type == EconomyTransactionType.ToolPurchased)
                    {
                        ledgerBalance = checked(ledgerBalance - transaction.Amount);
                    }
                }

                if (ledgerBalance != economy.Coins || discoveredCount != sites.Length)
                {
                    throw new InvalidOperationException("The transaction ledger does not reconcile with the wallet and fossil history.");
                }

                world.Generate(24017);
                excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                var restoredSites = world.GetComponentsInChildren<ExcavationSite>();
                if (restoredSites.Length != sites.Length ||
                    Array.Exists(restoredSites, site => !site.IsComplete) ||
                    economy.Coins != balanceAfterTools ||
                    economy.FossilHistory.Count != sites.Length)
                {
                    throw new InvalidOperationException("Changing worlds duplicated discoveries or lost economy progress.");
                }

                world.Generate(24018);
                excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                var newSeedSite = world.GetComponentsInChildren<ExcavationSite>()[0];
                if (newSeedSite.IsComplete ||
                    !excavation.TryExcavate(newSeedSite, newSeedSite.RequiredWorkSeconds / economy.ExcavationSpeedMultiplier) ||
                    !newSeedSite.IsComplete)
                {
                    throw new InvalidOperationException("The improved shovel did not accelerate excavation in a new world.");
                }

                if (!world.IsNearMarket(new Vector3(0f, 0f, 25f), 1f) ||
                    world.IsNearMarket(Vector3.zero, 8f))
                {
                    throw new InvalidOperationException("Market access range was not restricted to the market area.");
                }

                var sceneHud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
                var sceneBootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                var sceneResearch = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
                var sceneDinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
                var sceneZoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();
                scenePlayer.transform.position = new Vector3(0f, world.SampleHeight(0f, 25f), 25f);
                scenePlayer.SetInputEnabled(true);
                sceneResearch.Bind(GetOrCreateResearchCatalog(), economy);
                sceneDinosaurs.Bind(GetOrCreateDinosaurCatalog(), sceneResearch, world);
                sceneZoo.Bind(sceneDinosaurs, economy, world);
                sceneHud.Bind(world, scenePlayer, sceneBootstrap, excavation, economy,
                    sceneResearch, sceneDinosaurs, sceneZoo);
                sceneHud.ToggleInteraction();
                if (!sceneHud.MenuOpen || scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Opening the nearby market did not pause player input.");
                }

                sceneHud.CloseMarket();
                if (sceneHud.MenuOpen || !scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Closing the market did not restore player input.");
                }

                Debug.Log(
                    $"Phase 3 smoke test passed: {sites.Length} fossils sold once for {expectedCoins} coins, " +
                    $"both shovel upgrades purchased, transaction ledger balanced, market input gating, and seed-safe economy state.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 4 Smoke Test")]
        public static void RunPhase4SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            var researchCatalog = GetOrCreateResearchCatalog();
            ConfigurePhase1Scene(fossilCatalog, researchCatalog, GetOrCreateDinosaurCatalog());
            ValidatePhase1Scene();

            var testObject = new GameObject("Phase4 Research Smoke Test");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                var economy = testObject.AddComponent<EconomySystem>();
                economy.Bind(fossilCatalog);
                var excavation = testObject.AddComponent<ExcavationSystem>();
                var scenePlayer = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
                var research = testObject.AddComponent<ResearchSystem>();
                research.Bind(researchCatalog, economy);

                if (research.DnaFragments != 0 ||
                    research.CompletedMilestones.Count != 0 ||
                    research.NextMilestone == null ||
                    research.NextMilestone.DnaRequired != 10 ||
                    research.IsSpeciesUnlocked("velociraptor"))
                {
                    throw new InvalidOperationException("Research did not initialize with zero DNA and the first milestone.");
                }

                var generatedSiteCount = 0;
                var seed = 35001;
                while (generatedSiteCount < 36)
                {
                    world.Generate(seed++);
                    excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                    var sites = world.GetComponentsInChildren<ExcavationSite>();
                    foreach (var site in sites)
                    {
                        if (!excavation.TryExcavate(site, site.RequiredWorkSeconds) ||
                            !economy.IsInInventory(site.SiteId))
                        {
                            throw new InvalidOperationException("Excavated fossils were not added to the research inventory.");
                        }
                    }

                    generatedSiteCount += sites.Length;
                }

                if (economy.FossilInventory.Count != generatedSiteCount)
                {
                    throw new InvalidOperationException("Collected research specimens were not all available in inventory.");
                }

                var soldSpecimen = economy.FossilInventory[0];
                if (!economy.TrySellFossil(soldSpecimen.SiteId, out _) ||
                    research.TryAnalyzeFossil(soldSpecimen.SiteId, out _, out _) ||
                    research.DnaFragments != 0)
                {
                    throw new InvalidOperationException("A sold fossil could also be analyzed for DNA.");
                }

                var firstResearchSpecimen = economy.FossilInventory[0];
                var expectedFirstDna = firstResearchSpecimen.Definition.ResearchValue;
                if (!research.TryAnalyzeFossil(firstResearchSpecimen.SiteId, out var firstAward, out _) ||
                    firstAward != expectedFirstDna ||
                    !economy.IsAnalyzed(firstResearchSpecimen.SiteId) ||
                    economy.IsInInventory(firstResearchSpecimen.SiteId) ||
                    economy.TrySellFossil(firstResearchSpecimen.SiteId, out _) ||
                    research.TryAnalyzeFossil(firstResearchSpecimen.SiteId, out _, out _) ||
                    research.DnaFragments != expectedFirstDna)
                {
                    throw new InvalidOperationException("Analysis did not consume the specimen exactly once and award the correct DNA.");
                }

                var expectedDnaTotal = expectedFirstDna;
                var achievedSpeciesUnlock = research.IsSpeciesUnlocked("velociraptor");
                while (economy.FossilInventory.Count > 0 && !achievedSpeciesUnlock)
                {
                    var specimen = economy.FossilInventory[0];
                    if (!research.TryAnalyzeFossil(specimen.SiteId, out var awardedDna, out _))
                    {
                        throw new InvalidOperationException("An available specimen could not be analyzed.");
                    }

                    expectedDnaTotal = checked(expectedDnaTotal + awardedDna);
                    achievedSpeciesUnlock = research.IsSpeciesUnlocked("velociraptor");
                }

                if (!achievedSpeciesUnlock ||
                    research.DnaFragments != expectedDnaTotal ||
                    research.DnaFragments < 65 ||
                    research.CompletedMilestones.Count != 3 ||
                    research.NextMilestone != null ||
                    !research.IsSpeciesUnlocked("velociraptor"))
                {
                    throw new InvalidOperationException("Research thresholds did not unlock the small Velociraptor species at 65 DNA.");
                }

                var analyzedCount = economy.AnalyzedSiteIds.Count;
                var analysisTransactionCount = 0;
                for (var i = 0; i < economy.Transactions.Count; i++)
                {
                    if (economy.Transactions[i].Type == EconomyTransactionType.FossilAnalyzed)
                    {
                        analysisTransactionCount++;
                    }
                }

                if (economy.FossilInventory.Count != generatedSiteCount - analyzedCount - 1 ||
                    analysisTransactionCount != analyzedCount)
                {
                    throw new InvalidOperationException("Analyzed specimens remained sellable or the research ledger lost an analysis.");
                }

                var sceneWorld = UnityEngine.Object.FindObjectOfType<IslandWorld>();
                var sceneHud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
                var sceneResearch = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
                var sceneBootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                var sceneDinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
                var sceneZoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();
                sceneWorld.Generate(24017);
                var labPosition = new Vector3(-15f, sceneWorld.SampleHeight(-15f, 22f), 22f);
                if (!sceneWorld.IsNearResearchStation(labPosition, 1f) ||
                    sceneWorld.IsNearResearchStation(Vector3.zero, 8f))
                {
                    throw new InvalidOperationException("Research laboratory access is not limited to the lab area.");
                }

                scenePlayer.transform.position = labPosition;
                scenePlayer.SetInputEnabled(true);
                sceneResearch.Bind(researchCatalog, economy);
                sceneDinosaurs.Bind(GetOrCreateDinosaurCatalog(), sceneResearch, sceneWorld);
                sceneZoo.Bind(sceneDinosaurs, economy, sceneWorld);
                var sceneExcavation = UnityEngine.Object.FindObjectOfType<ExcavationSystem>();
                sceneHud.Bind(sceneWorld, scenePlayer, sceneBootstrap, sceneExcavation,
                    economy, sceneResearch, sceneDinosaurs, sceneZoo);
                sceneHud.ToggleInteraction();
                if (!sceneHud.ResearchOpen || scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Opening the lab did not gate player input.");
                }

                sceneHud.ToggleInteraction();
                if (sceneHud.MenuOpen || !scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Closing the lab did not restore player input.");
                }

                Debug.Log(
                    $"Phase 4 smoke test passed: {generatedSiteCount} specimens, mutually exclusive sale/analysis, " +
                    $"exact DNA awards, all research milestones, Velociraptor genome unlock, and lab input gating.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 5 Smoke Test")]
        public static void RunPhase5SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            var researchCatalog = GetOrCreateResearchCatalog();
            var dinosaurCatalog = GetOrCreateDinosaurCatalog();
            ConfigurePhase1Scene(fossilCatalog, researchCatalog, dinosaurCatalog);
            ValidatePhase1Scene();

            var testObject = new GameObject("Phase5 Dinosaur Smoke Test");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                var economy = testObject.AddComponent<EconomySystem>();
                economy.Bind(fossilCatalog);
                var research = testObject.AddComponent<ResearchSystem>();
                research.Bind(researchCatalog, economy);
                var dinosaurs = testObject.AddComponent<DinosaurSystem>();
                var scenePlayer = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
                var excavation = testObject.AddComponent<ExcavationSystem>();
                var generatedSiteCount = 0;
                var seed = 45001;

                while (generatedSiteCount < 36)
                {
                    world.Generate(seed++);
                    excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                    var sites = world.GetComponentsInChildren<ExcavationSite>();
                    foreach (var site in sites)
                    {
                        if (!excavation.TryExcavate(site, site.RequiredWorkSeconds))
                        {
                            throw new InvalidOperationException("The phase-5 genetics test could not collect research specimens.");
                        }
                    }

                    generatedSiteCount += sites.Length;
                }

                while (!research.IsSpeciesUnlocked("velociraptor") && economy.FossilInventory.Count > 0)
                {
                    var fossil = economy.FossilInventory[0];
                    if (!research.TryAnalyzeFossil(fossil.SiteId, out _, out _))
                    {
                        throw new InvalidOperationException("The phase-5 genetics test could not unlock the Velociraptor genome.");
                    }
                }

                dinosaurs.Bind(dinosaurCatalog, research, world);
                if (!research.IsSpeciesUnlocked("velociraptor") ||
                    dinosaurs.WildBirds.Count < 8 ||
                    dinosaurs.TryCreateEgg("velociraptor", out _))
                {
                    throw new InvalidOperationException("Dinosaur synthesis was not gated by research and a generated wild-bird flock.");
                }

                var bird = dinosaurs.WildBirds[0];
                var birdPlumageColors = new HashSet<Color32>();
                foreach (var wildBird in dinosaurs.WildBirds)
                {
                    if (wildBird.transform.Find("Left Eye") == null ||
                        wildBird.transform.Find("Right Eye") == null)
                    {
                        throw new InvalidOperationException("A wild bird is missing its visible eyes.");
                    }

                    var feathers = 0;
                    var birdParts = wildBird.GetComponentsInChildren<Transform>(true);
                    foreach (var part in birdParts)
                    {
                        if (part.name.Contains("Wing Feather"))
                        {
                            feathers++;
                        }
                    }

                    var plumage = wildBird.transform.Find("Body")?.GetComponent<Renderer>()?.sharedMaterial;
                    if (feathers < 10 || plumage == null)
                    {
                        throw new InvalidOperationException("A wild bird is missing individual wing feathers or plumage.");
                    }

                    birdPlumageColors.Add(plumage.color);
                }

                if (birdPlumageColors.Count < 6)
                {
                    throw new InvalidOperationException("The flock did not generate all six distinct bird appearances.");
                }

                bird.transform.position = new Vector3(80f, world.SampleHeight(80f, 80f), 80f);
                var cameraObject = new GameObject("Phase5 Capture Test Camera");
                cameraObject.transform.SetParent(testObject.transform, false);
                cameraObject.transform.position = bird.transform.position + new Vector3(0f, 0.35f, -3f);
                cameraObject.transform.LookAt(bird.transform.position + Vector3.up * 0.35f);
                var testCamera = cameraObject.AddComponent<Camera>();
                Physics.SyncTransforms();
                if (!dinosaurs.TryGetTargetedBird(testCamera, 6f, out var targetedBird) || targetedBird != bird)
                {
                    throw new InvalidOperationException("The phase-5 camera ray did not target the selected wild bird.");
                }

                if (!dinosaurs.TryCaptureBirdFromView(testCamera, 6f, out _) || dinosaurs.AvianSamples != 1 ||
                    dinosaurs.TryCaptureBird(bird, out _) ||
                    dinosaurs.AvianSamples != 1)
                {
                    throw new InvalidOperationException("A targeted wild bird did not yield exactly one avian DNA sample.");
                }

                if (!dinosaurs.TryCreateEgg("velociraptor", out _) ||
                    dinosaurs.AvianSamples != 0 ||
                    dinosaurs.Dinosaurs.Count != 1 ||
                    dinosaurs.Dinosaurs[0].LifeStage != DinosaurLifeStage.Egg)
                {
                    throw new InvalidOperationException("A captured avian DNA sample did not synthesize and consume one dinosaur egg.");
                }

                if (!dinosaurCatalog.TryGetSpecies("velociraptor", out var species))
                {
                    throw new InvalidOperationException("The Velociraptor species definition is missing.");
                }

                dinosaurs.AdvanceDevelopment(species.IncubationSeconds - 0.1f);
                if (dinosaurs.Dinosaurs[0].LifeStage != DinosaurLifeStage.Egg)
                {
                    throw new InvalidOperationException("The egg hatched before its incubation period elapsed.");
                }

                dinosaurs.AdvanceDevelopment(0.11f);
                if (dinosaurs.Dinosaurs[0].LifeStage != DinosaurLifeStage.Baby)
                {
                    throw new InvalidOperationException("The incubated egg did not hatch into a baby dinosaur.");
                }

                dinosaurs.AdvanceDevelopment(species.GrowthSeconds);
                if (dinosaurs.Dinosaurs[0].LifeStage != DinosaurLifeStage.Adult)
                {
                    throw new InvalidOperationException("The baby dinosaur did not mature into an adult.");
                }

                UnityEngine.Object.DestroyImmediate(cameraObject);
                Debug.Log(
                    $"Phase 5 smoke test passed: {dinosaurs.WildBirds.Count} generated birds, targeted capture, " +
                    "research/sample-gated egg synthesis, single-use avian sample, and egg-to-baby-to-adult progression.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 6 Smoke Test")]
        public static void RunPhase6SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            var researchCatalog = GetOrCreateResearchCatalog();
            var dinosaurCatalog = GetOrCreateDinosaurCatalog();
            ConfigurePhase1Scene(fossilCatalog, researchCatalog, dinosaurCatalog);
            ValidatePhase1Scene();

            var testObject = new GameObject("Phase6 Zoo Smoke Test");
            try
            {
                var world = testObject.AddComponent<IslandWorld>();
                var economy = testObject.AddComponent<EconomySystem>();
                economy.Bind(fossilCatalog);
                var excavation = testObject.AddComponent<ExcavationSystem>();
                var research = testObject.AddComponent<ResearchSystem>();
                research.Bind(researchCatalog, economy);
                var dinosaurs = testObject.AddComponent<DinosaurSystem>();
                var zoo = testObject.AddComponent<ZooSystem>();
                var scenePlayer = UnityEngine.Object.FindObjectOfType<FirstPersonController>();

                world.Generate(66001);
                dinosaurs.Bind(dinosaurCatalog, research, world);
                zoo.Bind(dinosaurs, economy, world);
                if (zoo.UnlockedEnclosureCount != 1 ||
                    !zoo.IsEnclosureUnlocked(0) ||
                    zoo.TryUnlockNextEnclosure(out _) ||
                    economy.Coins != 0 ||
                    economy.Transactions.Count != 0)
                {
                    throw new InvalidOperationException("The zoo did not start with one enclosure or allowed an unaffordable purchase.");
                }

                var generatedSiteCount = 0;
                var seed = 66002;
                while (generatedSiteCount < 60)
                {
                    world.Generate(seed++);
                    excavation.Bind(world, scenePlayer, fossilCatalog, economy);
                    var sites = world.GetComponentsInChildren<ExcavationSite>();
                    foreach (var site in sites)
                    {
                        if (!excavation.TryExcavate(site, site.RequiredWorkSeconds))
                        {
                            throw new InvalidOperationException("The zoo smoke test could not collect funds and research fossils.");
                        }
                    }

                    generatedSiteCount += sites.Length;
                }

                while (!research.IsSpeciesUnlocked("velociraptor") && economy.FossilInventory.Count > 0)
                {
                    var bestResearchValue = -1;
                    FossilFind bestFossil = null;
                    for (var i = 0; i < economy.FossilInventory.Count; i++)
                    {
                        var candidate = economy.FossilInventory[i];
                        if (candidate.Definition.ResearchValue > bestResearchValue)
                        {
                            bestResearchValue = candidate.Definition.ResearchValue;
                            bestFossil = candidate;
                        }
                    }

                    if (bestFossil == null ||
                        !research.TryAnalyzeFossil(bestFossil.SiteId, out _, out _))
                    {
                        throw new InvalidOperationException("The zoo smoke test could not unlock the first dinosaur genome.");
                    }
                }

                var inventoryIndex = 0;
                while (economy.Coins < 850 && inventoryIndex < economy.FossilInventory.Count)
                {
                    var fossil = economy.FossilInventory[inventoryIndex];
                    if (!economy.TrySellFossil(fossil.SiteId, out _))
                    {
                        throw new InvalidOperationException("A remaining fossil could not be sold to fund zoo expansion.");
                    }
                }

                var balanceBeforeUnlocks = economy.Coins;
                if (balanceBeforeUnlocks < 850 ||
                    !zoo.TryUnlockNextEnclosure(out _) ||
                    !zoo.IsEnclosureUnlocked(1) ||
                    !zoo.TryUnlockNextEnclosure(out _) ||
                    !zoo.IsEnclosureUnlocked(2) ||
                    zoo.UnlockedEnclosureCount != 3 ||
                    economy.Coins != balanceBeforeUnlocks - 850)
                {
                    throw new InvalidOperationException("Zoo enclosure purchases did not charge and unlock the correct tiers.");
                }

                dinosaurs.Bind(dinosaurCatalog, research, world);
                var bird = dinosaurs.WildBirds.Count > 0 ? dinosaurs.WildBirds[0] : null;
                if (bird == null ||
                    !dinosaurs.TryCaptureBird(bird, out _) ||
                    !dinosaurs.TryCreateEgg("velociraptor", out _))
                {
                    throw new InvalidOperationException("The zoo test could not create a researched dinosaur from a wild-bird sample.");
                }

                var species = dinosaurCatalog.Species[0];
                var dinosaur = dinosaurs.Dinosaurs[0];
                if (dinosaur.LifeStage != DinosaurLifeStage.Egg ||
                    zoo.TryAssignDinosaur(dinosaur.Id, 0, out _) ||
                    dinosaur.EnclosureIndex >= 0)
                {
                    throw new InvalidOperationException("The zoo accepted an egg before it matured.");
                }

                dinosaurs.AdvanceDevelopment(species.IncubationSeconds + species.GrowthSeconds);
                if (dinosaur.LifeStage != DinosaurLifeStage.Adult ||
                    !zoo.TryAssignDinosaur(dinosaur.Id, 0, out _) ||
                    dinosaur.EnclosureIndex != 0 ||
                    zoo.GetAssignedDinosaurId(0) != dinosaur.Id ||
                    zoo.TryAssignDinosaur(dinosaur.Id, 1, out _))
                {
                    throw new InvalidOperationException("The zoo did not enforce adult-only, single-enclosure dinosaur assignments.");
                }

                var balanceBeforeAdmission = economy.Coins;
                if (!zoo.SimulateVisitors(3, out var admissionRevenue, out _) ||
                    admissionRevenue != 6 ||
                    economy.Coins != balanceBeforeAdmission + admissionRevenue ||
                    zoo.VisitorsServed != 3 ||
                    zoo.TotalRevenue != 6)
                {
                    throw new InvalidOperationException("Visitor admissions did not create and account for the exact zoo revenue.");
                }

                if (!zoo.TryUnassignDinosaur(dinosaur.Id, out _) ||
                    !string.IsNullOrEmpty(zoo.GetAssignedDinosaurId(0)) ||
                    zoo.SimulateVisitors(2, out _, out _) ||
                    zoo.VisitorsServed != 3 ||
                    !zoo.TryAssignDinosaur(dinosaur.Id, 0, out _))
                {
                    throw new InvalidOperationException("Removing a dinosaur did not clear the enclosure or stop visitor income.");
                }

                var emptyZooObject = new GameObject("Empty Zoo Test");
                emptyZooObject.transform.SetParent(testObject.transform, false);
                var emptyDinosaurSystem = emptyZooObject.AddComponent<DinosaurSystem>();
                emptyDinosaurSystem.Bind(dinosaurCatalog, research, world);
                var emptyZoo = emptyZooObject.AddComponent<ZooSystem>();
                emptyZoo.Bind(emptyDinosaurSystem, economy, world);
                if (emptyZoo.SimulateVisitors(4, out _, out _) || emptyZoo.VisitorsServed != 0)
                {
                    throw new InvalidOperationException("An empty zoo incorrectly earned admission revenue.");
                }

                var sceneWorld = UnityEngine.Object.FindObjectOfType<IslandWorld>();
                var sceneHud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
                var sceneResearch = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
                var sceneDinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
                var sceneZoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();
                var sceneEconomy = UnityEngine.Object.FindObjectOfType<EconomySystem>();
                var sceneExcavation = UnityEngine.Object.FindObjectOfType<ExcavationSystem>();
                var sceneBootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                sceneWorld.Generate(66017);
                sceneResearch.Bind(researchCatalog, sceneEconomy);
                sceneDinosaurs.Bind(dinosaurCatalog, sceneResearch, sceneWorld);
                sceneZoo.Bind(sceneDinosaurs, sceneEconomy, sceneWorld);
                var zooPosition = sceneWorld.GetZooEnclosurePosition(0);
                if (!sceneWorld.IsNearZoo(zooPosition, 1f) ||
                    sceneWorld.IsNearZoo(Vector3.zero, 8f))
                {
                    throw new InvalidOperationException("Zoo interaction was not restricted to the zoo entrance.");
                }

                scenePlayer.transform.position = zooPosition;
                scenePlayer.SetInputEnabled(true);
                sceneHud.Bind(sceneWorld, scenePlayer, sceneBootstrap, sceneExcavation,
                    sceneEconomy, sceneResearch, sceneDinosaurs, sceneZoo);
                sceneHud.ToggleInteraction();
                if (!sceneHud.ZooOpen || scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Opening the zoo did not gate player input.");
                }

                sceneHud.ToggleInteraction();
                if (sceneHud.MenuOpen || !scenePlayer.InputEnabled)
                {
                    throw new InvalidOperationException("Closing the zoo did not restore player input.");
                }

                var purchaseCount = 0;
                var admissionCount = 0;
                for (var i = 0; i < economy.Transactions.Count; i++)
                {
                    if (economy.Transactions[i].Type == EconomyTransactionType.ZooEnclosurePurchased)
                    {
                        purchaseCount++;
                    }
                    else if (economy.Transactions[i].Type == EconomyTransactionType.ZooAdmissionRevenue)
                    {
                        admissionCount++;
                    }
                }

                if (purchaseCount != 2 || admissionCount != 1 || economy.Coins < 0)
                {
                    throw new InvalidOperationException("Zoo purchases and visitor revenue were not recorded correctly.");
                }

                var transactionBalance = 0;
                for (var i = 0; i < economy.Transactions.Count; i++)
                {
                    var transaction = economy.Transactions[i];
                    if (transaction.Type == EconomyTransactionType.FossilSold ||
                        transaction.Type == EconomyTransactionType.ZooAdmissionRevenue)
                    {
                        transactionBalance = checked(transactionBalance + transaction.Amount);
                    }
                    else if (transaction.Type == EconomyTransactionType.ZooEnclosurePurchased)
                    {
                        transactionBalance = checked(transactionBalance - transaction.Amount);
                    }
                }

                if (transactionBalance != economy.Coins)
                {
                    throw new InvalidOperationException("Zoo purchases and admission revenue did not reconcile with the coin ledger.");
                }

                var previousVisitors = zoo.VisitorsServed;
                var previousZooRevenue = zoo.TotalRevenue;
                world.Generate(66099);
                dinosaurs.Bind(dinosaurCatalog, research, world);
                zoo.Bind(dinosaurs, economy, world);
                if (zoo.GetAssignedDinosaurId(0) != dinosaur.Id ||
                    Vector3.Distance(dinosaur.Position, world.GetZooEnclosurePosition(0)) > 0.01f ||
                    zoo.VisitorsServed != previousVisitors ||
                    zoo.TotalRevenue != previousZooRevenue)
                {
                    throw new InvalidOperationException("Zoo assignments and progress did not survive world regeneration.");
                }

                Debug.Log(
                    $"Phase 6 smoke test passed: {zoo.UnlockedEnclosureCount} enclosures, adult assignment, " +
                    $"{zoo.VisitorsServed} visitors, {zoo.TotalRevenue} coins admission revenue, and zoo input gating.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Dino Genesis/Run Phase 7 Smoke Test")]
        public static void RunPhase7SmokeTest()
        {
            var fossilCatalog = GetOrCreateFossilCatalog();
            var researchCatalog = GetOrCreateResearchCatalog();
            var dinosaurCatalog = GetOrCreateDinosaurCatalog();
            ConfigurePhase1Scene(fossilCatalog, researchCatalog, dinosaurCatalog);
            ValidatePhase1Scene();

            var sourceRoot = new GameObject("Phase7 Save Source");
            var restoredRoot = new GameObject("Phase7 Save Restore");
            var testDirectory = Path.Combine(
                Path.GetTempPath(),
                "dino-genesis-phase7-" + Guid.NewGuid().ToString("N"));
            try
            {
                var seed = 71007;
                var sourceWorld = sourceRoot.AddComponent<IslandWorld>();
                sourceWorld.Generate(seed);
                var sourceEconomy = sourceRoot.AddComponent<EconomySystem>();
                sourceEconomy.Bind(fossilCatalog);
                var sourceResearch = sourceRoot.AddComponent<ResearchSystem>();
                sourceResearch.Bind(researchCatalog, sourceEconomy);
                var sourceDinosaurs = sourceRoot.AddComponent<DinosaurSystem>();
                sourceDinosaurs.Bind(dinosaurCatalog, sourceResearch, sourceWorld);
                var sourceZoo = sourceRoot.AddComponent<ZooSystem>();
                sourceZoo.Bind(sourceDinosaurs, sourceEconomy, sourceWorld);

                var sites = sourceWorld.GetComponentsInChildren<ExcavationSite>();
                var fossil = fossilCatalog.Fossils[fossilCatalog.Fossils.Count - 1];
                if (sites.Length < 3)
                {
                    throw new InvalidOperationException("The phase 7 test world has too few excavation sites.");
                }

                for (var i = 0; i < 3; i++)
                {
                    if (!sourceEconomy.TryAddFossil(new FossilFind(sites[i].SiteId, fossil)))
                    {
                        throw new InvalidOperationException("The save test could not record an excavated fossil.");
                    }
                }

                if (!sourceResearch.TryAnalyzeFossil(sites[0].SiteId, out _, out _) ||
                    !sourceResearch.TryAnalyzeFossil(sites[1].SiteId, out _, out _) ||
                    !sourceEconomy.TrySellFossil(sites[2].SiteId, out _) ||
                    !sourceEconomy.TryPurchaseNextTool(out _) ||
                    !sourceEconomy.TryPurchaseNextTool(out _))
                {
                    throw new InvalidOperationException("The save test could not prepare economy and research state.");
                }

                var capturedBird = sourceDinosaurs.WildBirds.Count == 0 ? null : sourceDinosaurs.WildBirds[0];
                if (capturedBird == null ||
                    !sourceDinosaurs.TryCaptureBird(capturedBird, out _) ||
                    !sourceDinosaurs.TryCreateEgg("velociraptor", out _))
                {
                    throw new InvalidOperationException("The save test could not create dinosaur progress.");
                }

                sourceDinosaurs.AdvanceDevelopment(90f);
                var sourceDinosaur = sourceDinosaurs.Dinosaurs[0];
                if (sourceDinosaur.LifeStage != DinosaurLifeStage.Adult ||
                    !sourceZoo.TryAssignDinosaur(sourceDinosaur.Id, 0, out _) ||
                    !sourceZoo.SimulateVisitors(3, out _, out _))
                {
                    throw new InvalidOperationException("The save test could not prepare zoo progress.");
                }

                var expectedSave = new GameSaveData
                {
                    worldSeed = seed,
                    economy = sourceEconomy.CaptureSaveData(),
                    research = sourceResearch.CaptureSaveData(),
                    dinosaurs = sourceDinosaurs.CaptureSaveData(),
                    zoo = sourceZoo.CaptureSaveData()
                };
                var databasePath = Path.Combine(testDirectory, "save.sqlite3");
                var repository = new SqliteGameSaveRepository(databasePath);
                if (repository.HasSave)
                {
                    throw new InvalidOperationException("A fresh SQLite save database unexpectedly contained a saved game.");
                }

                repository.Save(expectedSave);
                if (!repository.HasSave || !File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
                {
                    throw new InvalidOperationException("The SQLite save was not written to the requested database file.");
                }

                var restoredSave = repository.Load();
                if (restoredSave == null || restoredSave.worldSeed != seed ||
                    restoredSave.economy.coins != expectedSave.economy.coins ||
                    restoredSave.economy.transactions.Length != expectedSave.economy.transactions.Length)
                {
                    throw new InvalidOperationException("The SQLite game save did not round-trip its seed and economy ledger.");
                }

                var restoredWorld = restoredRoot.AddComponent<IslandWorld>();
                restoredWorld.Generate(restoredSave.worldSeed);
                var restoredEconomy = restoredRoot.AddComponent<EconomySystem>();
                restoredEconomy.Bind(fossilCatalog);
                restoredEconomy.RestoreSaveData(restoredSave.economy);
                var restoredResearch = restoredRoot.AddComponent<ResearchSystem>();
                restoredResearch.Bind(researchCatalog, restoredEconomy);
                restoredResearch.RestoreSaveData(restoredSave.research);
                var restoredDinosaurs = restoredRoot.AddComponent<DinosaurSystem>();
                restoredDinosaurs.Bind(dinosaurCatalog, restoredResearch, restoredWorld, false);
                restoredDinosaurs.RestoreSaveData(restoredSave.dinosaurs);
                var restoredZoo = restoredRoot.AddComponent<ZooSystem>();
                restoredZoo.Bind(restoredDinosaurs, restoredEconomy, restoredWorld);
                restoredZoo.RestoreSaveData(restoredSave.zoo);
                var restoredExcavation = restoredRoot.AddComponent<ExcavationSystem>();
                restoredExcavation.Bind(
                    restoredWorld,
                    UnityEngine.Object.FindObjectOfType<FirstPersonController>(),
                    fossilCatalog,
                    restoredEconomy);

                var restoredSites = restoredWorld.GetComponentsInChildren<ExcavationSite>();
                var completedSiteCount = 0;
                for (var i = 0; i < restoredSites.Length; i++)
                {
                    if (restoredSites[i].IsComplete)
                    {
                        completedSiteCount++;
                    }
                }

                var restoredBird = null as WildBird;
                for (var i = 0; i < restoredDinosaurs.WildBirds.Count; i++)
                {
                    if (restoredDinosaurs.WildBirds[i].Index == capturedBird.Index)
                    {
                        restoredBird = restoredDinosaurs.WildBirds[i];
                        break;
                    }
                }

                if (restoredEconomy.Coins != sourceEconomy.Coins ||
                    restoredEconomy.EquippedTool != ExcavationTool.ImprovedShovel ||
                    restoredEconomy.FossilHistory.Count != 3 ||
                    restoredEconomy.FossilInventory.Count != 0 ||
                    restoredEconomy.AnalyzedSiteIds.Count != 2 ||
                    restoredResearch.DnaFragments != 90 ||
                    !restoredResearch.IsSpeciesUnlocked("velociraptor") ||
                    restoredDinosaurs.AvianSamples != 0 ||
                    restoredBird == null || !restoredBird.IsCaptured ||
                    restoredDinosaurs.Dinosaurs.Count != 1 ||
                    restoredDinosaurs.Dinosaurs[0].LifeStage != DinosaurLifeStage.Adult ||
                    restoredZoo.GetAssignedDinosaurId(0) != sourceDinosaur.Id ||
                    restoredZoo.VisitorsServed != 3 || restoredZoo.TotalRevenue != 6 ||
                    completedSiteCount != 3 || restoredExcavation.Discoveries.Count != 3)
                {
                    throw new InvalidOperationException("Restoring the SQLite save did not reconstruct gameplay, excavation, dinosaur, and zoo state.");
                }

                Debug.Log(
                    $"Phase 7 smoke test passed: SQLite ARM64 plugin opened {databasePath}; seed, " +
                    "economy ledger, fossils, research, bird capture, adult dinosaur, zoo, and completed sites round-tripped.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceRoot);
                UnityEngine.Object.DestroyImmediate(restoredRoot);
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }

                AssetDatabase.SaveAssets();
            }
        }

        public static void BuildMacOSPlayerForPhase7Validation()
        {
            ConfigurePhase1Scene(GetOrCreateFossilCatalog(), GetOrCreateResearchCatalog(), GetOrCreateDinosaurCatalog());
            ValidateConfiguration();
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                "DinoGenesis-Phase7-MacOS-" + DateTime.UtcNow.Ticks + ".app");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "The macOS ARM64 IL2CPP validation build failed: " +
                    report.summary.result + " (" + report.summary.totalErrors + " error(s)).");
            }

            Debug.Log("Phase 7 macOS ARM64 IL2CPP validation build succeeded: " + outputPath);
        }

        private static void ValidateMenuInputGating()
        {
            var sceneWorld = UnityEngine.Object.FindObjectOfType<IslandWorld>();
            var player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
            var hud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
            var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            var excavation = UnityEngine.Object.FindObjectOfType<ExcavationSystem>();
            var economy = UnityEngine.Object.FindObjectOfType<EconomySystem>();
            var research = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
            var dinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
            var zoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();

            if (sceneWorld == null || player == null || hud == null ||
                bootstrap == null || excavation == null || economy == null || research == null ||
                dinosaurs == null || zoo == null)
            {
                throw new InvalidOperationException("The startup scene cannot run the menu input-gating check.");
            }

            sceneWorld.Generate(24017);
            var fossilCatalog = AssetDatabase.LoadAssetAtPath<FossilCatalog>(FossilCatalogPath);
            var researchCatalog = AssetDatabase.LoadAssetAtPath<ResearchCatalog>(ResearchCatalogPath);
            var dinosaurCatalog = AssetDatabase.LoadAssetAtPath<DinosaurCatalog>(DinosaurCatalogPath);
            economy.Bind(fossilCatalog);
            research.Bind(researchCatalog, economy);
            excavation.Bind(sceneWorld, player, fossilCatalog, economy);
            dinosaurs.Bind(dinosaurCatalog, research, sceneWorld);
            zoo.Bind(dinosaurs, economy, sceneWorld);
            player.BindHud(hud);
            hud.Bind(sceneWorld, player, bootstrap, excavation, economy, research, dinosaurs, zoo);
            hud.TogglePause();
            if (player.InputEnabled)
            {
                throw new InvalidOperationException("Player input remained enabled while the pause menu was open.");
            }

            hud.TogglePause();
            if (!player.InputEnabled)
            {
                throw new InvalidOperationException("Player input was not restored after closing the pause menu.");
            }

            hud.ToggleFieldNotes();
            if (player.InputEnabled)
            {
                throw new InvalidOperationException("Player input remained enabled while field notes were open.");
            }

            hud.ToggleFieldNotes();
            if (!player.InputEnabled)
            {
                throw new InvalidOperationException("Player input was not restored after closing field notes.");
            }

            var researchStation = new Vector3(-15f, sceneWorld.SampleHeight(-15f, 22f), 22f);
            player.transform.position = researchStation;
            hud.ToggleInteraction();
            if (!hud.ResearchOpen || player.InputEnabled)
            {
                throw new InvalidOperationException("The nearby research laboratory did not open with player input gated.");
            }

            hud.ToggleInteraction();
            if (hud.MenuOpen || !player.InputEnabled)
            {
                throw new InvalidOperationException("Closing the research laboratory did not restore player input.");
            }
        }

        private static void CreateStartupScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.name = "Main Camera";
                camera.transform.position = new Vector3(0f, 1.6f, -6f);
                camera.transform.rotation = Quaternion.identity;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void ConfigurePhase1Scene(
            FossilCatalog fossilCatalog,
            ResearchCatalog researchCatalog,
            DinosaurCatalog dinosaurCatalog)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                throw new InvalidOperationException("The startup scene requires the configured URP pipeline asset.");
            }

            pipeline.shadowDistance = 35f;
            EnableSoftShadows(pipeline);
            var playerObject = GameObject.Find("DinoGenesis Player");
            if (playerObject == null)
            {
                playerObject = new GameObject("DinoGenesis Player");
            }

            var controller = playerObject.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = playerObject.AddComponent<CharacterController>();
            }

            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 48f;
            controller.stepOffset = 0.3f;

            var firstPerson = playerObject.GetComponent<FirstPersonController>();
            if (firstPerson == null)
            {
                firstPerson = playerObject.AddComponent<FirstPersonController>();
            }

            var playerCamera = Camera.main;
            if (playerCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                playerCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            playerCamera.transform.SetParent(playerObject.transform, false);
            playerCamera.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = 76f;
            firstPerson.SetCamera(playerCamera);

            var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
            if (world == null)
            {
                world = new GameObject("Island World").AddComponent<IslandWorld>();
            }

            var hud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
            if (hud == null)
            {
                hud = new GameObject("Exploration HUD").AddComponent<ExplorationHud>();
            }

            var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
            }

            var excavation = UnityEngine.Object.FindObjectOfType<ExcavationSystem>();
            if (excavation == null)
            {
                excavation = new GameObject("Excavation System").AddComponent<ExcavationSystem>();
            }

            var economy = UnityEngine.Object.FindObjectOfType<EconomySystem>();
            if (economy == null)
            {
                economy = new GameObject("Player Economy").AddComponent<EconomySystem>();
            }

            var research = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
            if (research == null)
            {
                research = new GameObject("Research System").AddComponent<ResearchSystem>();
            }

            researchCatalog = AssetDatabase.LoadAssetAtPath<ResearchCatalog>(ResearchCatalogPath);
            research.Bind(researchCatalog, economy);
            var dinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
            if (dinosaurs == null)
            {
                dinosaurs = new GameObject("Dinosaur System").AddComponent<DinosaurSystem>();
            }

            var zoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();
            if (zoo == null)
            {
                zoo = new GameObject("Zoo System").AddComponent<ZooSystem>();
            }

            dinosaurCatalog = AssetDatabase.LoadAssetAtPath<DinosaurCatalog>(DinosaurCatalogPath);
            bootstrap.Configure(
                world, firstPerson, hud, excavation, economy, research,
                fossilCatalog, researchCatalog, dinosaurs, dinosaurCatalog, zoo);

            var sun = FindDirectionalLight();
            if (sun == null)
            {
                var lightObject = new GameObject("Island Sun");
                sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.91f, 0.78f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.64f, 0.77f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.49f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.23f, 0.25f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.59f, 0.68f, 0.72f);
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 285f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Light FindDirectionalLight()
        {
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        private static void EnableSoftShadows(UniversalRenderPipelineAsset pipeline)
        {
            var serializedPipeline = new SerializedObject(pipeline);
            var softShadows = serializedPipeline.FindProperty("m_SoftShadowsSupported");
            if (softShadows == null)
            {
                throw new InvalidOperationException("The URP asset does not expose its soft-shadow setting.");
            }

            softShadows.boolValue = true;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static void ValidatePhase1Scene()
        {
            var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            var world = UnityEngine.Object.FindObjectOfType<IslandWorld>();
            var hud = UnityEngine.Object.FindObjectOfType<ExplorationHud>();
            var player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
            var excavation = UnityEngine.Object.FindObjectOfType<ExcavationSystem>();
            var economy = UnityEngine.Object.FindObjectOfType<EconomySystem>();
            var research = UnityEngine.Object.FindObjectOfType<ResearchSystem>();
            var dinosaurs = UnityEngine.Object.FindObjectOfType<DinosaurSystem>();
            var zoo = UnityEngine.Object.FindObjectOfType<ZooSystem>();
            var researchCatalog = AssetDatabase.LoadAssetAtPath<ResearchCatalog>(ResearchCatalogPath);
            var dinosaurCatalog = AssetDatabase.LoadAssetAtPath<DinosaurCatalog>(DinosaurCatalogPath);

            if (bootstrap == null || !bootstrap.IsConfigured || world == null || hud == null ||
                player == null || excavation == null || economy == null ||
                research == null || researchCatalog == null || !researchCatalog.IsValid ||
                dinosaurs == null || dinosaurCatalog == null || !dinosaurCatalog.IsValid || zoo == null)
            {
                throw new InvalidOperationException("The startup scene is missing a wired world, player, HUD, excavation/economy/research systems, catalogs, or bootstrap.");
            }

            if (player.GetComponent<CharacterController>() == null || player.PlayerCamera == null ||
                player.PlayerCamera.transform.parent != player.transform)
            {
                throw new InvalidOperationException("The first-person player requires a CharacterController and child camera.");
            }
        }

        private static void ValidateConfiguration()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline))
            {
                throw new InvalidOperationException("A Universal Render Pipeline asset was not assigned.");
            }

            if (!pipeline.supportsSoftShadows || pipeline.shadowDistance < 30f)
            {
                throw new InvalidOperationException("The URP asset must support soft shadows within the island view range.");
            }

            if (PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone) != 1)
            {
                throw new InvalidOperationException("The macOS standalone architecture is not ARM64.");
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
            {
                throw new InvalidOperationException("The macOS standalone scripting backend is not IL2CPP.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            {
                throw new InvalidOperationException("The active build target is not macOS.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                throw new InvalidOperationException("The startup scene was not created.");
            }

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath && scene.enabled)
                {
                    return;
                }
            }

            throw new InvalidOperationException("The startup scene is not enabled in the build settings.");
        }

        private static FossilCatalog GetOrCreateFossilCatalog()
        {
            EnsureFolder(AssetRoot + "/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<FossilCatalog>(FossilCatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<FossilCatalog>();
            catalog.SetDefaultDefinitions();
            AssetDatabase.CreateAsset(catalog, FossilCatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static ResearchCatalog GetOrCreateResearchCatalog()
        {
            EnsureFolder(AssetRoot + "/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<ResearchCatalog>(ResearchCatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<ResearchCatalog>();
            catalog.SetDefaultMilestones();
            AssetDatabase.CreateAsset(catalog, ResearchCatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static DinosaurCatalog GetOrCreateDinosaurCatalog()
        {
            EnsureFolder(AssetRoot + "/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<DinosaurCatalog>(DinosaurCatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<DinosaurCatalog>();
            catalog.SetDefaultSpecies();
            AssetDatabase.CreateAsset(catalog, DinosaurCatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
