using DinoGenesis.World;
using UnityEngine;

namespace DinoGenesis.Dinosaurs
{
    [DisallowMultipleComponent]
    public sealed class WildBird : MonoBehaviour
    {
        private IslandWorld world;
        private Transform body;
        private Vector3 homePosition;
        private Vector3 destination;
        private System.Random random;
        private Material plumageMaterial;
        private Material detailMaterial;
        private Material eyeMaterial;
        private Material irisMaterial;
        private Material eyeWhiteMaterial;
        private Mesh featherMesh;
        private float destinationTimer;
        private bool captured;

        public int Index { get; private set; }
        public bool IsCaptured => captured;

        public void Initialize(IslandWorld islandWorld, int seed, int index, Material birdMaterial)
        {
            world = islandWorld;
            random = new System.Random(seed);
            Index = index;
            homePosition = transform.position;
            destination = homePosition;
            var variant = index % 6;
            plumageMaterial = new Material(birdMaterial)
            {
                name = "Wild Bird Plumage " + (variant + 1)
            };
            plumageMaterial.color = Color.Lerp(
                birdMaterial.color,
                new[]
                {
                    new Color(0.78f, 0.62f, 0.36f),
                    new Color(0.38f, 0.47f, 0.34f),
                    new Color(0.71f, 0.39f, 0.24f),
                    new Color(0.44f, 0.47f, 0.51f),
                    new Color(0.72f, 0.67f, 0.49f),
                    new Color(0.35f, 0.32f, 0.28f)
                }[variant],
                0.58f);
            detailMaterial = new Material(birdMaterial)
            {
                name = "Wild Bird Beak and Feet " + (variant + 1),
                color = new Color(0.82f, 0.56f + (variant % 2) * 0.12f, 0.22f)
            };
            eyeMaterial = new Material(birdMaterial)
            {
                name = "Wild Bird Eyes",
                color = new Color(0.035f, 0.028f, 0.022f)
            };
            irisMaterial = new Material(birdMaterial)
            {
                name = "Wild Bird Iris",
                color = new Color(0.88f, 0.55f, 0.1f)
            };
            eyeWhiteMaterial = new Material(birdMaterial)
            {
                name = "Wild Bird Eye Ring",
                color = new Color(0.88f, 0.82f, 0.66f)
            };
            featherMesh = BuildFeatherMesh();

            var bodyLength = new[] { 0.78f, 0.9f, 0.7f, 0.84f, 0.75f, 0.98f }[variant];
            body = CreatePart("Body", PrimitiveType.Sphere, plumageMaterial,
                new Vector3(0f, 0.3f, 0f), new Vector3(0.52f, 0.43f, bodyLength));
            CreatePart("Breast", PrimitiveType.Sphere, plumageMaterial,
                new Vector3(0f, 0.24f, 0.31f), new Vector3(0.39f, 0.32f, 0.42f));
            CreatePart("Head", PrimitiveType.Sphere, plumageMaterial,
                new Vector3(0f, 0.6f, 0.48f), new Vector3(0.34f, 0.34f, 0.34f));
            CreatePart("Neck", PrimitiveType.Sphere, plumageMaterial,
                new Vector3(0f, 0.42f, 0.34f), new Vector3(0.28f, 0.3f, 0.34f));
            CreatePart("Beak", PrimitiveType.Cube, detailMaterial,
                new Vector3(0f, 0.54f, 0.76f), new Vector3(0.12f, 0.1f, 0.25f));

            for (var side = -1; side <= 1; side += 2)
            {
                CreatePart(side < 0 ? "Left Eye Ring" : "Right Eye Ring", PrimitiveType.Sphere, eyeWhiteMaterial,
                    new Vector3(side * 0.17f, 0.65f, 0.635f), new Vector3(0.09f, 0.095f, 0.06f));
                CreatePart(side < 0 ? "Left Eye" : "Right Eye", PrimitiveType.Sphere, irisMaterial,
                    new Vector3(side * 0.17f, 0.65f, 0.68f), new Vector3(0.056f, 0.062f, 0.035f));
                CreatePart(side < 0 ? "Left Pupil" : "Right Pupil", PrimitiveType.Sphere, eyeMaterial,
                    new Vector3(side * 0.17f, 0.65f, 0.709f), new Vector3(0.027f, 0.034f, 0.02f));
                CreatePart(side < 0 ? "Left Eye Glint" : "Right Eye Glint", PrimitiveType.Sphere, eyeWhiteMaterial,
                    new Vector3(side * 0.17f - 0.012f, 0.668f, 0.725f), new Vector3(0.012f, 0.012f, 0.008f));
                CreatePart(side < 0 ? "Left Wing" : "Right Wing", PrimitiveType.Sphere, plumageMaterial,
                    new Vector3(side * 0.3f, 0.37f, -0.02f), new Vector3(0.18f, 0.2f, 0.49f));
                for (var feather = 0; feather < 9; feather++)
                {
                    var spread = feather - 4;
                    var featherPart = CreateFeatherPart(
                        (side < 0 ? "Left" : "Right") + " Wing Feather " + (feather + 1),
                        plumageMaterial,
                        new Vector3(side * (0.31f + feather * 0.022f), 0.32f - Mathf.Abs(spread) * 0.009f, -0.12f - feather * 0.045f),
                        Quaternion.Euler(18f + Mathf.Abs(spread) * 3f, side * spread * 3f, side * (58f + spread * 4f)),
                        new Vector3(0.58f, 0.58f + (4 - Mathf.Abs(spread)) * 0.055f, 0.62f));
                }

                CreatePart(side < 0 ? "Left Leg" : "Right Leg", PrimitiveType.Capsule, detailMaterial,
                    new Vector3(side * 0.17f, 0.075f, 0.08f), new Vector3(0.085f, 0.17f, 0.085f));
                CreatePart(side < 0 ? "Left Foot" : "Right Foot", PrimitiveType.Cube, detailMaterial,
                    new Vector3(side * 0.17f, 0.025f, 0.14f), new Vector3(0.2f, 0.045f, 0.18f));
            }

            for (var feather = -3; feather <= 3; feather++)
            {
                CreateFeatherPart(
                    "Tail Feather " + (feather + 4),
                    plumageMaterial,
                    new Vector3(feather * 0.075f, 0.31f - Mathf.Abs(feather) * 0.015f, -0.52f),
                    Quaternion.Euler(0f, feather * 4f, feather * -9f),
                    new Vector3(0.68f, 0.8f - Mathf.Abs(feather) * 0.04f, 0.7f));
            }

            for (var row = 0; row < 3; row++)
            {
                for (var feather = -2; feather <= 2; feather++)
                {
                    var side = feather < 0 ? -1 : 1;
                    CreateFeatherPart(
                        "Body Feather " + row + " " + feather,
                        plumageMaterial,
                        new Vector3(feather * 0.075f, 0.2f + row * 0.065f, 0.3f - row * 0.14f),
                        Quaternion.Euler(18f, feather * 7f, -side * (12f + row * 7f)),
                        new Vector3(0.42f, 0.42f, 0.45f));
                }
            }

            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.35f, 0.1f);
            collider.radius = 0.42f;
            collider.height = 1.05f;
            collider.direction = 1;
        }

        public bool TryCapture()
        {
            if (captured)
            {
                return false;
            }

            captured = true;
            gameObject.SetActive(false);
            return true;
        }

        private void Update()
        {
            if (captured || world == null || random == null)
            {
                return;
            }

            destinationTimer -= Time.deltaTime;
            var offset = destination - transform.position;
            offset.y = 0f;
            if (destinationTimer <= 0f || offset.sqrMagnitude < 0.3f)
            {
                var angle = random.NextDouble() * System.Math.PI * 2.0;
                var radius = 1.5f + random.NextDouble() * 5.5f;
                var x = homePosition.x + (float)System.Math.Cos(angle) * (float)radius;
                var z = homePosition.z + (float)System.Math.Sin(angle) * (float)radius;
                destination = new Vector3(x, world.SampleHeight(x, z), z);
                destinationTimer = 1.5f + (float)random.NextDouble() * 2.5f;
            }

            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                var step = Mathf.Min(1.15f * Time.deltaTime, direction.magnitude);
                transform.position += direction.normalized * step;
                transform.position = new Vector3(
                    transform.position.x,
                    world.SampleHeight(transform.position.x, transform.position.z),
                    transform.position.z);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up),
                    Time.deltaTime * 4f);
            }

            if (body != null)
            {
                var bob = Mathf.Sin(Time.time * 6f + GetInstanceID()) * 0.025f;
                body.localPosition = new Vector3(0f, 0.28f + bob, 0f);
            }
        }

        private void OnDestroy()
        {
            DestroyOwnedMaterial(plumageMaterial);
            DestroyOwnedMaterial(detailMaterial);
            DestroyOwnedMaterial(eyeMaterial);
            DestroyOwnedMaterial(irisMaterial);
            DestroyOwnedMaterial(eyeWhiteMaterial);
            if (featherMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(featherMesh);
                }
                else
                {
                    DestroyImmediate(featherMesh);
                }
            }
        }

        private Transform CreateFeatherPart(
            string partName,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.AddComponent<MeshFilter>().sharedMesh = featherMesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Mesh BuildFeatherMesh()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-0.2f, 0.28f, 0.015f),
                new Vector3(0.2f, 0.28f, 0.015f),
                new Vector3(-0.16f, 0.62f, 0.025f),
                new Vector3(0.16f, 0.62f, 0.025f),
                new Vector3(0f, 1f, 0.035f)
            };
            var front = new[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 };
            var doubleSided = new Vector3[front.Length * 2];
            var triangles = new int[front.Length * 2];
            for (var i = 0; i < front.Length; i += 3)
            {
                for (var vertex = 0; vertex < 3; vertex++)
                {
                    var index = i + vertex;
                    doubleSided[index] = vertices[front[index]];
                    doubleSided[front.Length + index] = vertices[front[index]];
                    triangles[index] = index;
                    triangles[front.Length + index] =
                        front.Length + i + (vertex == 0 ? 0 : 3 - vertex);
                }
            }

            var uv = new Vector2[doubleSided.Length];
            for (var i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2(doubleSided[i].x + 0.5f, doubleSided[i].y);
            }

            var mesh = new Mesh
            {
                name = "Wild Bird Feather",
                vertices = doubleSided,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyOwnedMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private Transform CreatePart(string partName, PrimitiveType shape, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            return part.transform;
        }
    }
}
