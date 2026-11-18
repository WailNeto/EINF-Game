using System;
using System.Collections.Generic;
using UnityEngine;

namespace DinoGenesis.Fossils
{
    public enum FossilRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    [Serializable]
    public sealed class FossilDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private FossilRarity rarity;
        [SerializeField] private int discoveryWeight;
        [SerializeField] private int saleValue;
        [SerializeField] private int researchValue;

        public string Id => id;
        public string DisplayName => displayName;
        public FossilRarity Rarity => rarity;
        public int DiscoveryWeight => discoveryWeight;
        public int SaleValue => saleValue;
        public int ResearchValue => researchValue;

        public FossilDefinition(
            string fossilId,
            string name,
            FossilRarity fossilRarity,
            int weight,
            int value,
            int research)
        {
            id = fossilId;
            displayName = name;
            rarity = fossilRarity;
            discoveryWeight = weight;
            saleValue = value;
            researchValue = research;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(id) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            discoveryWeight > 0 &&
            saleValue >= 0 &&
            researchValue >= 0;
    }

    [CreateAssetMenu(menuName = "Dino Genesis/Fossil Catalog")]
    public sealed class FossilCatalog : ScriptableObject
    {
        [SerializeField] private FossilDefinition[] fossils = Array.Empty<FossilDefinition>();

        public IReadOnlyList<FossilDefinition> Fossils => fossils;
        public bool IsValid => fossils != null && fossils.Length > 0 && Array.TrueForAll(fossils, fossil => fossil != null && fossil.IsValid);

        public bool TryGetDefinition(string fossilId, out FossilDefinition definition)
        {
            for (var i = 0; fossils != null && i < fossils.Length; i++)
            {
                if (fossils[i] != null && fossils[i].Id == fossilId)
                {
                    definition = fossils[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public FossilDefinition GetForSite(int worldSeed, int siteIndex)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("The fossil catalog is empty or contains an invalid fossil definition.");
            }

            var totalWeight = 0;
            for (var i = 0; i < fossils.Length; i++)
            {
                totalWeight = checked(totalWeight + fossils[i].DiscoveryWeight);
            }

            var random = new System.Random(unchecked(worldSeed * 397 ^ siteIndex));
            var selection = random.Next(totalWeight);
            for (var i = 0; i < fossils.Length; i++)
            {
                selection -= fossils[i].DiscoveryWeight;
                if (selection < 0)
                {
                    return fossils[i];
                }
            }

            throw new InvalidOperationException("The fossil catalog selection exceeded its configured weights.");
        }

        public void SetDefaultDefinitions()
        {
            fossils = new[]
            {
                new FossilDefinition("small-theropod-tooth", "Small theropod tooth", FossilRarity.Common, 60, 20, 2),
                new FossilDefinition("hadrosaur-vertebra", "Hadrosaur vertebra", FossilRarity.Uncommon, 28, 55, 7),
                new FossilDefinition("triceratops-horn", "Triceratops horn fragment", FossilRarity.Rare, 10, 130, 18),
                new FossilDefinition("tyrannosaur-jaw", "Tyrannosaur jaw fragment", FossilRarity.Epic, 2, 300, 45)
            };
        }
    }

    [Serializable]
    public sealed class FossilFind
    {
        [SerializeField] private string siteId;
        [SerializeField] private FossilDefinition definition;

        public string SiteId => siteId;
        public FossilDefinition Definition => definition;

        public FossilFind(string excavationSiteId, FossilDefinition fossilDefinition)
        {
            siteId = excavationSiteId;
            definition = fossilDefinition;
        }
    }
}
