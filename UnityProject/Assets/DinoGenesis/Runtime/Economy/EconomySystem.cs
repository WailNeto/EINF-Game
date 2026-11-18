using System;
using System.Collections.Generic;
using DinoGenesis.Fossils;
using DinoGenesis.Persistence;
using UnityEngine;

namespace DinoGenesis.Economy
{
    public enum ExcavationTool
    {
        Hands,
        Shovel,
        ImprovedShovel
    }

    public enum EconomyTransactionType
    {
        FossilDiscovered,
        FossilSold,
        ToolPurchased,
        FossilAnalyzed,
        ZooEnclosurePurchased,
        ZooAdmissionRevenue
    }

    [Serializable]
    public sealed class EconomyTransaction
    {
        [SerializeField] private EconomyTransactionType type;
        [SerializeField] private int amount;
        [SerializeField] private string itemId;

        public EconomyTransactionType Type => type;
        public int Amount => amount;
        public string ItemId => itemId;

        public EconomyTransaction(EconomyTransactionType transactionType, int transactionAmount, string transactionItemId)
        {
            type = transactionType;
            amount = transactionAmount;
            itemId = transactionItemId;
        }
    }

    [DisallowMultipleComponent]
    public sealed class EconomySystem : MonoBehaviour
    {
        public const int ShovelPrice = 30;
        public const int ImprovedShovelPrice = 90;

        private readonly List<FossilFind> fossilHistory = new List<FossilFind>();
        private readonly List<FossilFind> fossilInventory = new List<FossilFind>();
        private readonly List<string> analyzedSiteIds = new List<string>();
        private readonly List<string> claimedSiteIds = new List<string>();
        private readonly List<EconomyTransaction> transactions = new List<EconomyTransaction>();

        private FossilCatalog catalog;

        [SerializeField] private int coins;
        [SerializeField] private ExcavationTool equippedTool;

        public int Coins => coins;
        public ExcavationTool EquippedTool => equippedTool;
        public float ExcavationSpeedMultiplier => equippedTool == ExcavationTool.ImprovedShovel
            ? 3.5f
            : equippedTool == ExcavationTool.Shovel ? 2f : 1f;
        public IReadOnlyList<FossilFind> FossilHistory => fossilHistory;
        public IReadOnlyList<FossilFind> FossilInventory => fossilInventory;
        public IReadOnlyList<string> AnalyzedSiteIds => analyzedSiteIds;
        public IReadOnlyList<EconomyTransaction> Transactions => transactions;
        public int NextToolPrice => equippedTool == ExcavationTool.Hands
            ? ShovelPrice
            : equippedTool == ExcavationTool.Shovel ? ImprovedShovelPrice : 0;

        public EconomySaveData CaptureSaveData()
        {
            var data = new EconomySaveData
            {
                coins = coins,
                equippedTool = (int)equippedTool,
                fossilHistory = new FossilSaveData[fossilHistory.Count],
                inventorySiteIds = fossilInventory.ConvertAll(fossil => fossil.SiteId).ToArray(),
                analyzedSiteIds = analyzedSiteIds.ToArray(),
                claimedSiteIds = claimedSiteIds.ToArray(),
                transactions = new TransactionSaveData[transactions.Count]
            };

            for (var i = 0; i < fossilHistory.Count; i++)
            {
                data.fossilHistory[i] = new FossilSaveData
                {
                    siteId = fossilHistory[i].SiteId,
                    fossilId = fossilHistory[i].Definition.Id
                };
            }

            for (var i = 0; i < transactions.Count; i++)
            {
                data.transactions[i] = new TransactionSaveData
                {
                    type = (int)transactions[i].Type,
                    amount = transactions[i].Amount,
                    itemId = transactions[i].ItemId
                };
            }

            return data;
        }

        public void RestoreSaveData(EconomySaveData data)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException("Bind a fossil catalog before restoring economy data.");
            }

            if (data == null || data.coins < 0 ||
                !Enum.IsDefined(typeof(ExcavationTool), data.equippedTool) ||
                data.fossilHistory == null || data.inventorySiteIds == null ||
                data.analyzedSiteIds == null || data.claimedSiteIds == null ||
                data.transactions == null)
            {
                throw new ArgumentException("The saved economy data is invalid.", nameof(data));
            }

            var restoredHistory = new List<FossilFind>(data.fossilHistory.Length);
            var restoredHistoryBySite = new Dictionary<string, FossilFind>();
            foreach (var fossilData in data.fossilHistory)
            {
                if (fossilData == null || string.IsNullOrWhiteSpace(fossilData.siteId) ||
                    !catalog.TryGetDefinition(fossilData.fossilId, out var definition))
                {
                    throw new ArgumentException("The saved fossil history contains an unknown fossil or site.", nameof(data));
                }

                var fossil = new FossilFind(fossilData.siteId, definition);
                if (restoredHistoryBySite.ContainsKey(fossilData.siteId))
                {
                    throw new ArgumentException("The saved fossil history contains duplicate site IDs.", nameof(data));
                }

                restoredHistoryBySite.Add(fossilData.siteId, fossil);
                restoredHistory.Add(fossil);
            }

            var restoredClaimedSites = CreateUniqueSet(data.claimedSiteIds, nameof(data.claimedSiteIds));
            var restoredAnalyzedSites = CreateUniqueSet(data.analyzedSiteIds, nameof(data.analyzedSiteIds));
            var restoredInventory = new List<FossilFind>(data.inventorySiteIds.Length);
            var restoredInventorySites = CreateUniqueSet(data.inventorySiteIds, nameof(data.inventorySiteIds));
            if (restoredClaimedSites.Count != restoredHistoryBySite.Count ||
                !restoredClaimedSites.SetEquals(restoredHistoryBySite.Keys))
            {
                throw new ArgumentException("Saved claimed excavation sites do not match the fossil history.", nameof(data));
            }

            foreach (var siteId in data.inventorySiteIds)
            {
                if (!restoredHistoryBySite.TryGetValue(siteId, out var fossil) || restoredAnalyzedSites.Contains(siteId))
                {
                    throw new ArgumentException("Saved inventory contains a missing or already analyzed fossil.", nameof(data));
                }

                restoredInventory.Add(fossil);
            }

            foreach (var siteId in restoredAnalyzedSites)
            {
                if (!restoredHistoryBySite.ContainsKey(siteId) || restoredInventorySites.Contains(siteId))
                {
                    throw new ArgumentException("Saved analysis state is inconsistent with fossil history.", nameof(data));
                }
            }

            var restoredTransactions = new List<EconomyTransaction>(data.transactions.Length);
            var ledgerBalance = 0;
            foreach (var transactionData in data.transactions)
            {
                if (transactionData == null ||
                    !Enum.IsDefined(typeof(EconomyTransactionType), transactionData.type) ||
                    transactionData.amount < 0 ||
                    string.IsNullOrWhiteSpace(transactionData.itemId))
                {
                    throw new ArgumentException("The saved transaction ledger contains an invalid entry.", nameof(data));
                }

                var type = (EconomyTransactionType)transactionData.type;
                if (type == EconomyTransactionType.FossilSold || type == EconomyTransactionType.ZooAdmissionRevenue)
                {
                    ledgerBalance = checked(ledgerBalance + transactionData.amount);
                }
                else if (type == EconomyTransactionType.ToolPurchased ||
                         type == EconomyTransactionType.ZooEnclosurePurchased)
                {
                    ledgerBalance = checked(ledgerBalance - transactionData.amount);
                    if (ledgerBalance < 0)
                    {
                        throw new ArgumentException("The saved transaction ledger spends more coins than it earned.", nameof(data));
                    }
                }

                restoredTransactions.Add(new EconomyTransaction(
                    type,
                    transactionData.amount,
                    transactionData.itemId));
            }

            if (ledgerBalance != data.coins)
            {
                throw new ArgumentException("The saved transaction ledger does not match the coin balance.", nameof(data));
            }

            coins = data.coins;
            equippedTool = (ExcavationTool)data.equippedTool;
            fossilHistory.Clear();
            fossilHistory.AddRange(restoredHistory);
            fossilInventory.Clear();
            fossilInventory.AddRange(restoredInventory);
            analyzedSiteIds.Clear();
            analyzedSiteIds.AddRange(restoredAnalyzedSites);
            claimedSiteIds.Clear();
            claimedSiteIds.AddRange(restoredClaimedSites);
            transactions.Clear();
            transactions.AddRange(restoredTransactions);
        }

        public void Bind(FossilCatalog fossilCatalog)
        {
            if (fossilCatalog == null || !fossilCatalog.IsValid)
            {
                throw new ArgumentException("The economy requires a valid fossil catalog.", nameof(fossilCatalog));
            }

            catalog = fossilCatalog;
        }

        public bool IsSiteClaimed(string siteId)
        {
            return !string.IsNullOrWhiteSpace(siteId) && claimedSiteIds.Contains(siteId);
        }

        public bool TryGetFossil(string siteId, out FossilFind fossil)
        {
            for (var i = 0; i < fossilHistory.Count; i++)
            {
                if (fossilHistory[i].SiteId == siteId)
                {
                    fossil = fossilHistory[i];
                    return true;
                }
            }

            fossil = null;
            return false;
        }

        public bool IsInInventory(string siteId)
        {
            for (var i = 0; i < fossilInventory.Count; i++)
            {
                if (fossilInventory[i].SiteId == siteId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnalyzed(string siteId)
        {
            return !string.IsNullOrWhiteSpace(siteId) && analyzedSiteIds.Contains(siteId);
        }

        public bool TryAddFossil(FossilFind fossil)
        {
            if (catalog == null || fossil == null || fossil.Definition == null ||
                !fossil.Definition.IsValid || IsSiteClaimed(fossil.SiteId))
            {
                return false;
            }

            claimedSiteIds.Add(fossil.SiteId);
            fossilHistory.Add(fossil);
            fossilInventory.Add(fossil);
            transactions.Add(new EconomyTransaction(
                EconomyTransactionType.FossilDiscovered,
                0,
                fossil.SiteId));
            return true;
        }

        public bool TrySellFossil(string siteId, out int saleValue)
        {
            saleValue = 0;
            var inventoryIndex = FindInventoryIndex(siteId);
            if (catalog == null || inventoryIndex < 0)
            {
                return false;
            }

            var fossil = fossilInventory[inventoryIndex];
            var newBalance = checked(coins + fossil.Definition.SaleValue);

            fossilInventory.RemoveAt(inventoryIndex);
            coins = newBalance;
            saleValue = fossil.Definition.SaleValue;
            transactions.Add(new EconomyTransaction(EconomyTransactionType.FossilSold, saleValue, siteId));
            return true;
        }

        public bool TryAnalyzeFossil(string siteId, out int researchValue)
        {
            researchValue = 0;
            var inventoryIndex = FindInventoryIndex(siteId);
            if (catalog == null || inventoryIndex < 0 || IsAnalyzed(siteId))
            {
                return false;
            }

            var fossil = fossilInventory[inventoryIndex];
            fossilInventory.RemoveAt(inventoryIndex);
            analyzedSiteIds.Add(siteId);
            researchValue = fossil.Definition.ResearchValue;
            transactions.Add(new EconomyTransaction(EconomyTransactionType.FossilAnalyzed, researchValue, siteId));
            return true;
        }

        public bool TryPurchaseNextTool(out string reason)
        {
            if (equippedTool == ExcavationTool.ImprovedShovel)
            {
                reason = "You already own the best available shovel.";
                return false;
            }

            var price = NextToolPrice;
            if (coins < price)
            {
                reason = $"You need {price} coins; you have {coins}.";
                return false;
            }

            var nextTool = equippedTool == ExcavationTool.Hands
                ? ExcavationTool.Shovel
                : ExcavationTool.ImprovedShovel;
            coins -= price;
            equippedTool = nextTool;
            transactions.Add(new EconomyTransaction(EconomyTransactionType.ToolPurchased, price, nextTool.ToString()));
            reason = nextTool == ExcavationTool.Shovel
                ? "Shovel purchased. Excavation is now 2x faster."
                : "Improved shovel purchased. Excavation is now 3.5x faster.";
            return true;
        }

        public bool TryPurchaseZooEnclosure(int enclosureNumber, int price, out string reason)
        {
            if (enclosureNumber < 2 || price <= 0)
            {
                reason = "The requested enclosure purchase is invalid.";
                return false;
            }

            if (coins < price)
            {
                reason = $"You need {price} coins; you have {coins}.";
                return false;
            }

            coins -= price;
            transactions.Add(new EconomyTransaction(
                EconomyTransactionType.ZooEnclosurePurchased,
                price,
                "enclosure-" + enclosureNumber));
            reason = "Zoo enclosure " + enclosureNumber + " unlocked.";
            return true;
        }

        public bool TryCreditZooRevenue(int amount, out string reason)
        {
            if (amount <= 0)
            {
                reason = "Zoo revenue must be a positive amount.";
                return false;
            }

            var newBalance = checked(coins + amount);
            coins = newBalance;
            transactions.Add(new EconomyTransaction(EconomyTransactionType.ZooAdmissionRevenue, amount, "visitor-admissions"));
            reason = "Zoo earned " + amount + " coins.";
            return true;
        }

        private int FindInventoryIndex(string siteId)
        {
            for (var i = 0; i < fossilInventory.Count; i++)
            {
                if (fossilInventory[i].SiteId == siteId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static HashSet<string> CreateUniqueSet(string[] values, string parameterName)
        {
            var result = new HashSet<string>();
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !result.Add(value))
                {
                    throw new ArgumentException("Saved site IDs must be non-empty and unique.", parameterName);
                }
            }

            return result;
        }
    }
}
