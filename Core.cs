using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace ExtraOverlays
{
    public class Core : ModSystem
    {
        private ICoreClientAPI? _api;

        public override void StartClientSide(ICoreClientAPI api)
        {
            _api = api;
            api.RegisterEntityBehaviorClass("extraoverlay", typeof(ExtraOverlayEntityBehavior));
            api.Event.PlayerEntitySpawn += OnPlayerEntitySpawn;
            api.Logger.Notification("[extraoverlaysm4] Client startup complete, behavior registered");
        }

        private void OnPlayerEntitySpawn(IClientPlayer byPlayer)
        {
            Entity playerEntity = byPlayer.Entity;
            if (playerEntity.GetBehavior<ExtraOverlayEntityBehavior>() != null)
            {
                _api?.Logger.VerboseDebug("[extraoverlaysm4] Behavior already attached to player entity");
                return;
            }

            _api?.Logger.VerboseDebug("[extraoverlaysm4] Attaching overlay behavior to player entity");
            playerEntity.AddBehavior(new ExtraOverlayEntityBehavior(playerEntity));
        }
    }
}
