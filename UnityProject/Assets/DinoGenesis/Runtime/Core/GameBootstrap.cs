using System;
using System.IO;
using DinoGenesis.Player;
using DinoGenesis.World;
using DinoGenesis.Fossils;
using DinoGenesis.Economy;
using DinoGenesis.Research;
using DinoGenesis.Dinosaurs;
using DinoGenesis.Persistence;
using DinoGenesis.Zoo;
using UnityEngine;

namespace DinoGenesis
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private IslandWorld world;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private ExplorationHud hud;
        [SerializeField] private ExcavationSystem excavation;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private ResearchSystem research;
        [SerializeField] private DinosaurSystem dinosaurs;
        [SerializeField] private ZooSystem zoo;
        [SerializeField] private FossilCatalog fossilCatalog;
        [SerializeField] private ResearchCatalog researchCatalog;
        [SerializeField] private DinosaurCatalog dinosaurCatalog;
        [SerializeField] private int startingSeed = 20261106;

        private IGameSaveRepository saveRepository;
        private bool isInitialized;

        public int CurrentSeed => world != null ? world.CurrentSeed : startingSeed;
        public string SaveStatus { get; private set; } = "No save action yet.";
        public bool IsConfigured => world != null && player != null && hud != null &&
                                    excavation != null && economy != null &&
                                    research != null && dinosaurs != null && zoo != null &&
                                    fossilCatalog != null && fossilCatalog.IsValid &&
                                    researchCatalog != null && researchCatalog.IsValid &&
                                    dinosaurCatalog != null && dinosaurCatalog.IsValid;

        private void Start()
        {
            if (!IsConfigured)
            {
                Debug.LogError("Dino Genesis scene is missing a world, player, HUD, gameplay systems, or content catalog.", this);
                enabled = false;
                return;
            }

            world.Generate(startingSeed);
            economy.Bind(fossilCatalog);
            research.Bind(researchCatalog, economy);
            dinosaurs.Bind(dinosaurCatalog, research, world);
            zoo.Bind(dinosaurs, economy, world);
            player.BindHud(hud);
            player.Teleport(world.GetPlayerSpawnPosition());
            excavation.Bind(world, player, fossilCatalog, economy);
            hud.Bind(world, player, this, excavation, economy, research, dinosaurs, zoo);
            saveRepository = new SqliteGameSaveRepository(
                Path.Combine(Application.persistentDataPath, "dino-genesis.sqlite3"));
            if (saveRepository.HasSave)
            {
                RestoreSaveData(saveRepository.Load());
                SaveStatus = "Saved game loaded.";
            }
            else
            {
                SaveStatus = "No saved game found.";
            }

            isInitialized = true;
        }

        public void SaveGame()
        {
            EnsureInitialized();
            saveRepository.Save(new GameSaveData
            {
                schemaVersion = GameSaveData.CurrentSchemaVersion,
                worldSeed = world.CurrentSeed,
                economy = economy.CaptureSaveData(),
                research = research.CaptureSaveData(),
                dinosaurs = dinosaurs.CaptureSaveData(),
                zoo = zoo.CaptureSaveData()
            });
            SaveStatus = "Game saved to SQLite.";
        }

        public bool LoadGame()
        {
            EnsureInitialized();
            if (!saveRepository.HasSave)
            {
                SaveStatus = "No saved game found.";
                return false;
            }

            RestoreSaveData(saveRepository.Load());
            SaveStatus = "Saved game loaded.";
            return true;
        }

        public void RegenerateWorld(int seed)
        {
            startingSeed = seed;
            world.Generate(seed);
            excavation.Bind(world, player, fossilCatalog, economy);
            dinosaurs.Bind(dinosaurCatalog, research, world);
            zoo.Bind(dinosaurs, economy, world);
            player.Teleport(world.GetPlayerSpawnPosition());
            hud.SetWorldSeed(seed);
            hud.CloseMarket();
            hud.CloseResearch();
            hud.CloseZoo();
        }

        private void RestoreSaveData(GameSaveData data)
        {
            if (data == null || data.schemaVersion != GameSaveData.CurrentSchemaVersion ||
                data.economy == null || data.research == null ||
                data.dinosaurs == null || data.zoo == null)
            {
                throw new InvalidDataException("The saved game is incomplete or uses an unsupported schema.");
            }

            startingSeed = data.worldSeed;
            world.Generate(data.worldSeed);
            economy.RestoreSaveData(data.economy);
            research.RestoreSaveData(data.research);
            dinosaurs.Bind(dinosaurCatalog, research, world, false);
            dinosaurs.RestoreSaveData(data.dinosaurs);
            zoo.RestoreSaveData(data.zoo);
            player.Teleport(world.GetPlayerSpawnPosition());
            excavation.Bind(world, player, fossilCatalog, economy);
            hud.SetWorldSeed(data.worldSeed);
            hud.RefreshAfterLoad();
            hud.CloseMarket();
            hud.CloseResearch();
            hud.CloseZoo();
        }

        private void EnsureInitialized()
        {
            if (!isInitialized || saveRepository == null)
            {
                throw new InvalidOperationException("The game has not finished initializing its save repository.");
            }
        }

        public void Configure(
            IslandWorld islandWorld,
            FirstPersonController firstPersonController,
            ExplorationHud explorationHud,
            ExcavationSystem excavationSystem,
            EconomySystem economySystem,
            ResearchSystem researchSystem,
            FossilCatalog catalog,
            ResearchCatalog researchContent,
            DinosaurSystem dinosaurSystem,
            DinosaurCatalog dinosaurContent,
            ZooSystem zooSystem)
        {
            world = islandWorld;
            player = firstPersonController;
            hud = explorationHud;
            excavation = excavationSystem;
            economy = economySystem;
            research = researchSystem;
            dinosaurs = dinosaurSystem;
            zoo = zooSystem;
            fossilCatalog = catalog;
            researchCatalog = researchContent;
            dinosaurCatalog = dinosaurContent;
        }
    }
}
