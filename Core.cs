using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
        }

        private void OnPlayerEntitySpawn(IClientPlayer byPlayer)
        {
            if (byPlayer.Entity.GetBehavior<ExtraOverlayEntityBehavior>() == null)
            {
                byPlayer.Entity.AddBehavior(new ExtraOverlayEntityBehavior(byPlayer.Entity));
            }
        }

        public override void Dispose()
        {
            if (_api != null)
            {
                _api.Event.PlayerEntitySpawn -= OnPlayerEntitySpawn;
                _api = null;
            }
            base.Dispose();
        }
    }
}
