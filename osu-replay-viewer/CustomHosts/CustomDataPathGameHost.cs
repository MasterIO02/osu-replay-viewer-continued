using osu.Framework.Input.Handlers;
using osu.Framework.Platform;
using System.Collections.Generic;

namespace osu_replay_renderer_netcore.CustomHosts
{
    public class CustomDataPathGameHost : DesktopGameHost
    {
        private readonly string customDataPath;

        public override IEnumerable<string> UserStoragePaths
        {
            get
            {
                if (!string.IsNullOrEmpty(customDataPath))
                    return new[] { customDataPath };

                return CrossPlatform.GetUserStoragePaths();
            }
        }

        public CustomDataPathGameHost(string gameName, string customDataPath) : base(gameName)
        {
            this.customDataPath = customDataPath;
        }

        protected override IWindow CreateWindow(GraphicsSurfaceType preferredSurface) => CrossPlatform.GetWindow(preferredSurface, Name);
        protected override IEnumerable<InputHandler> CreateAvailableInputHandlers() => [];
    }
}
