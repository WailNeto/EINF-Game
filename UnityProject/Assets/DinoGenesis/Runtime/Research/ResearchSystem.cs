using System;
using System.Collections.Generic;
using System.IO;
using DinoGenesis.Economy;
using DinoGenesis.Persistence;
using UnityEngine;

namespace DinoGenesis.Research
{
    [DisallowMultipleComponent]
    public sealed class ResearchSystem : MonoBehaviour
    {
        private readonly List<ResearchMilestone> completedMilestones = new List<ResearchMilestone>();
        private readonly List<string> unlockedSpeciesIds = new List<string>();
        private ResearchCatalog catalog;
        private EconomySystem economy;

        [SerializeField] private int dnaFragments;

        public int DnaFragments => dnaFragments;
        public IReadOnlyList<ResearchMilestone> CompletedMilestones => completedMilestones;
        public IReadOnlyList<string> UnlockedSpeciesIds => unlockedSpeciesIds;
        public ResearchMilestone NextMilestone
        {
            get
            {
                if (catalog == null)
                {
                    return null;
                }

                var milestones = catalog.Milestones;
                for (var i = 0; i < milestones.Length; i++)
                {
                    if (dnaFragments < milestones[i].DnaRequired)
                    {
                        return milestones[i];
                    }
                }

                return null;
            }
        }

        public ResearchSaveData CaptureSaveData()
        {
            return new ResearchSaveData { dnaFragments = dnaFragments };
        }

        public void RestoreSaveData(ResearchSaveData data)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException("Bind a research catalog before restoring research data.");
            }

            if (data == null || data.dnaFragments < 0)
            {
                throw new ArgumentException("The saved research data is invalid.", nameof(data));
            }

            var expectedDna = 0;
            foreach (var siteId in economy.AnalyzedSiteIds)
            {
                if (!economy.TryGetFossil(siteId, out var fossil) || fossil?.Definition == null)
                {
                    throw new InvalidDataException("A saved analyzed fossil is missing from the fossil history.");
                }

                expectedDna = checked(expectedDna + fossil.Definition.ResearchValue);
            }

            if (data.dnaFragments != expectedDna)
            {
                throw new ArgumentException("Saved research points do not match the analyzed fossil history.", nameof(data));
            }

            dnaFragments = data.dnaFragments;
            RebuildMilestones();
        }

        public void Bind(ResearchCatalog researchCatalog, EconomySystem economySystem)
        {
            if (researchCatalog == null)
            {
                throw new ArgumentNullException(nameof(researchCatalog), "Research requires a research catalog.");
            }

            if (!researchCatalog.IsValid)
            {
                throw new ArgumentException("The research catalog contains no valid, strictly ordered milestones.", nameof(researchCatalog));
            }

            if (economySystem == null)
            {
                throw new ArgumentNullException(nameof(economySystem), "Research requires an economy.");
            }

            catalog = researchCatalog;
            economy = economySystem;
            RebuildMilestones();
        }

        public bool IsSpeciesUnlocked(string speciesId)
        {
            return !string.IsNullOrWhiteSpace(speciesId) && unlockedSpeciesIds.Contains(speciesId);
        }

        public bool TryAnalyzeFossil(string siteId, out int dnaAwarded, out ResearchMilestone[] newlyCompleted)
        {
            dnaAwarded = 0;
            newlyCompleted = Array.Empty<ResearchMilestone>();
            if (catalog == null || economy == null ||
                !economy.TryGetFossil(siteId, out var fossil) ||
                fossil == null || fossil.Definition == null ||
                !economy.IsInInventory(siteId))
            {
                return false;
            }

            var resultingDna = checked(dnaFragments + fossil.Definition.ResearchValue);
            var earnedMilestones = GetMilestonesBetween(dnaFragments, resultingDna);
            if (!economy.TryAnalyzeFossil(siteId, out var awarded))
            {
                return false;
            }

            dnaFragments = resultingDna;
            dnaAwarded = awarded;
            newlyCompleted = earnedMilestones;
            for (var i = 0; i < earnedMilestones.Length; i++)
            {
                var milestone = earnedMilestones[i];
                completedMilestones.Add(milestone);
                if (milestone.UnlocksSpecies && !unlockedSpeciesIds.Contains(milestone.UnlockedSpeciesId))
                {
                    unlockedSpeciesIds.Add(milestone.UnlockedSpeciesId);
                }
            }

            return true;
        }

        private ResearchMilestone[] GetMilestonesBetween(int previousDna, int resultingDna)
        {
            var milestones = catalog.Milestones;
            var earned = new List<ResearchMilestone>();
            for (var i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (milestone.DnaRequired > previousDna && milestone.DnaRequired <= resultingDna)
                {
                    earned.Add(milestone);
                }
            }

            return earned.ToArray();
        }

        private void RebuildMilestones()
        {
            completedMilestones.Clear();
            unlockedSpeciesIds.Clear();
            var milestones = catalog.Milestones;
            for (var i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (dnaFragments < milestone.DnaRequired)
                {
                    continue;
                }

                completedMilestones.Add(milestone);
                if (milestone.UnlocksSpecies)
                {
                    unlockedSpeciesIds.Add(milestone.UnlockedSpeciesId);
                }
            }
        }
    }
}
