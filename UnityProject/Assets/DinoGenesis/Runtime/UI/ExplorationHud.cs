using System.Globalization;
using DinoGenesis.Economy;
using DinoGenesis.Fossils;
using DinoGenesis.Research;
using DinoGenesis.Dinosaurs;
using DinoGenesis.World;
using DinoGenesis.Zoo;
using UnityEngine;

namespace DinoGenesis
{
    public sealed class ExplorationHud : MonoBehaviour
    {
        private const float LandmarkRange = 18f;
        private IslandWorld world;
        private Player.FirstPersonController player;
        private GameBootstrap bootstrap;
        private ExcavationSystem excavation;
        private EconomySystem economy;
        private ResearchSystem research;
        private DinosaurSystem dinosaurs;
        private ZooSystem zoo;
        private string seedText = string.Empty;
        private string nearbyLandmark = string.Empty;
        private string marketMessage = string.Empty;
        private string researchMessage = string.Empty;
        private string dinosaurMessage = string.Empty;
        private string zooMessage = string.Empty;
        private string pendingSiteId = string.Empty;
        private string pendingResearchSiteId = string.Empty;
        private bool eggConfirmationPending;
        private bool enclosureUnlockConfirmationPending;
        private PendingMarketAction pendingMarketAction;
        private bool researchConfirmationPending;
        private Vector2 marketScroll;
        private Vector2 researchScroll;
        private int displayedDiscoveryCount;
        private float discoveryToastRemaining;
        private float marketMessageRemaining;
        private float researchMessageRemaining;
        private float dinosaurMessageRemaining;
        private float zooMessageRemaining;
        private WildBird targetedBird;
        private Vector2 zooScroll;
        private MenuState menuState;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle fieldStyle;
        private bool stylesCreated;

        private enum MenuState
        {
            Closed,
            FieldNotes,
            Market,
            Research,
            Zoo,
            Pause
        }

        private enum PendingMarketAction
        {
            None,
            SellFossil,
            BuyNextTool
        }

        public bool MenuOpen => menuState != MenuState.Closed;
        public bool MarketOpen => menuState == MenuState.Market;
        public bool ResearchOpen => menuState == MenuState.Research;
        public bool ZooOpen => menuState == MenuState.Zoo;

        public void Bind(
            IslandWorld islandWorld,
            Player.FirstPersonController firstPersonController,
            GameBootstrap gameBootstrap,
            ExcavationSystem excavationSystem,
            EconomySystem economySystem,
            ResearchSystem researchSystem,
            DinosaurSystem dinosaurSystem,
            ZooSystem zooSystem)
        {
            world = islandWorld;
            player = firstPersonController;
            bootstrap = gameBootstrap;
            excavation = excavationSystem;
            economy = economySystem;
            research = researchSystem;
            dinosaurs = dinosaurSystem;
            zoo = zooSystem;
            seedText = world.CurrentSeed.ToString(CultureInfo.InvariantCulture);
            displayedDiscoveryCount = excavation.Discoveries.Count;
            discoveryToastRemaining = 0f;
            menuState = MenuState.Closed;
            ClearPendingMarketAction();
            ClearResearchConfirmation();
            eggConfirmationPending = false;
            enclosureUnlockConfirmationPending = false;
            SetMenuInputState();
        }

        private void Update()
        {
            if (world == null || player == null || excavation == null || economy == null ||
                research == null || dinosaurs == null || zoo == null)
            {
                return;
            }

            nearbyLandmark = world.TryGetNearbyLandmark(player.transform.position, LandmarkRange, out var name, out _)
                ? name
                : string.Empty;
            dinosaurs.TryGetTargetedBird(player.PlayerCamera, 6f, out targetedBird);

            if (excavation.Discoveries.Count != displayedDiscoveryCount)
            {
                displayedDiscoveryCount = excavation.Discoveries.Count;
                discoveryToastRemaining = 4f;
            }
            else if (discoveryToastRemaining > 0f)
            {
                discoveryToastRemaining -= Time.unscaledDeltaTime;
            }

            if (marketMessageRemaining > 0f)
            {
                marketMessageRemaining -= Time.unscaledDeltaTime;
            }

            if (researchMessageRemaining > 0f)
            {
                researchMessageRemaining -= Time.unscaledDeltaTime;
            }

            if (dinosaurMessageRemaining > 0f)
            {
                dinosaurMessageRemaining -= Time.unscaledDeltaTime;
            }

            if (zooMessageRemaining > 0f)
            {
                zooMessageRemaining -= Time.unscaledDeltaTime;
            }
        }

        public void TogglePause()
        {
            menuState = menuState == MenuState.Closed ? MenuState.Pause : MenuState.Closed;
            ClearPendingMarketAction();
            ClearResearchConfirmation();
            eggConfirmationPending = false;
            enclosureUnlockConfirmationPending = false;
            SetMenuInputState();
        }

        public void ToggleFieldNotes()
        {
            menuState = menuState == MenuState.FieldNotes ? MenuState.Closed : MenuState.FieldNotes;
            ClearPendingMarketAction();
            ClearResearchConfirmation();
            eggConfirmationPending = false;
            enclosureUnlockConfirmationPending = false;
            SetMenuInputState();
        }

        public void ToggleInteraction()
        {
            if (menuState == MenuState.Research)
            {
                CloseResearch();
                return;
            }

            if (menuState == MenuState.Market)
            {
                CloseMarket();
                return;
            }

            if (menuState == MenuState.Zoo)
            {
                CloseZoo();
                return;
            }

            if (dinosaurs.TryCaptureBirdFromView(player.PlayerCamera, 6f, out var captureMessage))
            {
                SetDinosaurMessage(captureMessage);
                return;
            }

            if (world.IsNearResearchStation(player.transform.position, 8f))
            {
                marketMessage = string.Empty;
                menuState = MenuState.Research;
                ClearResearchConfirmation();
                eggConfirmationPending = false;
                SetMenuInputState();
                return;
            }

            if (world.IsNearZoo(player.transform.position, 12f))
            {
                zooMessage = string.Empty;
                menuState = MenuState.Zoo;
                enclosureUnlockConfirmationPending = false;
                SetMenuInputState();
                return;
            }

            if (!world.IsNearMarket(player.transform.position, 8f))
            {
                marketMessage = "Visit the market, research lab, or zoo entrance to interact.";
                marketMessageRemaining = 2.5f;
                return;
            }

            marketMessage = string.Empty;
            pendingMarketAction = PendingMarketAction.None;
            ClearResearchConfirmation();
            menuState = MenuState.Market;
            SetMenuInputState();
        }

        public void CloseMarket()
        {
            if (menuState != MenuState.Market)
            {
                return;
            }

            menuState = MenuState.Closed;
            ClearPendingMarketAction();
            SetMenuInputState();
        }

        public void CloseResearch()
        {
            if (menuState != MenuState.Research)
            {
                return;
            }

            menuState = MenuState.Closed;
            ClearResearchConfirmation();
            eggConfirmationPending = false;
            SetMenuInputState();
        }

        public void CloseZoo()
        {
            if (menuState != MenuState.Zoo)
            {
                return;
            }

            menuState = MenuState.Closed;
            enclosureUnlockConfirmationPending = false;
            SetMenuInputState();
        }

        public void SetWorldSeed(int seed)
        {
            seedText = seed.ToString(CultureInfo.InvariantCulture);
            marketScroll = Vector2.zero;
            researchScroll = Vector2.zero;
        }

        public void RefreshAfterLoad()
        {
            displayedDiscoveryCount = excavation.Discoveries.Count;
            discoveryToastRemaining = 0f;
            ClearPendingMarketAction();
            ClearResearchConfirmation();
            eggConfirmationPending = false;
            enclosureUnlockConfirmationPending = false;
        }

        private void SetMenuInputState()
        {
            player?.SetInputEnabled(menuState == MenuState.Closed);
        }

        private void OnGUI()
        {
            if (world == null)
            {
                return;
            }

            EnsureStyles();
            DrawHud();

            if (menuState == MenuState.Pause)
            {
                DrawPauseMenu();
            }
            else if (menuState == MenuState.FieldNotes)
            {
                DrawFieldNotes();
            }
            else if (menuState == MenuState.Market)
            {
                DrawMarketMenu();
            }
            else if (menuState == MenuState.Research)
            {
                DrawResearchMenu();
            }
            else if (menuState == MenuState.Zoo)
            {
                DrawZooMenu();
            }
        }

        private void DrawHud()
        {
            GUI.color = new Color(0.035f, 0.09f, 0.085f, 0.88f);
            GUI.Box(new Rect(20f, 20f, 350f, 196f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(36f, 26f, 310f, 28f), "DINO GENESIS", titleStyle);
            GUI.Label(new Rect(36f, 54f, 310f, 22f), "FIELD SITE 01  |  SEED " + world.CurrentSeed, bodyStyle);
            GUI.Label(new Rect(36f, 78f, 310f, 20f), "Funds: " + economy.Coins + " coins", smallStyle);
            GUI.Label(new Rect(36f, 98f, 310f, 20f),
                "Fossils: " + economy.FossilInventory.Count + "  |  Tool: " + GetToolName(economy.EquippedTool), smallStyle);
            GUI.Label(new Rect(36f, 118f, 310f, 18f), "Recovered: " + excavation.Discoveries.Count, smallStyle);
            GUI.Label(new Rect(36f, 136f, 310f, 18f),
                "DNA: " + research.DnaFragments + "  |  Species: " + research.UnlockedSpeciesIds.Count, smallStyle);
            GUI.Label(new Rect(36f, 154f, 310f, 18f),
                "Bird samples: " + dinosaurs.AvianSamples + "  |  Dinosaurs: " + dinosaurs.Dinosaurs.Count, smallStyle);
            GUI.Label(new Rect(36f, 172f, 310f, 18f),
                "Zoo visitors: " + zoo.VisitorsServed + "  |  Revenue: " + zoo.TotalRevenue, smallStyle);

            var helpY = Screen.height - 88f;
            GUI.color = new Color(0.035f, 0.09f, 0.085f, 0.78f);
            GUI.Box(new Rect(20f, helpY, 360f, 66f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, helpY + 9f, 330f, 22f), "WASD Move  |  Mouse Look  |  Space Jump", smallStyle);
            GUI.Label(new Rect(34f, helpY + 33f, 330f, 22f), "Shift Sprint | LMB Dig | I Inventory | E Catch / Interact", smallStyle);

            DrawExcavationPrompt();
            if (menuState == MenuState.Closed && targetedBird != null)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f + 32f, 360f, 24f),
                    "Press E to catch a wild bird for DNA", centerStyle);
            }
            else if (menuState == MenuState.Closed && world.IsNearResearchStation(player.transform.position, 8f))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 32f, 310f, 24f),
                    "Press E to open the research lab", centerStyle);
            }
            else if (menuState == MenuState.Closed && world.IsNearMarket(player.transform.position, 8f))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 32f, 310f, 24f),
                    "Press E to open the market", centerStyle);
            }
            else if (menuState == MenuState.Closed && world.IsNearZoo(player.transform.position, 12f))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 32f, 310f, 24f),
                    "Press E to manage the zoo", centerStyle);
            }
            else if (researchMessageRemaining > 0f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f + 32f, 420f, 40f),
                    researchMessage, centerStyle);
            }
            else if (marketMessageRemaining > 0f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 175f, Screen.height * 0.5f + 32f, 350f, 24f),
                    marketMessage, centerStyle);
            }
            else if (dinosaurMessageRemaining > 0f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f + 32f, 440f, 40f),
                    dinosaurMessage, centerStyle);
            }
            else if (zooMessageRemaining > 0f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f + 32f, 440f, 40f),
                    zooMessage, centerStyle);
            }
            if (!string.IsNullOrEmpty(nearbyLandmark))
            {
                GUI.color = new Color(0.04f, 0.11f, 0.1f, 0.83f);
                GUI.Box(new Rect(Screen.width - 318f, 22f, 294f, 48f), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width - 305f, 34f, 270f, 24f), "Nearby: " + nearbyLandmark, bodyStyle);
            }

            GUI.color = new Color(0.96f, 0.93f, 0.78f, 0.9f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, Screen.height * 0.5f - 1f, 2f, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawExcavationPrompt()
        {
            if (excavation.CurrentSite != null)
            {
                var panel = new Rect(Screen.width * 0.5f - 190f, Screen.height - 174f, 380f, 78f);
                GUI.color = new Color(0.035f, 0.09f, 0.085f, 0.9f);
                GUI.Box(panel, GUIContent.none);
                GUI.color = Color.white;

                var site = excavation.CurrentSite;
                var heading = "EXCAVATION SITE " + (site.SiteIndex + 1).ToString("00", CultureInfo.InvariantCulture);
                if (site.IsComplete && excavation.TryGetDiscovery(site.SiteId, out var discovery))
                {
                    GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 24f),
                        "Recovered: " + discovery.Definition.DisplayName + " (" + discovery.Definition.Rarity + ")", bodyStyle);
                    GUI.Label(new Rect(panel.x + 14f, panel.y + 40f, panel.width - 28f, 20f),
                        "Research value " + discovery.Definition.ResearchValue + "  |  Sale value " + discovery.Definition.SaleValue, smallStyle);
                }
                else
                {
                    GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 22f),
                        heading + "  |  Hold left mouse to excavate", bodyStyle);
                    var bar = new Rect(panel.x + 14f, panel.y + 39f, panel.width - 28f, 20f);
                    GUI.color = new Color(0.13f, 0.19f, 0.16f);
                    GUI.Box(bar, GUIContent.none);
                    GUI.color = new Color(0.82f, 0.63f, 0.29f);
                    GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * site.Progress, bar.height - 4f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bar.x, bar.y, bar.width, bar.height),
                        Mathf.RoundToInt(site.Progress * 100f) + "%", centerStyle);
                }
            }
            else if (discoveryToastRemaining > 0f && excavation.LastDiscovery != null)
            {
                var found = excavation.LastDiscovery;
                GUI.Label(new Rect(Screen.width * 0.5f - 210f, Screen.height - 48f, 420f, 30f),
                    "Recovered " + found.Definition.DisplayName + " (" + found.Definition.Rarity + ")", centerStyle);
            }
            else
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height - 48f, 360f, 30f),
                    "Explore the island and find amber sites", centerStyle);
            }
        }

        private void DrawPauseMenu()
        {
            var panel = new Rect(Screen.width * 0.5f - 205f, Screen.height * 0.5f - 220f, 410f, 440f);
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 0.97f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 30f, panel.y + 24f, panel.width - 60f, panel.height - 48f));
            GUILayout.Label("PAUSED", titleStyle);
            GUILayout.Space(10f);
            GUILayout.Label("World seed", bodyStyle);
            seedText = GUILayout.TextField(seedText, fieldStyle, GUILayout.Height(34f));
            GUILayout.Space(12f);
            if (GUILayout.Button("Generate island from seed", buttonStyle, GUILayout.Height(42f)))
            {
                if (int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedSeed))
                {
                    bootstrap.RegenerateWorld(requestedSeed);
                }
                else
                {
                    seedText = world.CurrentSeed.ToString(CultureInfo.InvariantCulture);
                }
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Save game", buttonStyle, GUILayout.Height(38f)))
            {
                bootstrap.SaveGame();
            }

            if (GUILayout.Button("Load game", buttonStyle, GUILayout.Height(38f)))
            {
                bootstrap.LoadGame();
            }

            GUILayout.Label(bootstrap.SaveStatus, smallStyle);
            GUILayout.Space(4f);
            if (GUILayout.Button("Resume exploration", buttonStyle, GUILayout.Height(42f)))
            {
                menuState = MenuState.Closed;
                SetMenuInputState();
            }

            GUILayout.Space(12f);
            GUILayout.Label("Esc resumes. Seeded terrain and objects regenerate together.", smallStyle);
            GUILayout.EndArea();
        }

        private void DrawFieldNotes()
        {
            var panel = new Rect(Screen.width - 384f, 88f, 360f, Mathf.Min(440f, Screen.height - 110f));
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 0.95f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 22f, panel.y + 20f, panel.width - 44f, panel.height - 40f));
            GUILayout.Label("INVENTORY", titleStyle);
            GUILayout.Label(
                "Funds: " + economy.Coins + " coins  |  Equipped: " + GetToolName(economy.EquippedTool) +
                " (" + economy.ExcavationSpeedMultiplier.ToString("0.#", CultureInfo.InvariantCulture) + "x digging)",
                bodyStyle);
            GUILayout.Space(8f);
            if (economy.FossilInventory.Count == 0)
            {
                GUILayout.Label("No fossils in your pack. Find an amber site and hold the left mouse button to excavate.", smallStyle);
            }
            else
            {
                for (var i = 0; i < economy.FossilInventory.Count; i++)
                {
            var find = economy.FossilInventory[i];
            GUILayout.Label(
                find.Definition.DisplayName + "  [" + find.Definition.Rarity + "]\n" +
                "Sale " + find.Definition.SaleValue + " coins  |  Research " + find.Definition.ResearchValue + " DNA",
                smallStyle);
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label(
                "Recovered this expedition: " + excavation.Discoveries.Count +
                ". Sell fossils at the nearby market or keep them for research.",
                smallStyle);
            GUILayout.Space(12f);
            if (GUILayout.Button("Close inventory  (I)", buttonStyle, GUILayout.Height(38f)))
            {
                menuState = MenuState.Closed;
                SetMenuInputState();
            }

            GUILayout.EndArea();
        }

        private void DrawMarketMenu()
        {
            var panelHeight = Mathf.Min(600f, Screen.height - 32f);
            var panel = new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - panelHeight * 0.5f, 520f, panelHeight);
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 0.97f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, panel.height - 36f));
            GUILayout.Label("ISLAND MARKET", titleStyle);
            GUILayout.Label("Balance: " + economy.Coins + " coins  |  E or Esc to close", bodyStyle);
            GUILayout.Space(10f);

            if (pendingMarketAction == PendingMarketAction.SellFossil &&
                economy.TryGetFossil(pendingSiteId, out var pendingFossil))
            {
                GUILayout.Label("Sell " + pendingFossil.Definition.DisplayName + " for " +
                                pendingFossil.Definition.SaleValue + " coins?", bodyStyle);
                DrawConfirmationButtons();
            }
            else if (pendingMarketAction == PendingMarketAction.BuyNextTool)
            {
                var name = economy.EquippedTool == ExcavationTool.Hands ? "Shovel" : "Improved shovel";
                GUILayout.Label("Buy " + name + " for " + economy.NextToolPrice + " coins?", bodyStyle);
                DrawConfirmationButtons();
            }

            GUILayout.Space(8f);
            marketScroll = GUILayout.BeginScrollView(marketScroll, GUILayout.Height(Mathf.Max(120f, panelHeight - 260f)));
            GUILayout.Label("SELL FOSSILS", bodyStyle);
            if (economy.FossilInventory.Count == 0)
            {
                GUILayout.Label("Your inventory is empty. Fossils can only be sold once.", smallStyle);
            }
            else
            {
                for (var i = 0; i < economy.FossilInventory.Count; i++)
                {
                    var fossil = economy.FossilInventory[i];
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label(fossil.Definition.DisplayName + "  [" + fossil.Definition.Rarity + "]",
                        smallStyle, GUILayout.MinWidth(210f), GUILayout.ExpandWidth(true));
                    GUILayout.Label(fossil.Definition.SaleValue + " coins", smallStyle, GUILayout.Width(72f));
                    if (GUILayout.Button("Select", buttonStyle, GUILayout.Width(72f), GUILayout.Height(30f)))
                    {
                        pendingMarketAction = PendingMarketAction.SellFossil;
                        pendingSiteId = fossil.SiteId;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("TOOLS", bodyStyle);
            GUILayout.Label("Equipped: " + GetToolName(economy.EquippedTool) + "  |  Digging speed: " +
                            economy.ExcavationSpeedMultiplier.ToString("0.#", CultureInfo.InvariantCulture) + "x", smallStyle);
            if (economy.EquippedTool == ExcavationTool.ImprovedShovel)
            {
                GUILayout.Label("All available tools purchased.", smallStyle);
            }
            else
            {
                var nextTool = economy.EquippedTool == ExcavationTool.Hands ? "Shovel" : "Improved shovel";
                if (GUILayout.Button("Select " + nextTool + "  (" + economy.NextToolPrice + " coins)",
                        buttonStyle, GUILayout.Height(36f)))
                {
                    pendingMarketAction = PendingMarketAction.BuyNextTool;
                    pendingSiteId = string.Empty;
                }
            }

            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(marketMessage) && marketMessageRemaining > 0f)
            {
                GUILayout.Label(marketMessage, smallStyle);
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Close market  (E / Esc)", buttonStyle, GUILayout.Height(36f)))
            {
                CloseMarket();
            }

            GUILayout.EndArea();
        }

        private void DrawResearchMenu()
        {
            var panelHeight = Mathf.Min(600f, Screen.height - 32f);
            var panel = new Rect(Screen.width * 0.5f - 270f, Screen.height * 0.5f - panelHeight * 0.5f, 540f, panelHeight);
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 0.97f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, panel.height - 36f));
            GUILayout.Label("RESEARCH LAB", titleStyle);
            GUILayout.Label("DNA fragments: " + research.DnaFragments + "  |  Fossils available: " +
                            economy.FossilInventory.Count + "  |  E or Esc to close", bodyStyle);
            GUILayout.Space(8f);
            DrawResearchProgress();

            if (researchConfirmationPending &&
                economy.TryGetFossil(pendingResearchSiteId, out var selectedFossil) &&
                economy.IsInInventory(pendingResearchSiteId))
            {
                GUILayout.Label("Analyze " + selectedFossil.Definition.DisplayName + " for " +
                                selectedFossil.Definition.ResearchValue + " DNA fragments?", bodyStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm analysis", buttonStyle, GUILayout.Height(34f)))
                {
                    ConfirmResearchAnalysis();
                }

                if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(34f)))
                {
                    ClearResearchConfirmation();
                }

                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(researchMessage) && researchMessageRemaining > 0f)
            {
                GUILayout.Label(researchMessage, smallStyle);
            }

            researchScroll = GUILayout.BeginScrollView(researchScroll, GUILayout.Height(Mathf.Max(120f, panelHeight - 300f)));
            GUILayout.Label("AVAILABLE SPECIMENS", bodyStyle);
            if (economy.FossilInventory.Count == 0)
            {
                GUILayout.Label("No fossils available. A fossil can be sold or analyzed, never both.", smallStyle);
            }
            else
            {
                for (var i = 0; i < economy.FossilInventory.Count; i++)
                {
                    var fossil = economy.FossilInventory[i];
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label(fossil.Definition.DisplayName + "  [" + fossil.Definition.Rarity + "]",
                        smallStyle, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true));
                    GUILayout.Label("+" + fossil.Definition.ResearchValue + " DNA", smallStyle, GUILayout.Width(90f));
                    if (GUILayout.Button("Analyze", buttonStyle, GUILayout.Width(82f), GUILayout.Height(30f)))
                    {
                        pendingResearchSiteId = fossil.SiteId;
                        researchConfirmationPending = true;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("RESEARCH MILESTONES", bodyStyle);
            for (var i = 0; i < research.CompletedMilestones.Count; i++)
            {
                var milestone = research.CompletedMilestones[i];
                GUILayout.Label("Unlocked: " + milestone.DisplayName + " - " + milestone.Description, smallStyle);
            }

            var nextMilestone = research.NextMilestone;
            if (nextMilestone != null)
            {
                GUILayout.Label("Next: " + nextMilestone.DisplayName + " (" + nextMilestone.DnaRequired +
                                " DNA) - " + nextMilestone.Description, smallStyle);
            }
            else
            {
                GUILayout.Label("All current research milestones completed.", smallStyle);
            }

            if (research.IsSpeciesUnlocked("velociraptor"))
            {
                GUILayout.Label("SPECIES UNLOCKED: Small Velociraptor genome ready for synthesis.", bodyStyle);
            }

            DrawDinosaurWorkbench();
            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            if (GUILayout.Button("Close lab  (E / Esc)", buttonStyle, GUILayout.Height(36f)))
            {
                CloseResearch();
            }

            GUILayout.EndArea();
        }

        private void DrawDinosaurWorkbench()
        {
            GUILayout.Space(10f);
            GUILayout.Label("DINO GENETICS", bodyStyle);
            GUILayout.Label("Wild bird DNA samples: " + dinosaurs.AvianSamples, smallStyle);
            if (research.IsSpeciesUnlocked("velociraptor"))
            {
                if (eggConfirmationPending)
                {
                    GUILayout.Label("Synthesize a Velociraptor egg using 1 bird DNA sample?", smallStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Synthesize egg", buttonStyle, GUILayout.Height(34f)))
                    {
                        dinosaurs.TryCreateEgg("velociraptor", out var result);
                        SetDinosaurMessage(result);
                        eggConfirmationPending = false;
                    }

                    if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(34f)))
                    {
                        eggConfirmationPending = false;
                    }

                    GUILayout.EndHorizontal();
                }
                else if (GUILayout.Button("Prepare Velociraptor egg", buttonStyle, GUILayout.Height(34f)))
                {
                    eggConfirmationPending = true;
                }
            }
            else
            {
                GUILayout.Label("Velociraptor genome is still locked in research.", smallStyle);
            }

            for (var i = 0; i < dinosaurs.Dinosaurs.Count; i++)
            {
                var dinosaur = dinosaurs.Dinosaurs[i];
                var stageName = dinosaur.LifeStage == DinosaurLifeStage.Egg
                    ? "Incubating egg"
                    : dinosaur.LifeStage == DinosaurLifeStage.Baby ? "Baby" : "Adult";
                GUILayout.Label(
                    dinosaur.SpeciesId + "  |  " + stageName + "  " +
                    Mathf.RoundToInt(dinosaur.StageProgress * 100f) + "%",
                    smallStyle);
            }

            if (dinosaurMessageRemaining > 0f)
            {
                GUILayout.Label(dinosaurMessage, smallStyle);
            }
        }

        private void SetDinosaurMessage(string message)
        {
            dinosaurMessage = message;
            dinosaurMessageRemaining = 6f;
        }

        private void DrawZooMenu()
        {
            var panelHeight = Mathf.Min(620f, Screen.height - 32f);
            var panel = new Rect(
                Screen.width * 0.5f - 300f,
                Screen.height * 0.5f - panelHeight * 0.5f,
                600f,
                panelHeight);
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 0.97f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, panel.height - 36f));
            GUILayout.Label("ISLAND ZOO", titleStyle);
            GUILayout.Label(
                "Enclosures: " + zoo.UnlockedEnclosureCount + " / 3  |  Visitors: " + zoo.VisitorsServed +
                "  |  Admission: 2 coins / visitor  |  E or Esc to close",
                bodyStyle);
            GUILayout.Label("Lifetime zoo revenue: " + zoo.TotalRevenue + " coins.", smallStyle);
            zooScroll = GUILayout.BeginScrollView(zooScroll, GUILayout.Height(Mathf.Max(150f, panelHeight - 260f)));

            for (var enclosureIndex = 0; enclosureIndex < 3; enclosureIndex++)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("ENCLOSURE " + (enclosureIndex + 1), bodyStyle);
                if (!zoo.IsEnclosureUnlocked(enclosureIndex))
                {
                    GUILayout.Label("Locked. Unlock cost: " + (enclosureIndex == 1 ? 250 : 600) + " coins.", smallStyle);
                }
                else
                {
                    var assignedId = zoo.GetAssignedDinosaurId(enclosureIndex);
                    if (!string.IsNullOrEmpty(assignedId) && dinosaurs.TryGetDinosaur(assignedId, out var assigned))
                    {
                        GUILayout.Label(assigned.SpeciesId + " (" + assigned.LifeStage + ") on display.", smallStyle);
                        if (GUILayout.Button("Remove dinosaur", buttonStyle, GUILayout.Height(30f)))
                        {
                            zoo.TryUnassignDinosaur(assigned.Id, out var result);
                            SetZooMessage(result);
                        }
                    }
                    else
                    {
                        GUILayout.Label("Empty enclosure. Only adult dinosaurs can be displayed.", smallStyle);
                        for (var i = 0; i < dinosaurs.Dinosaurs.Count; i++)
                        {
                            var candidate = dinosaurs.Dinosaurs[i];
                            if (candidate.LifeStage != DinosaurLifeStage.Adult || candidate.EnclosureIndex >= 0)
                            {
                                continue;
                            }

                            if (GUILayout.Button("Assign " + candidate.SpeciesId + "  (" + candidate.Id + ")",
                                    buttonStyle, GUILayout.Height(30f)))
                            {
                                zoo.TryAssignDinosaur(candidate.Id, enclosureIndex, out var result);
                                SetZooMessage(result);
                            }
                        }
                    }
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            if (enclosureUnlockConfirmationPending)
            {
                GUILayout.Label("Unlock the next enclosure for " + zoo.NextEnclosurePrice + " coins?", bodyStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm unlock", buttonStyle, GUILayout.Height(34f)))
                {
                    zoo.TryUnlockNextEnclosure(out var result);
                    SetZooMessage(result);
                    enclosureUnlockConfirmationPending = false;
                }

                if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(34f)))
                {
                    enclosureUnlockConfirmationPending = false;
                }

                GUILayout.EndHorizontal();
            }
            else if (zoo.NextEnclosurePrice > 0 &&
                     GUILayout.Button("Unlock next enclosure  (" + zoo.NextEnclosurePrice + " coins)",
                         buttonStyle, GUILayout.Height(34f)))
            {
                enclosureUnlockConfirmationPending = true;
            }

            if (zooMessageRemaining > 0f)
            {
                GUILayout.Label(zooMessage, smallStyle);
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Close zoo  (E / Esc)", buttonStyle, GUILayout.Height(36f)))
            {
                CloseZoo();
            }

            GUILayout.EndArea();
        }

        private void SetZooMessage(string message)
        {
            zooMessage = message;
            zooMessageRemaining = 5f;
        }

        private void DrawResearchProgress()
        {
            var nextMilestone = research.NextMilestone;
            if (nextMilestone == null)
            {
                GUILayout.Label("Current research tree complete.", smallStyle);
                return;
            }

            var previousRequirement = research.CompletedMilestones.Count == 0
                ? 0
                : research.CompletedMilestones[research.CompletedMilestones.Count - 1].DnaRequired;
            var interval = nextMilestone.DnaRequired - previousRequirement;
            var progress = interval <= 0
                ? 1f
                : Mathf.Clamp01((research.DnaFragments - previousRequirement) / (float)interval);

            GUILayout.Label("Research stage: " + nextMilestone.DisplayName, smallStyle);
            var bar = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
            GUI.color = new Color(0.13f, 0.19f, 0.16f);
            GUI.Box(bar, GUIContent.none);
            GUI.color = new Color(0.42f, 0.72f, 0.62f);
            GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * progress, bar.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(bar, research.DnaFragments + " / " + nextMilestone.DnaRequired + " DNA", centerStyle);
        }

        private void ConfirmResearchAnalysis()
        {
            if (!research.TryAnalyzeFossil(pendingResearchSiteId, out var dnaAwarded, out var completedMilestones))
            {
                SetResearchMessage("This fossil is no longer available to analyze.");
                ClearResearchConfirmation();
                return;
            }

            var message = "Analysis complete: +" + dnaAwarded + " DNA fragments.";
            for (var i = 0; i < completedMilestones.Length; i++)
            {
                message += " Unlocked " + completedMilestones[i].DisplayName + ".";
            }

            SetResearchMessage(message);
            ClearResearchConfirmation();
        }

        private void SetResearchMessage(string message)
        {
            researchMessage = message;
            researchMessageRemaining = 6f;
        }

        private void ClearResearchConfirmation()
        {
            researchConfirmationPending = false;
            pendingResearchSiteId = string.Empty;
        }

        private void DrawConfirmationButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirm", buttonStyle, GUILayout.Height(34f)))
            {
                ConfirmMarketAction();
            }

            if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(34f)))
            {
                ClearPendingMarketAction();
            }

            GUILayout.EndHorizontal();
        }

        private void ConfirmMarketAction()
        {
            if (pendingMarketAction == PendingMarketAction.SellFossil)
            {
                if (!economy.TrySellFossil(pendingSiteId, out var saleValue))
                {
                    SetMarketMessage("That fossil is no longer available to sell.");
                }
                else
                {
                    SetMarketMessage("Fossil sold for " + saleValue + " coins.");
                }
            }
            else if (pendingMarketAction == PendingMarketAction.BuyNextTool)
            {
                economy.TryPurchaseNextTool(out var reason);
                SetMarketMessage(reason);
            }

            ClearPendingMarketAction();
        }

        private void SetMarketMessage(string message)
        {
            marketMessage = message;
            marketMessageRemaining = 4f;
        }

        private void ClearPendingMarketAction()
        {
            pendingMarketAction = PendingMarketAction.None;
            pendingSiteId = string.Empty;
        }

        private static string GetToolName(ExcavationTool tool)
        {
            return tool == ExcavationTool.ImprovedShovel
                ? "Improved shovel"
                : tool == ExcavationTool.Shovel ? "Shovel" : "Hands";
        }

        private void EnsureStyles()
        {
            if (stylesCreated)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.83f, 0.77f, 0.55f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.93f, 0.86f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.82f, 0.76f) }
            };
            centerStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.1f, 0.16f, 0.13f) }
            };
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            stylesCreated = true;
        }
    }
}
