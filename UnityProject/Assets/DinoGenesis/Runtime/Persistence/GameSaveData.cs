using System;

namespace DinoGenesis.Persistence
{
    [Serializable]
    public sealed class GameSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int worldSeed;
        public EconomySaveData economy = new EconomySaveData();
        public ResearchSaveData research = new ResearchSaveData();
        public DinosaurSaveData dinosaurs = new DinosaurSaveData();
        public ZooSaveData zoo = new ZooSaveData();
    }

    [Serializable]
    public sealed class EconomySaveData
    {
        public int coins;
        public int equippedTool;
        public FossilSaveData[] fossilHistory = Array.Empty<FossilSaveData>();
        public string[] inventorySiteIds = Array.Empty<string>();
        public string[] analyzedSiteIds = Array.Empty<string>();
        public string[] claimedSiteIds = Array.Empty<string>();
        public TransactionSaveData[] transactions = Array.Empty<TransactionSaveData>();
    }

    [Serializable]
    public sealed class FossilSaveData
    {
        public string siteId;
        public string fossilId;
    }

    [Serializable]
    public sealed class TransactionSaveData
    {
        public int type;
        public int amount;
        public string itemId;
    }

    [Serializable]
    public sealed class ResearchSaveData
    {
        public int dnaFragments;
    }

    [Serializable]
    public sealed class DinosaurSaveData
    {
        public int avianSamples;
        public int[] capturedBirdIndices = Array.Empty<int>();
        public DinosaurRecordSaveData[] records = Array.Empty<DinosaurRecordSaveData>();
    }

    [Serializable]
    public sealed class DinosaurRecordSaveData
    {
        public string id;
        public string speciesId;
        public float ageSeconds;
        public int enclosureIndex;
    }

    [Serializable]
    public sealed class ZooSaveData
    {
        public bool[] unlockedEnclosures = { true, false, false };
        public int visitorsServed;
        public int totalRevenue;
    }
}
