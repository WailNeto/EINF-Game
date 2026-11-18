using System;
using UnityEngine;

namespace DinoGenesis.Dinosaurs
{
    [Serializable]
    public sealed class DinosaurSpeciesDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string researchSpeciesId;
        [SerializeField] private int birdSamplesRequired = 1;
        [SerializeField] private float incubationSeconds = 30f;
        [SerializeField] private float growthSeconds = 60f;
        [SerializeField] private Color bodyColor = new Color(0.43f, 0.57f, 0.31f);

        public string Id => id;
        public string DisplayName => displayName;
        public string ResearchSpeciesId => researchSpeciesId;
        public int BirdSamplesRequired => birdSamplesRequired;
        public float IncubationSeconds => incubationSeconds;
        public float GrowthSeconds => growthSeconds;
        public Color BodyColor => bodyColor;

        public DinosaurSpeciesDefinition(
            string speciesId,
            string name,
            string unlockedByResearchId,
            int requiredBirdSamples,
            float incubationDuration,
            float growthDuration,
            Color color)
        {
            id = speciesId;
            displayName = name;
            researchSpeciesId = unlockedByResearchId;
            birdSamplesRequired = requiredBirdSamples;
            incubationSeconds = incubationDuration;
            growthSeconds = growthDuration;
            bodyColor = color;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(id) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            !string.IsNullOrWhiteSpace(researchSpeciesId) &&
            birdSamplesRequired > 0 &&
            incubationSeconds > 0f &&
            growthSeconds > 0f;
    }

    [CreateAssetMenu(menuName = "Dino Genesis/Dinosaur Catalog")]
    public sealed class DinosaurCatalog : ScriptableObject
    {
        [SerializeField] private DinosaurSpeciesDefinition[] species = Array.Empty<DinosaurSpeciesDefinition>();

        public DinosaurSpeciesDefinition[] Species => species;
        public bool IsValid
        {
            get
            {
                if (species == null || species.Length == 0)
                {
                    return false;
                }

                for (var i = 0; i < species.Length; i++)
                {
                    if (species[i] == null || !species[i].IsValid)
                    {
                        return false;
                    }

                    for (var j = 0; j < i; j++)
                    {
                        if (species[i].Id == species[j].Id)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public bool TryGetSpecies(string speciesId, out DinosaurSpeciesDefinition definition)
        {
            for (var i = 0; species != null && i < species.Length; i++)
            {
                if (species[i] != null && species[i].Id == speciesId)
                {
                    definition = species[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public void SetDefaultSpecies()
        {
            species = new[]
            {
                new DinosaurSpeciesDefinition(
                    "velociraptor",
                    "Small Velociraptor",
                    "velociraptor",
                    1,
                    30f,
                    60f,
                    new Color(0.43f, 0.57f, 0.31f))
            };
        }
    }
}
