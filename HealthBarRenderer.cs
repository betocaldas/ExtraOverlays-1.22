using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExtraOverlays
{
    public class HealthBarRenderer : IRenderer, IDisposable
    {
        private enum RenderAttemptResult
        {
            Drawn,
            InvalidHealth,
            BehindCamera
        }

        private readonly ICoreClientAPI _api;
        private readonly HealthBarRenderConfig _config;
        private readonly Matrixf _mvMatrix = new();
        private readonly MeshRef? _healthBarRef;
        private readonly MeshRef? _backRef;
        private Vec4f _color = new();
        private float _alpha = 0f;
        private string _lastRenderState = "init";
        private long _nextRenderSampleMs;
        private readonly List<(Entity Entity, double DistSq)> _nearbyEntities = new();
        private long _nextDiagnosticLogMs;

        public bool Active { get; set; }

        public double RenderOrder => 0.41; // After Entity 0.4
        public int RenderRange => 10;

        public HealthBarRenderer(ICoreClientAPI api, HealthBarRenderConfig config)
        {
            _api = api;
            _config = config;

            MeshData backData = LineMeshUtil.GetRectangle(ColorUtil.WhiteArgb);
            _backRef = _api.Render.UploadMesh(backData);
            _healthBarRef = _api.Render.UploadMesh(QuadMeshUtil.GetQuad());

            _api.Event.RegisterRenderer(this, EnumRenderStage.Ortho);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!Active)
            {
                UpdateRenderState("inactive");
                return;
            }

            Entity? playerEntity = _api.World.Player?.Entity;
            if (playerEntity == null)
            {
                UpdateRenderState("no-player");
                return;
            }

            _nearbyEntities.Clear();
            int scannedEntities = 0;
            int healthEntities = 0;
            float maxDistanceBlocks = _config.MaxDistanceBlocks;
            double maxDistanceSq = maxDistanceBlocks * maxDistanceBlocks;
            int maxVisibleEntities = _config.MaxVisibleEntities;
            _api.World.GetEntitiesAround(
                new Vec3d(playerEntity.Pos.X, playerEntity.Pos.Y, playerEntity.Pos.Z),
                maxDistanceBlocks,
                maxDistanceBlocks,
                entity =>
                {
                    scannedEntities++;
                    if (entity.EntityId == playerEntity.EntityId) return false;
                    if (!entity.WatchedAttributes.HasAttribute("health")) return false;

                    healthEntities++;
                    double dx = entity.Pos.X - playerEntity.Pos.X;
                    double dy = entity.Pos.Y - playerEntity.Pos.Y;
                    double dz = entity.Pos.Z - playerEntity.Pos.Z;
                    double distSq = dx * dx + dy * dy + dz * dz;
                    if (distSq > maxDistanceSq) return false;

                    _nearbyEntities.Add((entity, distSq));
                    return false;
                });

            if (scannedEntities == 0)
            {
                UpdateRenderState("no-entities-in-range-query");
                MaybeLogDiagnostics(scannedEntities, healthEntities, 0, 0, 0, 0, maxDistanceBlocks, maxVisibleEntities);
                return;
            }

            if (_nearbyEntities.Count == 0)
            {
                UpdateRenderState("no-nearby-health-entities");
                MaybeLogDiagnostics(scannedEntities, healthEntities, 0, 0, 0, 0, maxDistanceBlocks, maxVisibleEntities);
                return;
            }

            _nearbyEntities.Sort((left, right) => left.DistSq.CompareTo(right.DistSq));

            float deltaAlpha = deltaTime / (Active ? _config.FadeIn : -_config.FadeOut);
            _alpha = Math.Max(0f, Math.Min(1f, _alpha + deltaAlpha));
            int renderCount = Math.Min(maxVisibleEntities, _nearbyEntities.Count);
            int drawnCount = 0;
            int behindCameraCount = 0;
            int invalidHealthCount = 0;

            for (int i = 0; i < renderCount; i++)
            {
                RenderAttemptResult result = RenderHealthBar(_nearbyEntities[i].Entity);
                if (result == RenderAttemptResult.Drawn) drawnCount++;
                if (result == RenderAttemptResult.BehindCamera) behindCameraCount++;
                if (result == RenderAttemptResult.InvalidHealth) invalidHealthCount++;
            }

            UpdateRenderState($"rendering-multi:count={renderCount}");
            MaybeLogDiagnostics(
                scannedEntities,
                healthEntities,
                renderCount,
                drawnCount,
                behindCameraCount,
                invalidHealthCount,
                maxDistanceBlocks,
                maxVisibleEntities);
        }

        private RenderAttemptResult RenderHealthBar(Entity entity)
        {
            ITreeAttribute healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
            float currentHealth = healthTree.GetFloat("currenthealth");
            float maxHealth = healthTree.GetFloat("maxhealth");
            if (maxHealth <= 0)
            {
                return RenderAttemptResult.InvalidHealth;
            }

            float progress = currentHealth / maxHealth;

            GetHealthBarColor(progress, ref _color);

            IShaderProgram shader = _api.Render.CurrentActiveShader;
            shader.Uniform("rgbaIn", _color);
            shader.Uniform("extraGlow", 0);
            shader.Uniform("applyColor", 0);
            shader.Uniform("tex2d", 0);
            shader.Uniform("noTexture", 1f);

            var aboveHeadPos = new Vec3d(
                entity.SidedPos.X,
                entity.SidedPos.Y + entity.CollisionBox.Y2,
                entity.SidedPos.Z);

            double offX = entity.CollisionBox.X2 - entity.OriginCollisionBox.X2;
            double offZ = entity.CollisionBox.Z2 - entity.OriginCollisionBox.Z2;
            aboveHeadPos.Add(offX, 0, offZ);

            Vec3d pos = MatrixToolsd.Project(aboveHeadPos,
                _api.Render.PerspectiveProjectionMat,
                _api.Render.PerspectiveViewMat,
                _api.Render.FrameWidth,
                _api.Render.FrameHeight);

            // Z negative seems to indicate that the name tag is behind us \o/
            if (pos.Z < 0)
            {
                return RenderAttemptResult.BehindCamera;
            }

            float scale = 4f / Math.Max(1, (float)pos.Z);

            float cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f) cappedScale = 0.75f + (cappedScale - 0.75f) / 2;

            float x = (float)pos.X - cappedScale * _config.Width / 2;
            float y = _api.Render.FrameHeight - (float)pos.Y - (_config.Height * Math.Max(0, cappedScale)) - _config.YOffset;
            float z = 20;

            float width = cappedScale * _config.Width;
            float height = cappedScale * _config.Height;

            // Render back
            _mvMatrix
                .Set(_api.Render.CurrentModelviewMatrix)
                .Translate(x, y, z)
                .Scale(width, height, 0)
                .Translate(0.5f, 0.5f, 0)
                .Scale(0.5f, 0.5f, 0);

            shader.UniformMatrix("projectionMatrix", _api.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("modelViewMatrix", _mvMatrix.Values);

            _api.Render.RenderMesh(_backRef);


            // Render health bar
            _mvMatrix
                .Set(_api.Render.CurrentModelviewMatrix)
                .Translate(x, y, z)
                .Scale(width * progress, height, 0)
                .Translate(0.5f, 0.5f, 0)
                .Scale(0.5f, 0.5f, 0);

            shader.UniformMatrix("projectionMatrix", _api.Render.CurrentProjectionMatrix);
            shader.UniformMatrix("modelViewMatrix", _mvMatrix.Values);

            _api.Render.RenderMesh(_healthBarRef);
            return RenderAttemptResult.Drawn;
        }

        private void GetHealthBarColor(float progress, ref Vec4f color)
        {
            HexToVec(_config.HighHPColor, ref color);

            if (progress <= _config.LowHPThreshold)
            {
                HexToVec(_config.LowHPColor, ref color);
            }
            else if (progress <= _config.MidHPThreshold)
            {
                HexToVec(_config.MidHPColor, ref color);
            }

            color.A = _alpha;
        }

        private static void HexToVec(string hexColor, ref Vec4f color)
        {
            int intColor = ColorUtil.Hex2Int(hexColor);
            ColorUtil.ToRGBAVec4f(intColor, ref color);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _api.Render.DeleteMesh(_backRef);
            _api.Render.DeleteMesh(_healthBarRef);
            _api.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }

        private void UpdateRenderState(string nextState)
        {
            if (_lastRenderState != nextState)
            {
                _lastRenderState = nextState;
                _api.Logger.VerboseDebug($"[extraoverlaysm4] Render state: {nextState}");
                _nextRenderSampleMs = _api.ElapsedMilliseconds + 10000;
                return;
            }

            if (_api.ElapsedMilliseconds >= _nextRenderSampleMs)
            {
                _api.Logger.VerboseDebug($"[extraoverlaysm4] Render state sample: {nextState}");
                _nextRenderSampleMs = _api.ElapsedMilliseconds + 10000;
            }
        }

        private void MaybeLogDiagnostics(
            int scannedEntities,
            int healthEntities,
            int selectedToRender,
            int drawnEntities,
            int behindCameraCount,
            int invalidHealthCount,
            float maxDistanceBlocks,
            int maxVisibleEntities)
        {
            if (_api.ElapsedMilliseconds < _nextDiagnosticLogMs)
            {
                return;
            }

            _api.Logger.Notification(
                $"[extraoverlaysm4] Diagnostics range={maxDistanceBlocks} maxVisible={maxVisibleEntities} scanned={scannedEntities} withHealth={healthEntities} renderable={_nearbyEntities.Count} selected={selectedToRender} drawn={drawnEntities} behindCamera={behindCameraCount} invalidHealth={invalidHealthCount} alpha={_alpha:0.00}");
            _nextDiagnosticLogMs = _api.ElapsedMilliseconds + 5000;
        }
    }
}
