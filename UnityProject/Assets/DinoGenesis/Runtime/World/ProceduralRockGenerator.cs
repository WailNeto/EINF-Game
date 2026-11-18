using System;
using System.Collections.Generic;
using UnityEngine;

namespace DinoGenesis.World
{
    internal static class ProceduralRockGenerator
    {
        private const int VariantCount = 6;

        internal static Mesh[] BuildVariants()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-1, 1.618034f, 0).normalized,
                new Vector3(1, 1.618034f, 0).normalized,
                new Vector3(-1, -1.618034f, 0).normalized,
                new Vector3(1, -1.618034f, 0).normalized,
                new Vector3(0, -1, 1.618034f).normalized,
                new Vector3(0, 1, 1.618034f).normalized,
                new Vector3(0, -1, -1.618034f).normalized,
                new Vector3(0, 1, -1.618034f).normalized,
                new Vector3(1.618034f, 0, -1).normalized,
                new Vector3(1.618034f, 0, 1).normalized,
                new Vector3(-1.618034f, 0, -1).normalized,
                new Vector3(-1.618034f, 0, 1).normalized
            };
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            Subdivide(vertices, faces);

            var meshes = new Mesh[VariantCount];
            for (var variant = 0; variant < meshes.Length; variant++)
            {
                meshes[variant] = BuildFacetedVariant(vertices, faces, variant);
            }

            return meshes;
        }

        private static void Subdivide(List<Vector3> vertices, List<int> faces)
        {
            var midpointCache = new Dictionary<long, int>();
            var subdivided = new List<int>(faces.Count * 4);
            for (var i = 0; i < faces.Count; i += 3)
            {
                var a = GetMidpoint(faces[i], faces[i + 1], vertices, midpointCache);
                var b = GetMidpoint(faces[i + 1], faces[i + 2], vertices, midpointCache);
                var c = GetMidpoint(faces[i + 2], faces[i], vertices, midpointCache);
                subdivided.AddRange(new[] { faces[i], a, c, faces[i + 1], b, a, faces[i + 2], c, b, a, b, c });
            }

            faces.Clear();
            faces.AddRange(subdivided);
        }

        private static int GetMidpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            var low = Math.Min(a, b);
            var high = Math.Max(a, b);
            var key = ((long)low << 32) + (uint)high;
            if (cache.TryGetValue(key, out var index))
            {
                return index;
            }

            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]).normalized);
            cache.Add(key, index);
            return index;
        }

        private static Mesh BuildFacetedVariant(List<Vector3> source, List<int> faces, int variant)
        {
            var vertices = new Vector3[faces.Count];
            var uv = new Vector2[faces.Count];
            var triangles = new int[faces.Count];
            var heightScale = new[] { 0.67f, 0.52f, 0.82f, 0.61f, 0.73f, 0.57f }[variant];
            var roughness = new[] { 0.16f, 0.12f, 0.21f, 0.25f, 0.14f, 0.19f }[variant];
            for (var i = 0; i < faces.Count; i++)
            {
                var sourceVertex = source[faces[i]];
                var noise = Mathf.Sin(sourceVertex.x * 17f + sourceVertex.y * 31f + variant * 7.13f) *
                            Mathf.Cos(sourceVertex.z * 23f - sourceVertex.y * 13f + variant * 3.71f);
                var radialScale = 1f + noise * roughness;
                var point = sourceVertex * radialScale;
                point.y *= heightScale;
                point.x *= variant % 2 == 0 ? 1f : 0.86f;
                point.z *= variant % 3 == 0 ? 0.82f : 1f;
                vertices[i] = point;
                uv[i] = new Vector2(0.5f + point.x * 0.5f, 0.5f + point.z * 0.5f);
                triangles[i] = i;
            }

            var mesh = new Mesh
            {
                name = "Faceted Basalt Variant " + (variant + 1),
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
