using System;
using UnityEngine;

namespace DinoGenesis.World
{
    public static class ProceduralTerrainSurface
    {
        public const int DefaultResolution = 256;

        public static void Generate(
            int seed,
            int resolution,
            float halfExtent,
            float islandRadius,
            Vector2 noiseOffset,
            Func<float, float, float> sampleHeight,
            out Texture2D albedo,
            out Texture2D normal,
            out int signature)
        {
            if (resolution < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), "Terrain texture resolution must be at least 2.");
            }

            if (sampleHeight == null)
            {
                throw new ArgumentNullException(nameof(sampleHeight));
            }

            var random = new System.Random(unchecked(seed * 7919 + 173));
            var offsetX = (float)random.NextDouble() * 10000f;
            var offsetZ = (float)random.NextDouble() * 10000f;
            var pixelCount = checked(resolution * resolution);
            var heights = new float[pixelCount];
            var colors = new Color32[pixelCount];
            var normals = new Color32[pixelCount];
            var spacing = 2f * halfExtent / (resolution - 1);
            var signatureValue = 23;

            for (var y = 0; y < resolution; y++)
            {
                var worldZ = Mathf.Lerp(-halfExtent, halfExtent, y / (float)(resolution - 1));
                for (var x = 0; x < resolution; x++)
                {
                    var worldX = Mathf.Lerp(-halfExtent, halfExtent, x / (float)(resolution - 1));
                    var index = y * resolution + x;
                    heights[index] = sampleHeight(worldX, worldZ);

                    var broad = Mathf.PerlinNoise(
                        (worldX + noiseOffset.x + offsetX) * 0.026f,
                        (worldZ + noiseOffset.y + offsetZ) * 0.026f);
                    var patch = Mathf.PerlinNoise(
                        (worldX + noiseOffset.x + offsetZ) * 0.092f,
                        (worldZ + noiseOffset.y + offsetX) * 0.092f);
                    var grain = Mathf.PerlinNoise(
                        (worldX + noiseOffset.y + offsetX) * 0.39f,
                        (worldZ + noiseOffset.x + offsetZ) * 0.39f);

                    var grass = Color.Lerp(
                        new Color(0.14f, 0.31f, 0.11f),
                        new Color(0.29f, 0.47f, 0.16f),
                        Mathf.Clamp01(broad * 0.78f + patch * 0.22f));
                    var earth = Color.Lerp(
                        new Color(0.25f, 0.22f, 0.16f),
                        new Color(0.43f, 0.36f, 0.25f),
                        Mathf.Clamp01(patch * 0.72f + grain * 0.28f));
                    var soilCoverage = Mathf.SmoothStep(0.48f, 0.73f, patch) *
                                       Mathf.Lerp(0.38f, 0.74f, broad);
                    var ground = Color.Lerp(grass, earth, soilCoverage);

                    var coastWarp = (broad - 0.5f) * 0.055f + (patch - 0.5f) * 0.02f;
                    var normalizedRadius = Mathf.Sqrt(worldX * worldX + worldZ * worldZ) / islandRadius + coastWarp;
                    var beachBand = Mathf.SmoothStep(0.75f, 0.82f, normalizedRadius) *
                                    (1f - Mathf.SmoothStep(0.88f, 0.96f, normalizedRadius));
                    var sand = Color.Lerp(
                        new Color(0.49f, 0.42f, 0.30f),
                        new Color(0.66f, 0.59f, 0.43f),
                        Mathf.Clamp01(patch * 0.66f + grain * 0.34f));
                    ground = Color.Lerp(ground, sand, beachBand);

                    if ((x % 32 == 0) && (y % 32 == 0))
                    {
                        signatureValue = unchecked(
                            signatureValue * 31 +
                            Mathf.RoundToInt(ground.r * 255f) * 3 +
                            Mathf.RoundToInt(ground.g * 255f) * 5 +
                            Mathf.RoundToInt(ground.b * 255f) * 7);
                    }

                    colors[index] = new Color32(
                        ToByte(Mathf.LinearToGammaSpace(ground.r)),
                        ToByte(Mathf.LinearToGammaSpace(ground.g)),
                        ToByte(Mathf.LinearToGammaSpace(ground.b)),
                        255);
                }
            }

            const float normalStrength = 0.65f;
            for (var y = 0; y < resolution; y++)
            {
                var previousY = Mathf.Max(0, y - 1);
                var nextY = Mathf.Min(resolution - 1, y + 1);
                for (var x = 0; x < resolution; x++)
                {
                    var previousX = Mathf.Max(0, x - 1);
                    var nextX = Mathf.Min(resolution - 1, x + 1);
                    var dx = (heights[y * resolution + nextX] - heights[y * resolution + previousX]) /
                             (Mathf.Max(1, nextX - previousX) * spacing);
                    var dz = (heights[nextY * resolution + x] - heights[previousY * resolution + x]) /
                             (Mathf.Max(1, nextY - previousY) * spacing);
                    var surfaceNormal = new Vector3(-dx * normalStrength, 1f, -dz * normalStrength).normalized;
                    var tangentX = ToByte(surfaceNormal.x * 0.5f + 0.5f);
                    var tangentY = ToByte(-surfaceNormal.z * 0.5f + 0.5f);
                    var tangentZ = ToByte(surfaceNormal.y * 0.5f + 0.5f);
                    normals[y * resolution + x] = new Color32(
                        tangentX,
                        tangentY,
                        tangentZ,
                        tangentX);
                }
            }

            albedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
            {
                name = "Seeded Island Ground Albedo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            albedo.SetPixels32(colors);
            albedo.Apply(true, false);

            normal = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = "Seeded Island Ground Normal",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            normal.SetPixels32(normals);
            normal.Apply(true, false);
            signature = signatureValue;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
