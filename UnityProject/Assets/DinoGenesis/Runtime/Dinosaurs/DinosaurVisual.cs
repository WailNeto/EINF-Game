using DinoGenesis.World;
using UnityEngine;

namespace DinoGenesis.Dinosaurs
{
    [DisallowMultipleComponent]
    public sealed class DinosaurVisual : MonoBehaviour
    {
        private IslandWorld world;
        private Transform model;
        private Vector3 homePosition;
        private float movementTimer;
        private Vector3 destination;
        private Transform egg;
        private Material eggMaterial;
        private Material bodyMaterial;
        private float roamingRadius = 3f;

        public void Initialize(DinosaurSpeciesDefinition species, IslandWorld islandWorld, DinosaurLifeStage stage)
        {
            world = islandWorld;
            homePosition = transform.position;
            destination = homePosition;

            eggMaterial = CreateMaterial("Dinosaur Egg", new Color(0.83f, 0.79f, 0.59f));
            bodyMaterial = CreateMaterial(species.DisplayName, species.BodyColor);
            egg = CreatePart("Egg", PrimitiveType.Sphere, eggMaterial, Vector3.up * 0.45f,
                new Vector3(0.55f, 0.72f, 0.55f));
            model = new GameObject("Dinosaur Model").transform;
            model.SetParent(transform, false);
            model.gameObject.SetActive(false);

            CreatePart(model, "Body", PrimitiveType.Sphere, bodyMaterial, new Vector3(0f, 0.55f, 0f),
                new Vector3(1.05f, 0.7f, 1.55f));
            CreatePart(model, "Head", PrimitiveType.Sphere, bodyMaterial, new Vector3(0f, 0.85f, 0.92f),
                new Vector3(0.55f, 0.5f, 0.64f));
            CreatePart(model, "Tail", PrimitiveType.Cube, bodyMaterial, new Vector3(0f, 0.55f, -1.05f),
                new Vector3(0.24f, 0.24f, 1.25f));
            CreatePart(model, "Left Leg", PrimitiveType.Cylinder, bodyMaterial, new Vector3(-0.35f, 0.18f, 0.32f),
                new Vector3(0.15f, 0.3f, 0.15f));
            CreatePart(model, "Right Leg", PrimitiveType.Cylinder, bodyMaterial, new Vector3(0.35f, 0.18f, 0.32f),
                new Vector3(0.15f, 0.3f, 0.15f));

            SetStage(stage);
        }

        public void SetStage(DinosaurLifeStage stage)
        {
            if (model == null)
            {
                return;
            }

            var isEgg = stage == DinosaurLifeStage.Egg;
            if (egg != null)
            {
                egg.gameObject.SetActive(isEgg);
            }

            model.gameObject.SetActive(!isEgg);
            if (!isEgg)
            {
                var scale = stage == DinosaurLifeStage.Baby ? 0.55f : 1f;
                model.localScale = Vector3.one * scale;
            }
        }

        public void SetRoamingArea(Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(radius), "The roaming radius must be positive.");
            }

            homePosition = center;
            destination = center;
            roamingRadius = radius;
            transform.position = center;
        }

        private void Update()
        {
            if (world == null || model == null || !model.gameObject.activeSelf)
            {
                return;
            }

            movementTimer -= Time.deltaTime;
            var direction = destination - transform.position;
            direction.y = 0f;
            if (movementTimer <= 0f || direction.sqrMagnitude < 0.2f)
            {
                var angle = Random.value * Mathf.PI * 2f;
                var radius = Random.Range(0.5f, roamingRadius);
                var x = homePosition.x + Mathf.Cos(angle) * radius;
                var z = homePosition.z + Mathf.Sin(angle) * radius;
                destination = new Vector3(x, world.SampleHeight(x, z), z);
                movementTimer = Random.Range(2f, 5f);
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.position += direction.normalized * Mathf.Min(0.55f * Time.deltaTime, direction.magnitude);
                transform.position = new Vector3(
                    transform.position.x,
                    world.SampleHeight(transform.position.x, transform.position.z),
                    transform.position.z);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up),
                    Time.deltaTime * 2f);
            }
        }

        private Transform CreatePart(string partName, PrimitiveType shape, Material material, Vector3 localPosition, Vector3 localScale)
        {
            return CreatePart(transform, partName, shape, material, localPosition, localScale);
        }

        private static Transform CreatePart(
            Transform parent,
            string partName,
            PrimitiveType shape,
            Material material,
            Vector3 localPosition,
            Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
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

        private static Material CreateMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new System.InvalidOperationException("No compatible Unity material shader was found.");
            }

            return new Material(shader) { name = materialName, color = color };
        }

        private void OnDestroy()
        {
            if (eggMaterial != null)
            {
                DestroyOwnedMaterial(eggMaterial);
            }

            if (bodyMaterial != null)
            {
                DestroyOwnedMaterial(bodyMaterial);
            }
        }

        private static void DestroyOwnedMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
