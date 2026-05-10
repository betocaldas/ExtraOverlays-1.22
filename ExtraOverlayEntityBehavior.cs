using System;
using System.IO;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace ExtraOverlays
{
    public class ExtraOverlayEntityBehavior : EntityBehavior
    {
        private readonly ICoreClientAPI _api;
        private readonly HealthBarRenderer _healthBarRenderer;
        private long _lastSelectedEntityId = -1;

        public ExtraOverlayEntityBehavior(Entity entity) : base(entity)
        {
            _api = (ICoreClientAPI)entity.Api;
            var config = LoadConfigWithLegacySupport();
            _api.StoreModConfig(config, "extraoverlays.json");
            _healthBarRenderer = new HealthBarRenderer(_api, config);
            _api.Logger.Notification(
                $"[extraoverlaysm4] Config loaded fadeIn={config.FadeIn} fadeOut={config.FadeOut} width={config.Width} height={config.Height} yOffset={config.YOffset}");
        }

        private HealthBarRenderConfig LoadConfigWithLegacySupport()
        {
            try
            {
                return _api.LoadModConfig<HealthBarRenderConfig>("extraoverlays.json") ?? new HealthBarRenderConfig();
            }
            catch (Exception e)
            {
                _api.Logger.Warning($"[extraoverlaysm4] Failed to load modern config format, trying legacy format: {e.Message}");
                return LoadLegacyConfig();
            }
        }

        private HealthBarRenderConfig LoadLegacyConfig()
        {
            try
            {
                var config = new HealthBarRenderConfig();
                var filePath = Path.Combine(_api.GetOrCreateDataPath("ModConfig"), "extraoverlays.json");

                if (!File.Exists(filePath))
                {
                    _api.Logger.Warning("[extraoverlaysm4] No config file found, using defaults");
                    return config;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(filePath));
                var root = document.RootElement;
                config.FadeIn = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.FadeIn), config.FadeIn);
                config.FadeOut = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.FadeOut), config.FadeOut);
                config.Width = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.Width), config.Width);
                config.Height = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.Height), config.Height);
                config.YOffset = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.YOffset), config.YOffset);
                config.HighHPColor = ReadLegacyString(root, nameof(HealthBarRenderConfig.HighHPColor), config.HighHPColor);
                config.MidHPColor = ReadLegacyString(root, nameof(HealthBarRenderConfig.MidHPColor), config.MidHPColor);
                config.LowHPColor = ReadLegacyString(root, nameof(HealthBarRenderConfig.LowHPColor), config.LowHPColor);
                config.LowHPThreshold = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.LowHPThreshold), config.LowHPThreshold);
                config.MidHPThreshold = ReadLegacyFloat(root, nameof(HealthBarRenderConfig.MidHPThreshold), config.MidHPThreshold);
                _api.Logger.Notification("[extraoverlaysm4] Legacy config converted successfully");
                return config;
            }
            catch
            {
                _api.Logger.Error("[extraoverlaysm4] Legacy config conversion failed, using defaults");
                return new HealthBarRenderConfig();
            }
        }

        private static float ReadLegacyFloat(JsonElement root, string key, float fallback)
        {
            if (!root.TryGetProperty(key, out var token))
            {
                return fallback;
            }

            if (token.ValueKind == JsonValueKind.Object && token.TryGetProperty("Value", out var valueToken))
            {
                return valueToken.GetSingle();
            }

            if (token.ValueKind == JsonValueKind.Number)
            {
                return token.GetSingle();
            }

            return fallback;
        }

        private static string ReadLegacyString(JsonElement root, string key, string fallback)
        {
            if (!root.TryGetProperty(key, out var token))
            {
                return fallback;
            }

            if (token.ValueKind == JsonValueKind.Object && token.TryGetProperty("Value", out var valueToken))
            {
                return valueToken.GetString() ?? fallback;
            }

            if (token.ValueKind == JsonValueKind.String)
            {
                return token.GetString() ?? fallback;
            }

            return fallback;
        }

        public override void OnGameTick(float dt)
        {
            var selectedEntity = _api.World.Player.CurrentEntitySelection?.Entity;
            if (selectedEntity == null)
            {
                _healthBarRenderer.Active = false;
                if (_lastSelectedEntityId != -1)
                {
                    _api.Logger.VerboseDebug("[extraoverlaysm4] Selection cleared");
                    _lastSelectedEntityId = -1;
                }
            }
            else
            {
                _healthBarRenderer.ForEntity = selectedEntity;
                _healthBarRenderer.Active = true;
                if (_lastSelectedEntityId != selectedEntity.EntityId)
                {
                    bool hasHealth = selectedEntity.WatchedAttributes.HasAttribute("health");
                    _api.Logger.VerboseDebug(
                        $"[extraoverlaysm4] Selected entity code={selectedEntity.Code} id={selectedEntity.EntityId} hasHealth={hasHealth}");
                    _lastSelectedEntityId = selectedEntity.EntityId;
                }
            }
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
