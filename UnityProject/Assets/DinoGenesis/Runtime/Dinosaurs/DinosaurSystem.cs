using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DinoGenesis.Persistence;
using DinoGenesis.Research;
using DinoGenesis.World;
using UnityEngine;

namespace DinoGenesis.Dinosaurs
{
    public enum DinosaurLifeStage
    {
        Egg,
        Baby,
        Adult
    }

    [Serializable]
    public sealed class DinosaurRecord
    {
        [SerializeField] private string id;
        [SerializeField] private string speciesId;
        [SerializeField] private float ageSeconds;
        [SerializeField] private int enclosureIndex = -1;

        [NonSerialized] private DinosaurSpeciesDefinition definition;
        [NonSerialized] private DinosaurVisual visual;

        public string Id => id;
        public string SpeciesId => speciesId;
        public int EnclosureIndex => enclosureIndex;
        public float AgeSeconds => ageSeconds;
        public Vector3 Position => visual != null ? visual.transform.position : Vector3.zero;
        public DinosaurLifeStage LifeStage { get; private set; }
        public float StageProgress { get; private set; }

        internal DinosaurRecord(
            string dinosaurId,
            DinosaurSpeciesDefinition species,
            DinosaurVisual dinosaurVisual)
        {
            id = dinosaurId;
            speciesId = species.Id;
            definition = species;
            visual = dinosaurVisual;
            LifeStage = DinosaurLifeStage.Egg;
        }

        internal void Advance(float seconds)
        {
            ageSeconds += seconds;
            var nextStage = ageSeconds < definition.IncubationSeconds
                ? DinosaurLifeStage.Egg
                : ageSeconds < definition.IncubationSeconds + definition.GrowthSeconds
                    ? DinosaurLifeStage.Baby
                    : DinosaurLifeStage.Adult;

            var elapsedInStage = nextStage == DinosaurLifeStage.Egg
                ? ageSeconds
                : nextStage == DinosaurLifeStage.Baby
                    ? ageSeconds - definition.IncubationSeconds
                    : ageSeconds - definition.IncubationSeconds - definition.GrowthSeconds;
            var duration = nextStage == DinosaurLifeStage.Egg
                ? definition.IncubationSeconds
                : nextStage == DinosaurLifeStage.Baby ? definition.GrowthSeconds : 1f;
            var progress = nextStage == DinosaurLifeStage.Adult ? 1f : Mathf.Clamp01(elapsedInStage / duration);

            if (LifeStage != nextStage)
            {
                LifeStage = nextStage;
                visual.SetStage(nextStage);
            }

            StageProgress = progress;
        }

        internal void SetEnclosure(int index, Vector3 enclosurePosition, float roamingRadius)
        {
            enclosureIndex = index;
            visual.SetRoamingArea(enclosurePosition, roamingRadius);
        }

        internal void RestoreEnclosureIndex(int index)
        {
            enclosureIndex = index;
        }

        internal void DestroyVisual()
        {
            if (visual == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(visual.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(visual.gameObject);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class DinosaurSystem : MonoBehaviour
    {
        private readonly List<DinosaurRecord> dinosaurs = new List<DinosaurRecord>();
        private readonly List<WildBird> birds = new List<WildBird>();
        private readonly List<int> capturedBirdIndices = new List<int>();
        private DinosaurCatalog catalog;
        private ResearchSystem research;
        private IslandWorld world;
        private int avianSamples;
        private int nextDinosaurId;

        public int AvianSamples => avianSamples;
        public IReadOnlyList<DinosaurRecord> Dinosaurs => dinosaurs;
        public IReadOnlyList<WildBird> WildBirds => birds;

        public bool TryGetDinosaur(string dinosaurId, out DinosaurRecord dinosaur)
        {
            for (var i = 0; i < dinosaurs.Count; i++)
            {
                if (dinosaurs[i].Id == dinosaurId)
                {
                    dinosaur = dinosaurs[i];
                    return true;
                }
            }

            dinosaur = null;
            return false;
        }

        public DinosaurSaveData CaptureSaveData()
        {
            var data = new DinosaurSaveData
            {
                avianSamples = avianSamples,
                capturedBirdIndices = capturedBirdIndices.ToArray(),
                records = new DinosaurRecordSaveData[dinosaurs.Count]
            };

            for (var i = 0; i < dinosaurs.Count; i++)
            {
                var dinosaur = dinosaurs[i];
                data.records[i] = new DinosaurRecordSaveData
                {
                    id = dinosaur.Id,
                    speciesId = dinosaur.SpeciesId,
                    ageSeconds = dinosaur.AgeSeconds,
                    enclosureIndex = dinosaur.EnclosureIndex
                };
            }

            return data;
        }

        public void RestoreSaveData(DinosaurSaveData data)
        {
            if (catalog == null || research == null || world == null)
            {
                throw new InvalidOperationException("Bind the dinosaur catalog, research, and world before restoring dinosaur data.");
            }

            if (data == null || data.avianSamples < 0 ||
                data.capturedBirdIndices == null || data.records == null)
            {
                throw new ArgumentException("The saved dinosaur data is invalid.", nameof(data));
            }

            var restoredBirdIndices = new HashSet<int>();
            foreach (var index in data.capturedBirdIndices)
            {
                if (index < 0 || index >= 10 || !restoredBirdIndices.Add(index))
                {
                    throw new ArgumentException("Saved captured bird indices must be unique and within the generated flock.", nameof(data));
                }

                var birdExists = false;
                for (var i = 0; i < birds.Count; i++)
                {
                    if (birds[i].Index == index)
                    {
                        birdExists = true;
                        break;
                    }
                }

                if (!birdExists)
                {
                    throw new InvalidDataException("A saved bird capture does not exist in the generated world.");
                }
            }

            var restoredRecords = new List<DinosaurRecord>(data.records.Length);
            var speciesDefinitions = new List<DinosaurSpeciesDefinition>(data.records.Length);
            var ids = new HashSet<string>();
            var highestId = 0;
            var consumedSamples = 0;
            foreach (var savedRecord in data.records)
            {
                if (savedRecord == null || string.IsNullOrWhiteSpace(savedRecord.id) ||
                    !ids.Add(savedRecord.id) ||
                    !catalog.TryGetSpecies(savedRecord.speciesId, out var species) ||
                    !research.IsSpeciesUnlocked(species.ResearchSpeciesId) ||
                    float.IsNaN(savedRecord.ageSeconds) || float.IsInfinity(savedRecord.ageSeconds) ||
                    savedRecord.ageSeconds < 0f || savedRecord.enclosureIndex < -1 || savedRecord.enclosureIndex > 2)
                {
                    throw new ArgumentException("The saved dinosaur list contains an invalid record.", nameof(data));
                }

                const string prefix = "dinosaur-";
                if (!savedRecord.id.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(
                        savedRecord.id.Substring(prefix.Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var numericId) ||
                    numericId <= 0)
                {
                    throw new ArgumentException("A saved dinosaur ID has an unsupported format.", nameof(data));
                }

                highestId = Math.Max(highestId, numericId);
                consumedSamples = checked(consumedSamples + species.BirdSamplesRequired);
                speciesDefinitions.Add(species);
            }

            if (checked(consumedSamples + data.avianSamples) > restoredBirdIndices.Count)
            {
                throw new ArgumentException("Saved dinosaur synthesis exceeds the number of captured bird samples.", nameof(data));
            }

            for (var i = 0; i < data.records.Length; i++)
            {
                var savedRecord = data.records[i];
                var species = speciesDefinitions[i];
                var labX = -15f;
                var labZ = 22f;
                var visualObject = new GameObject(species.DisplayName + " " + savedRecord.id);
                visualObject.transform.SetParent(transform, true);
                visualObject.transform.position = new Vector3(labX, world.SampleHeight(labX, labZ), 30f);
                var visual = visualObject.AddComponent<DinosaurVisual>();
                visual.Initialize(species, world, DinosaurLifeStage.Egg);
                var dinosaur = new DinosaurRecord(savedRecord.id, species, visual);
                dinosaur.Advance(savedRecord.ageSeconds);
                dinosaur.RestoreEnclosureIndex(savedRecord.enclosureIndex);
                restoredRecords.Add(dinosaur);
            }

            ClearDinosaurs();
            dinosaurs.AddRange(restoredRecords);
            avianSamples = data.avianSamples;
            nextDinosaurId = highestId;
            capturedBirdIndices.Clear();
            capturedBirdIndices.AddRange(data.capturedBirdIndices);
            ApplyCapturedBirds();
        }

        public void Bind(
            DinosaurCatalog dinosaurCatalog,
            ResearchSystem researchSystem,
            IslandWorld islandWorld,
            bool applyPreviouslyCapturedBirds = true)
        {
            if (dinosaurCatalog == null || !dinosaurCatalog.IsValid ||
                researchSystem == null || islandWorld == null)
            {
                throw new ArgumentException("Dinosaur systems require a valid species catalog, research system, and island world.");
            }

            catalog = dinosaurCatalog;
            research = researchSystem;
            world = islandWorld;
            birds.Clear();
            birds.AddRange(world.GetComponentsInChildren<WildBird>());
            if (applyPreviouslyCapturedBirds)
            {
                ApplyCapturedBirds();
            }
        }

        public bool TryGetTargetedBird(Camera playerCamera, float range, out WildBird bird)
        {
            bird = null;
            if (playerCamera == null ||
                !Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, range))
            {
                return false;
            }

            bird = hit.collider.GetComponentInParent<WildBird>();
            return bird != null && !bird.IsCaptured;
        }

        public bool TryCaptureBirdFromView(Camera playerCamera, float range, out string message)
        {
            message = string.Empty;
            if (!TryGetTargetedBird(playerCamera, range, out var bird))
            {
                return false;
            }

            return TryCaptureBird(bird, out message);
        }

        public bool TryCaptureBird(WildBird bird, out string message)
        {
            message = string.Empty;
            if (bird == null || !birds.Contains(bird))
            {
                return false;
            }

            if (!bird.TryCapture())
            {
                message = "This bird has already been captured.";
                return false;
            }

            avianSamples++;
            if (!capturedBirdIndices.Contains(bird.Index))
            {
                capturedBirdIndices.Add(bird.Index);
            }
            message = "Bird DNA sample collected. Samples: " + avianSamples + ".";
            return true;
        }

        public bool TryCreateEgg(string speciesId, out string reason)
        {
            if (catalog == null || research == null || world == null ||
                !catalog.TryGetSpecies(speciesId, out var species))
            {
                reason = "The selected dinosaur species is unavailable.";
                return false;
            }

            if (!research.IsSpeciesUnlocked(species.ResearchSpeciesId))
            {
                reason = "Research has not unlocked this dinosaur genome yet.";
                return false;
            }

            if (avianSamples < species.BirdSamplesRequired)
            {
                reason = "Capture " + species.BirdSamplesRequired + " wild bird DNA sample(s) first.";
                return false;
            }

            var labPosition = new Vector3(-15f, world.SampleHeight(-15f, 22f), 30f);
            var visualRoot = new GameObject(species.DisplayName + " " + (dinosaurs.Count + 1));
            visualRoot.transform.SetParent(transform, true);
            visualRoot.transform.position = labPosition;
            var visual = visualRoot.AddComponent<DinosaurVisual>();
            visual.Initialize(species, world, DinosaurLifeStage.Egg);

            avianSamples -= species.BirdSamplesRequired;
            var record = new DinosaurRecord(
                "dinosaur-" + (++nextDinosaurId).ToString("D4"),
                species,
                visual);
            dinosaurs.Add(record);
            reason = species.DisplayName + " egg synthesized. Incubation started.";
            return true;
        }

        public void AdvanceDevelopment(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Development time must be finite and non-negative.");
            }

            for (var i = 0; i < dinosaurs.Count; i++)
            {
                dinosaurs[i].Advance(seconds);
            }
        }

        private void Update()
        {
            AdvanceDevelopment(Time.deltaTime);
        }

        private void ApplyCapturedBirds()
        {
            var restoredIndices = new HashSet<int>();
            for (var i = 0; i < birds.Count; i++)
            {
                if (!capturedBirdIndices.Contains(birds[i].Index))
                {
                    continue;
                }

                restoredIndices.Add(birds[i].Index);
                if (!birds[i].IsCaptured && !birds[i].TryCapture())
                {
                    throw new InvalidOperationException("A saved bird capture could not be restored.");
                }
            }

            if (restoredIndices.Count != capturedBirdIndices.Count)
            {
                throw new InvalidDataException("A saved bird capture does not exist in the generated world.");
            }
        }

        private void ClearDinosaurs()
        {
            for (var i = 0; i < dinosaurs.Count; i++)
            {
                dinosaurs[i].DestroyVisual();
            }

            dinosaurs.Clear();
        }

    }
}
