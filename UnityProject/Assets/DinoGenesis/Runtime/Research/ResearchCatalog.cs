using System;
using UnityEngine;

namespace DinoGenesis.Research
{
    [Serializable]
    public sealed class ResearchMilestone
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int dnaRequired;
        [SerializeField] private string description;
        [SerializeField] private string unlockedSpeciesId;

        public string Id => id;
        public string DisplayName => displayName;
        public int DnaRequired => dnaRequired;
        public string Description => description;
        public string UnlockedSpeciesId => unlockedSpeciesId;
        public bool UnlocksSpecies => !string.IsNullOrWhiteSpace(unlockedSpeciesId);

        public ResearchMilestone(
            string milestoneId,
            string name,
            int requiredDna,
            string milestoneDescription,
            string speciesId = "")
        {
            id = milestoneId;
            displayName = name;
            dnaRequired = requiredDna;
            description = milestoneDescription;
            unlockedSpeciesId = speciesId;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(id) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            !string.IsNullOrWhiteSpace(description) &&
            dnaRequired > 0;
    }

    [CreateAssetMenu(menuName = "Dino Genesis/Research Catalog")]
    public sealed class ResearchCatalog : ScriptableObject
    {
        [SerializeField] private ResearchMilestone[] milestones = Array.Empty<ResearchMilestone>();

        public ResearchMilestone[] Milestones => milestones;
        public bool IsValid
        {
            get
            {
                if (milestones == null || milestones.Length == 0)
                {
                    return false;
                }

                var previousRequirement = 0;
                for (var i = 0; i < milestones.Length; i++)
                {
                    if (milestones[i] == null || !milestones[i].IsValid ||
                        milestones[i].DnaRequired <= previousRequirement)
                    {
                        return false;
                    }

                    previousRequirement = milestones[i].DnaRequired;
                }

                return true;
            }
        }

        public void SetDefaultMilestones()
        {
            milestones = new[]
            {
                new ResearchMilestone(
                    "field-methods",
                    "Field methods",
                    10,
                    "Improved fossil handling and specimen records."),
                new ResearchMilestone(
                    "comparative-genomics",
                    "Comparative genomics",
                    30,
                    "The lab can now compare recovered DNA fragments."),
                new ResearchMilestone(
                    "velociraptor-genome",
                    "Velociraptor genome",
                    65,
                    "A complete small Velociraptor genome is ready for synthesis.",
                    "velociraptor")
            };
        }
    }
}
