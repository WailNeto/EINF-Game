using System;
using System.Collections.Generic;
using DinoGenesis.Economy;
using DinoGenesis.Player;
using DinoGenesis.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DinoGenesis.Fossils
{
    [DisallowMultipleComponent]
    public sealed class ExcavationSystem : MonoBehaviour
    {
        private const float InteractionRange = 4f;
        private readonly List<FossilFind> discoveries = new List<FossilFind>();
        private IslandWorld world;
        private FirstPersonController player;
        private FossilCatalog catalog;
        private EconomySystem economy;

        public ExcavationSite CurrentSite { get; private set; }
        public IReadOnlyList<FossilFind> Discoveries => discoveries;
        public FossilFind LastDiscovery => discoveries.Count == 0 ? null : discoveries[discoveries.Count - 1];

        public void Bind(
            IslandWorld islandWorld,
            FirstPersonController firstPersonController,
            FossilCatalog fossilCatalog,
            EconomySystem economySystem)
        {
            if (islandWorld == null || firstPersonController == null ||
                fossilCatalog == null || !fossilCatalog.IsValid || economySystem == null)
            {
                throw new ArgumentException("Excavation requires a world, player, valid fossil catalog, and economy.");
            }

            world = islandWorld;
            player = firstPersonController;
            catalog = fossilCatalog;
            economy = economySystem;
            CurrentSite = null;
            discoveries.Clear();

            var sites = world.GetComponentsInChildren<ExcavationSite>();
            foreach (var site in sites)
            {
                if (!economy.IsSiteClaimed(site.SiteId))
                {
                    continue;
                }

                site.MarkComplete();
                if (economy.TryGetFossil(site.SiteId, out var existingFind))
                {
                    discoveries.Add(existingFind);
                }
            }
        }

        public bool TryExcavate(ExcavationSite site, float elapsedSeconds)
        {
            if (site == null || world == null || catalog == null ||
                economy == null ||
                site.WorldSeed != world.CurrentSeed ||
                !site.transform.IsChildOf(world.transform) ||
                site.IsComplete ||
                economy.IsSiteClaimed(site.SiteId))
            {
                return false;
            }

            if (!site.ApplyWork(elapsedSeconds * economy.ExcavationSpeedMultiplier))
            {
                return false;
            }

            var find = new FossilFind(site.SiteId, catalog.GetForSite(site.WorldSeed, site.SiteIndex));
            if (!economy.TryAddFossil(find))
            {
                Debug.LogError($"Excavation at {site.SiteId} completed without an inventory reward.", site);
                return false;
            }

            discoveries.Add(find);
            return true;
        }

        public bool TryGetDiscovery(string siteId, out FossilFind discovery)
        {
            for (var i = 0; i < discoveries.Count; i++)
            {
                if (discoveries[i].SiteId == siteId)
                {
                    discovery = discoveries[i];
                    return true;
                }
            }

            discovery = null;
            return false;
        }

        private void Update()
        {
            if (world == null || player == null || !player.InputEnabled ||
                player.PlayerCamera == null || Mouse.current == null)
            {
                CurrentSite = null;
                return;
            }

            var ray = player.PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out var hit, InteractionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                CurrentSite = hit.collider.GetComponentInParent<ExcavationSite>();
                if (CurrentSite != null && CurrentSite.WorldSeed != world.CurrentSeed)
                {
                    CurrentSite = null;
                }
            }
            else
            {
                CurrentSite = null;
            }

            if (CurrentSite != null && !CurrentSite.IsComplete && Mouse.current.leftButton.isPressed)
            {
                TryExcavate(CurrentSite, Time.deltaTime);
            }
        }
    }
}
