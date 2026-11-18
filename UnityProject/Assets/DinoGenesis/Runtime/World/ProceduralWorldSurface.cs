using System;
using UnityEngine;

namespace DinoGenesis.World
{
    public enum WorldSurfaceStyle
    {
        WeatheredWood,
        PaintedPlaster,
        WovenCanvas,
        DarkMetal,
        Gravel,
        VolcanicStone,
        CanopyLeaves,
        PineNeedles,
        FernLeaves
    }

    public static class ProceduralWorldSurface
    {
        public const int DefaultResolution = 128;
        private const float Tau = Mathf.PI * 2f;

        public static void Generate(
            WorldSurfaceStyle style,
            int seed,
            int resolution,
            out Texture2D albedo,
            out Texture2D normal)
        {
            if (resolution < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), "World surface resolution must be at least 8.");
            }

            var random = new System.Random(unchecked(seed * 397 + (int)style * 7919));
            var phaseX = (float)random.NextDouble() * Tau;
            var phaseY = (float)random.NextDouble() * Tau;
            var pixels = new Color32[checked(resolution * resolution)];
            var heights = new float[pixels.Length];
            var tint = GetPalette(style);

            for (var y = 0; y < resolution; y++)
            {
                var v = y / (float)resolution;
                for (var x = 0; x < resolution; x++)
                {
                    var u = x / (float)resolution;
                    var low = PeriodicNoise(u, v, 3, 5, phaseX, phaseY);
                    var fine = PeriodicNoise(u, v, 11, 13, phaseY, phaseX);
                    var grain = PeriodicNoise(u, v, 27, 29, phaseX * 0.7f, phaseY * 0.8f);
                    var pattern = EvaluatePattern(style, u, v, phaseX, phaseY, fine);
                    var height = Mathf.Clamp01(0.52f + (low - 0.5f) * 0.42f + pattern * 0.22f + (grain - 0.5f) * 0.1f);
                    var index = y * resolution + x;
                    heights[index] = height;

                    var variation = (low - 0.5f) * 0.23f + (fine - 0.5f) * 0.13f + pattern * 0.16f;
                    var color = tint * Mathf.Clamp(0.78f + variation, 0.58f, 1.24f);
                    pixels[index] = new Color32(ToByte(color.r), ToByte(color.g), ToByte(color.b), 255);
                }
            }

            var normals = new Color32[pixels.Length];
            const float normalStrength = 2.4f;
            for (var y = 0; y < resolution; y++)
            {
                var previousY = (y + resolution - 1) % resolution;
                var nextY = (y + 1) % resolution;
                for (var x = 0; x < resolution; x++)
                {
                    var previousX = (x + resolution - 1) % resolution;
                    var nextX = (x + 1) % resolution;
                    var dx = (heights[y * resolution + nextX] - heights[y * resolution + previousX]) * normalStrength;
                    var dy = (heights[nextY * resolution + x] - heights[previousY * resolution + x]) * normalStrength;
                    var surfaceNormal = new Vector3(-dx, -dy, 1f).normalized;
                    var tangentX = ToByte(surfaceNormal.x * 0.5f + 0.5f);
                    normals[y * resolution + x] = new Color32(
                        tangentX,
                        ToByte(surfaceNormal.y * 0.5f + 0.5f),
                        ToByte(surfaceNormal.z * 0.5f + 0.5f),
                        tangentX);
                }
            }

            albedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
            {
                name = style + " Procedural Albedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            albedo.SetPixels32(pixels);
            albedo.Apply(true, false);

            normal = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = style + " Procedural Normal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            normal.SetPixels32(normals);
            normal.Apply(true, false);
        }

        private static Color GetPalette(WorldSurfaceStyle style)
        {
            switch (style)
            {
                case WorldSurfaceStyle.WeatheredWood:
                    return new Color(0.43f, 0.31f, 0.20f);
                case WorldSurfaceStyle.PaintedPlaster:
                    return new Color(0.72f, 0.77f, 0.69f);
                case WorldSurfaceStyle.WovenCanvas:
                    return new Color(0.78f, 0.61f, 0.37f);
                case WorldSurfaceStyle.DarkMetal:
                    return new Color(0.20f, 0.29f, 0.29f);
                case WorldSurfaceStyle.Gravel:
                    return new Color(0.41f, 0.38f, 0.31f);
                case WorldSurfaceStyle.VolcanicStone:
                    return new Color(0.34f, 0.35f, 0.32f);
                case WorldSurfaceStyle.CanopyLeaves:
                case WorldSurfaceStyle.PineNeedles:
                case WorldSurfaceStyle.FernLeaves:
                    return new Color(0.78f, 0.81f, 0.72f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown world surface style.");
            }
        }

        private static float EvaluatePattern(
            WorldSurfaceStyle style,
            float u,
            float v,
            float phaseX,
            float phaseY,
            float fineNoise)
        {
            switch (style)
            {
                case WorldSurfaceStyle.WeatheredWood:
                {
                    var grain = Mathf.Sin((u * 18f + Mathf.Sin(v * Tau * 2f + phaseY) * 0.035f) * Tau + phaseX);
                    var boardSeam = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * Tau * 5f + phaseY)), 48f);
                    return grain * 0.19f - boardSeam * 0.48f + (fineNoise - 0.5f) * 0.18f;
                }
                case WorldSurfaceStyle.PaintedPlaster:
                {
                    var panelJoint = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(u * Tau * 3f + phaseX)), 72f) +
                                     Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * Tau * 4f + phaseY)), 72f);
                    return (fineNoise - 0.5f) * 0.28f - panelJoint * 0.19f;
                }
                case WorldSurfaceStyle.WovenCanvas:
                {
                    var warp = Mathf.Sin(u * Tau * 32f + phaseX);
                    var weft = Mathf.Sin(v * Tau * 32f + phaseY);
                    return warp * weft * 0.16f + (fineNoise - 0.5f) * 0.1f;
                }
                case WorldSurfaceStyle.DarkMetal:
                {
                    var seam = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * Tau * 4f + phaseY)), 90f);
                    var rib = Mathf.Sin(u * Tau * 16f + phaseX) * 0.035f;
                    return -seam * 0.36f + rib + (fineNoise - 0.5f) * 0.12f;
                }
                case WorldSurfaceStyle.Gravel:
                {
                    var pebbles = Mathf.Max(0f, Mathf.Sin(u * Tau * 17f + phaseX) *
                                                 Mathf.Sin(v * Tau * 19f + phaseY));
                    return (pebbles - 0.25f) * 0.25f + (fineNoise - 0.5f) * 0.2f;
                }
                case WorldSurfaceStyle.VolcanicStone:
                {
                    var stratum = Mathf.Sin((u * 3f + v * 2f + fineNoise * 0.18f) * Tau + phaseX);
                    var chips = Mathf.Sin(u * Tau * 37f + phaseX) *
                                Mathf.Sin(v * Tau * 31f + phaseY);
                    return stratum * 0.075f + (fineNoise - 0.5f) * 0.34f + chips * 0.12f;
                }
                case WorldSurfaceStyle.CanopyLeaves:
                case WorldSurfaceStyle.FernLeaves:
                {
                    var veinCurve = u + Mathf.Sin(v * Tau * 2f + phaseY) * 0.035f;
                    var veins = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(veinCurve * Tau * 14f + phaseX)), 16f);
                    var leafEdge = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * Tau * 5f + phaseY)), 28f);
                    return (veins - 0.16f) * 0.35f - leafEdge * 0.14f + (fineNoise - 0.5f) * 0.18f;
                }
                case WorldSurfaceStyle.PineNeedles:
                {
                    var needleRidges = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((u * 1.7f + v * 0.23f) * Tau * 22f + phaseX)), 12f);
                    return (needleRidges - 0.15f) * 0.26f + (fineNoise - 0.5f) * 0.16f;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown world surface style.");
            }
        }

        private static float PeriodicNoise(float u, float v, int frequencyX, int frequencyY, float phaseX, float phaseY)
        {
            var x = Mathf.Sin(u * Tau * frequencyX + phaseX);
            var y = Mathf.Sin(v * Tau * frequencyY + phaseY);
            var diagonal = Mathf.Sin((u * frequencyX + v * frequencyY) * Tau + phaseX - phaseY);
            return Mathf.Clamp01(0.5f + 0.2f * x + 0.2f * y + 0.1f * diagonal);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(value) * 255f), 0, 255);
        }
    }
}
