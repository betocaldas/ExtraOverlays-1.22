using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace ExtraOverlays
{
    public class ExtraOverlayEntityBehavior : EntityBehavior
    {
        private readonly ICoreClientAPI _api;
        private readonly HealthBarRenderer _healthBarRenderer;

        public ExtraOverlayEntityBehavior(Entity entity) : base(entity)
        {
            _api = (ICoreClientAPI)entity.Api;
            var config = LoadConfigFromAttrFile();
            _healthBarRenderer = new HealthBarRenderer(_api, config);
            _api.Logger.Notification(
                $"[extraoverlaysm4] attr.json loaded fadeIn={config.FadeIn} fadeOut={config.FadeOut} width={config.Width} height={config.Height} yOffset={config.YOffset} maxVisible={config.MaxVisibleEntities} maxDistance={config.MaxDistanceBlocks}");
        }

        private HealthBarRenderConfig LoadConfigFromAttrFile()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string? assemblyDir = Path.GetDirectoryName(assemblyPath);
                if (string.IsNullOrWhiteSpace(assemblyDir))
                {
                    _api.Logger.Error("[extraoverlaysm4] Could not resolve assembly directory, using fallback attr values");
                    return CreateFallbackConfig();
                }

                string attrPath = Path.Combine(assemblyDir, "attr.json");
                if (!File.Exists(attrPath))
                {
                    _api.Logger.Error($"[extraoverlaysm4] attr.json not found at {attrPath}, using fallback attr values");
                    return CreateFallbackConfig();
                }

                var config = JsonSerializer.Deserialize<HealthBarRenderConfig>(File.ReadAllText(attrPath));
                if (config == null)
                {
                    _api.Logger.Error("[extraoverlaysm4] attr.json is invalid, using fallback attr values");
                    return CreateFallbackConfig();
                }

                return config;
            }
            catch (Exception e)
            {
                _api.Logger.Error($"[extraoverlaysm4] Failed to load attr.json: {e.Message}");
                return CreateFallbackConfig();
            }
        }

        private static HealthBarRenderConfig CreateFallbackConfig()
        {
            return new HealthBarRenderConfig
            {
                FadeIn = 0.2f,
                FadeOut = 0.4f,
                Width = 100f,
                Height = 10f,
                YOffset = 10f,
                HighHPColor = "#7FBF7F",
                MidHPColor = "#BFBF7F",
                LowHPColor = "#BF7F7F",
                LowHPThreshold = 0.25f,
                MidHPThreshold = 0.5f,
                MaxVisibleEntities = 15,
                MaxDistanceBlocks = 10f
            };
        }

        public override void OnGameTick(float dt)
        {
            _healthBarRenderer.Active = true;
        }

        public override string PropertyName() => "extraoverlay";

        // Fired only if client player exit, not died
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            _healthBarRenderer?.Dispose();
        }
    }
}
