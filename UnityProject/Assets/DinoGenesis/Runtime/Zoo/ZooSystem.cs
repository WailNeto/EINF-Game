using System;
using DinoGenesis.Dinosaurs;
using DinoGenesis.Economy;
using DinoGenesis.Persistence;
using DinoGenesis.World;
using UnityEngine;

namespace DinoGenesis.Zoo
{
    [DisallowMultipleComponent]
    public sealed class ZooSystem : MonoBehaviour
    {
        private const int EnclosureCount = 3;
        private const float VisitorIntervalSeconds = 12f;
        private const int AdmissionPrice = 2;
        private static readonly int[] EnclosurePrices = { 0, 250, 600 };

        [SerializeField] private bool[] unlockedEnclosures = { true, false, false };
        [SerializeField] private int visitorsServed;
        [SerializeField] private int totalRevenue;

        private DinosaurSystem dinosaurs;
        private EconomySystem economy;
        private IslandWorld world;
        private Transform enclosureRoot;
        private Material fenceMaterial;
        private float visitorTimer;

        public int VisitorsServed => visitorsServed;
        public int TotalRevenue => totalRevenue;
        public int UnlockedEnclosureCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < unlockedEnclosures.Length; i++)
                {
                    if (unlockedEnclosures[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int NextEnclosurePrice
        {
            get
            {
                var index = GetNextLockedEnclosureIndex();
                return index < 0 ? 0 : EnclosurePrices[index];
            }
        }

        public ZooSaveData CaptureSaveData()
        {
            return new ZooSaveData
            {
                unlockedEnclosures = (bool[])unlockedEnclosures.Clone(),
                visitorsServed = visitorsServed,
                totalRevenue = totalRevenue
            };
        }

        public void RestoreSaveData(ZooSaveData data)
        {
            if (dinosaurs == null || economy == null || world == null)
            {
                throw new InvalidOperationException("Bind the zoo systems before restoring zoo data.");
            }

            if (data == null || data.unlockedEnclosures == null ||
                data.unlockedEnclosures.Length != EnclosureCount ||
                !data.unlockedEnclosures[0] || data.visitorsServed < 0 || data.totalRevenue < 0)
            {
                throw new ArgumentException("The saved zoo data is invalid.", nameof(data));
            }

            for (var i = 1; i < data.unlockedEnclosures.Length; i++)
            {
                if (data.unlockedEnclosures[i] && !data.unlockedEnclosures[i - 1])
                {
                    throw new ArgumentException("Saved zoo enclosures must be unlocked in order.", nameof(data));
                }
            }

            var assignedEnclosures = new bool[EnclosureCount];
            for (var i = 0; i < dinosaurs.Dinosaurs.Count; i++)
            {
                var dinosaur = dinosaurs.Dinosaurs[i];
                if (dinosaur.EnclosureIndex < 0)
                {
                    continue;
                }

                if (dinosaur.EnclosureIndex >= EnclosureCount ||
                    !data.unlockedEnclosures[dinosaur.EnclosureIndex] ||
                    assignedEnclosures[dinosaur.EnclosureIndex] ||
                    dinosaur.LifeStage != DinosaurLifeStage.Adult)
                {
                    throw new ArgumentException("Saved dinosaur assignments do not match unlocked zoo enclosures.", nameof(data));
                }

                assignedEnclosures[dinosaur.EnclosureIndex] = true;
            }

            var ledgerRevenue = 0;
            foreach (var transaction in economy.Transactions)
            {
                if (transaction.Type == EconomyTransactionType.ZooAdmissionRevenue)
                {
                    ledgerRevenue = checked(ledgerRevenue + transaction.Amount);
                }
            }

            if (ledgerRevenue != data.totalRevenue ||
                checked(data.visitorsServed * AdmissionPrice) != data.totalRevenue)
            {
                throw new ArgumentException("Saved zoo visitors and revenue do not match the economy transaction ledger.", nameof(data));
            }

            unlockedEnclosures = (bool[])data.unlockedEnclosures.Clone();
            visitorsServed = data.visitorsServed;
            totalRevenue = data.totalRevenue;
            visitorTimer = 0f;
            Bind(dinosaurs, economy, world);
        }

        public void Bind(DinosaurSystem dinosaurSystem, EconomySystem economySystem, IslandWorld islandWorld)
        {
            if (dinosaurSystem == null || economySystem == null || islandWorld == null)
            {
                throw new ArgumentException("The zoo requires dinosaur, economy, and island systems.");
            }

            dinosaurs = dinosaurSystem;
            economy = economySystem;
            world = islandWorld;
            EnsureEnclosureState();
            RebuildEnclosures();

            for (var i = 0; i < dinosaurs.Dinosaurs.Count; i++)
            {
                var dinosaur = dinosaurs.Dinosaurs[i];
                if (dinosaur.EnclosureIndex >= 0 && dinosaur.EnclosureIndex < unlockedEnclosures.Length &&
                    unlockedEnclosures[dinosaur.EnclosureIndex])
                {
                    dinosaur.SetEnclosure(
                        dinosaur.EnclosureIndex,
                        world.GetZooEnclosurePosition(dinosaur.EnclosureIndex),
                        5.5f);
                }
            }
        }

        public bool IsEnclosureUnlocked(int enclosureIndex)
        {
            return enclosureIndex >= 0 &&
                   enclosureIndex < unlockedEnclosures.Length &&
                   unlockedEnclosures[enclosureIndex];
        }

        public string GetAssignedDinosaurId(int enclosureIndex)
        {
            if (!IsEnclosureUnlocked(enclosureIndex) || dinosaurs == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < dinosaurs.Dinosaurs.Count; i++)
            {
                if (dinosaurs.Dinosaurs[i].EnclosureIndex == enclosureIndex)
                {
                    return dinosaurs.Dinosaurs[i].Id;
                }
            }

            return string.Empty;
        }

        public bool TryAssignDinosaur(string dinosaurId, int enclosureIndex, out string reason)
        {
            if (dinosaurs == null || world == null || !IsEnclosureUnlocked(enclosureIndex))
            {
                reason = "That enclosure is not available.";
                return false;
            }

            if (!string.IsNullOrEmpty(GetAssignedDinosaurId(enclosureIndex)))
            {
                reason = "That enclosure already has a dinosaur.";
                return false;
            }

            if (!dinosaurs.TryGetDinosaur(dinosaurId, out var dinosaur))
            {
                reason = "That dinosaur could not be found.";
                return false;
            }

            if (dinosaur.LifeStage != DinosaurLifeStage.Adult)
            {
                reason = "Only fully grown dinosaurs can be assigned to zoo enclosures.";
                return false;
            }

            if (dinosaur.EnclosureIndex >= 0)
            {
                reason = "That dinosaur is already assigned to an enclosure.";
                return false;
            }

            dinosaur.SetEnclosure(
                enclosureIndex,
                world.GetZooEnclosurePosition(enclosureIndex),
                5.5f);
            reason = dinosaur.SpeciesId + " assigned to enclosure " + (enclosureIndex + 1) + ".";
            return true;
        }

        public bool TryUnassignDinosaur(string dinosaurId, out string reason)
        {
            if (dinosaurs == null || world == null ||
                !dinosaurs.TryGetDinosaur(dinosaurId, out var dinosaur) ||
                dinosaur.EnclosureIndex < 0)
            {
                reason = "That dinosaur is not in an enclosure.";
                return false;
            }

            var oldIndex = dinosaur.EnclosureIndex;
            var x = -15f;
            var z = 30f;
            dinosaur.SetEnclosure(-1, new Vector3(x, world.SampleHeight(x, z), z), 3f);
            reason = "Dinosaur removed from enclosure " + (oldIndex + 1) + ".";
            return true;
        }

        public bool TryUnlockNextEnclosure(out string reason)
        {
            if (economy == null || world == null)
            {
                reason = "The zoo is not ready.";
                return false;
            }

            var index = GetNextLockedEnclosureIndex();
            if (index < 0)
            {
                reason = "All available enclosures are already unlocked.";
                return false;
            }

            if (!economy.TryPurchaseZooEnclosure(index + 1, EnclosurePrices[index], out reason))
            {
                return false;
            }

            unlockedEnclosures[index] = true;
            BuildEnclosure(index);
            return true;
        }

        public bool SimulateVisitors(int visitorCount, out int coinsEarned, out string reason)
        {
            coinsEarned = 0;
            if (visitorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(visitorCount), "Visitor count cannot be negative.");
            }

            if (visitorCount == 0 || CountOccupiedEnclosures() == 0)
            {
                reason = "The zoo needs visitors and at least one dinosaur on display.";
                return false;
            }

            var revenue = checked(visitorCount * AdmissionPrice);
            var newVisitorCount = checked(visitorsServed + visitorCount);
            var newTotalRevenue = checked(totalRevenue + revenue);
            if (!economy.TryCreditZooRevenue(revenue, out reason))
            {
                return false;
            }

            visitorsServed = newVisitorCount;
            totalRevenue = newTotalRevenue;
            coinsEarned = revenue;
            return true;
        }

        private void Update()
        {
            if (dinosaurs == null || economy == null || CountOccupiedEnclosures() == 0)
            {
                return;
            }

            visitorTimer += Time.deltaTime;
            if (visitorTimer < VisitorIntervalSeconds)
            {
                return;
            }

            visitorTimer -= VisitorIntervalSeconds;
            var visitors = UnityEngine.Random.Range(1, 4) * CountOccupiedEnclosures();
            if (!SimulateVisitors(visitors, out _, out var reason))
            {
                Debug.LogError("Zoo admission revenue could not be recorded: " + reason, this);
            }
        }

        private int CountOccupiedEnclosures()
        {
            var count = 0;
            for (var i = 0; i < unlockedEnclosures.Length; i++)
            {
                if (!string.IsNullOrEmpty(GetAssignedDinosaurId(i)))
                {
                    count++;
                }
            }

            return count;
        }

        private int GetNextLockedEnclosureIndex()
        {
            for (var i = 0; i < unlockedEnclosures.Length; i++)
            {
                if (!unlockedEnclosures[i])
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureEnclosureState()
        {
            if (unlockedEnclosures == null || unlockedEnclosures.Length != EnclosureCount)
            {
                Array.Resize(ref unlockedEnclosures, EnclosureCount);
                unlockedEnclosures[0] = true;
            }
        }

        private void RebuildEnclosures()
        {
            if (enclosureRoot != null)
            {
                DestroyOwnedObject(enclosureRoot.gameObject);
            }

            enclosureRoot = new GameObject("Unlocked Zoo Enclosures").transform;
            enclosureRoot.SetParent(transform, false);
            for (var i = 1; i < unlockedEnclosures.Length; i++)
            {
                if (unlockedEnclosures[i])
                {
                    BuildEnclosure(i);
                }
            }
        }

        private void BuildEnclosure(int index)
        {
            if (enclosureRoot == null)
            {
                return;
            }

            if (fenceMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No compatible Unity material shader was found.");
                }

                fenceMaterial = new Material(shader)
                {
                    name = "Zoo Fence",
                    color = new Color(0.48f, 0.37f, 0.25f)
                };
            }

            var center = world.GetZooEnclosurePosition(index);
            var pen = new GameObject("Zoo Enclosure " + (index + 1));
            pen.transform.SetParent(enclosureRoot, false);
            pen.transform.position = center;
            const float halfWidth = 9f;
            const float halfDepth = 7f;
            const float fenceHeight = 1.6f;
            CreateFencePart(pen.transform, "North Fence", new Vector3(0f, fenceHeight * 0.5f, halfDepth),
                new Vector3(halfWidth * 2f, fenceHeight, 0.25f));
            CreateFencePart(pen.transform, "South Fence", new Vector3(0f, fenceHeight * 0.5f, -halfDepth),
                new Vector3(halfWidth * 2f, fenceHeight, 0.25f));
            CreateFencePart(pen.transform, "East Fence", new Vector3(halfWidth, fenceHeight * 0.5f, 0f),
                new Vector3(0.25f, fenceHeight, halfDepth * 2f));
            CreateFencePart(pen.transform, "West Fence", new Vector3(-halfWidth, fenceHeight * 0.5f, 0f),
                new Vector3(0.25f, fenceHeight, halfDepth * 2f));
        }

        private void CreateFencePart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = fenceMaterial;
        }

        private void OnDestroy()
        {
            if (fenceMaterial != null)
            {
                DestroyOwnedObject(fenceMaterial);
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
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
