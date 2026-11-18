using System;
using System.Globalization;
using UnityEngine;

namespace DinoGenesis.Fossils
{
    [DisallowMultipleComponent]
    public sealed class ExcavationSite : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock markerProperties;
        private Renderer markerRenderer;
        private float requiredWorkSeconds;

        public int WorldSeed { get; private set; }
        public int SiteIndex { get; private set; }
        public string SiteId => WorldSeed.ToString(CultureInfo.InvariantCulture) + ":" +
                                SiteIndex.ToString(CultureInfo.InvariantCulture);
        public float Progress { get; private set; }
        public float RequiredWorkSeconds => requiredWorkSeconds;
        public bool IsComplete { get; private set; }

        private void Awake()
        {
            markerProperties = new MaterialPropertyBlock();
        }

        public void Initialize(int worldSeed, int siteIndex, Renderer visualMarker)
        {
            if (markerProperties == null)
            {
                markerProperties = new MaterialPropertyBlock();
            }

            WorldSeed = worldSeed;
            SiteIndex = siteIndex;
            markerRenderer = visualMarker;

            var random = new System.Random(unchecked(worldSeed * 397 ^ siteIndex));
            requiredWorkSeconds = 6.5f + (float)random.NextDouble() * 3.5f;
            Progress = 0f;
            IsComplete = false;
            UpdateMarkerColor();
        }

        public bool ApplyWork(float elapsedSeconds)
        {
            if (IsComplete || elapsedSeconds <= 0f)
            {
                return false;
            }

            Progress = Mathf.Clamp01(Progress + elapsedSeconds / requiredWorkSeconds);
            if (Progress < 1f)
            {
                return false;
            }

            IsComplete = true;
            UpdateMarkerColor();
            return true;
        }

        public void MarkComplete()
        {
            Progress = 1f;
            IsComplete = true;
            UpdateMarkerColor();
        }

        private void UpdateMarkerColor()
        {
            if (markerRenderer == null)
            {
                return;
            }

            markerRenderer.GetPropertyBlock(markerProperties);
            var color = IsComplete
                ? new Color(0.34f, 0.48f, 0.38f)
                : new Color(0.92f, 0.68f, 0.28f);
            markerProperties.SetColor(BaseColorId, color);
            markerProperties.SetColor(ColorId, color);
            markerRenderer.SetPropertyBlock(markerProperties);
        }
    }
}
