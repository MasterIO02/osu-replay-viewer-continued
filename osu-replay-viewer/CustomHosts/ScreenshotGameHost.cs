using osu.Framework;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Input.Handlers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu_replay_renderer_netcore.CustomHosts.CustomClocks;
using osu_replay_renderer_netcore.Patching;
using osuTK.Graphics.ES30;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace osu_replay_renderer_netcore.CustomHosts
{
    public class ScreenshotGameHost : DesktopGameHost
    {
        private readonly string customDataPath;
        private readonly RecordClock recordClock;

        public double? TargetScreenshotMs { get; set; }
        public string? ScreenshotOutputPath { get; set; }

        private bool screenshotTaken;
        private bool screenshotPending;
        private IOpenGLGraphicsSurface openGLSurface;
        private readonly Action<IRenderer> preSwapHandler;

        private WrappedClock wrappedClock;

        public override IEnumerable<string> UserStoragePaths
        {
            get
            {
                if (!string.IsNullOrEmpty(customDataPath))
                    return new[] { customDataPath };
                return CrossPlatform.GetUserStoragePaths();
            }
        }

        public override bool OpenFileExternally(string filename)
        {
            Logger.Log($"Application has requested file \"{filename}\" to be opened.");
            return true;
        }

        public override void OpenUrlExternally(string url) =>
            Logger.Log($"Application has requested URL \"{url}\" to be opened.");

        protected override IFrameBasedClock SceneGraphClock => recordClock;
        protected override IWindow CreateWindow(GraphicsSurfaceType preferredSurface) =>
            CrossPlatform.GetWindow(preferredSurface, Name);
        protected override IEnumerable<InputHandler> CreateAvailableInputHandlers() => [];

        public ScreenshotGameHost(string gameName, RecordClock recordClock, bool patchesApplied, string customDataPath = null) : base(gameName)
        {
            this.recordClock = recordClock;
            this.customDataPath = customDataPath;

            if (patchesApplied)
            {
                preSwapHandler = _ => onPreSwap();
                RenderPatcher.OnPreSwap += preSwapHandler;
            }
        }

        protected override void ChooseAndSetupRenderer()
        {
            var type = RuntimeInfo.OS switch
            {
                RuntimeInfo.Platform.Windows => "veldrid",
                _ => "gl"
            };
            SetupRendererAndWindow(type, GraphicsSurfaceType.OpenGL);
        }

        protected override void SetupForRun()
        {
            base.SetupForRun();
            MaximumDrawHz = 0;
            MaximumUpdateHz = MaximumInactiveHz = 0;
        }

        public void NotifyClockSetup(WrappedClock clock)
        {
            wrappedClock = clock;
        }

        private void ensureGLContext()
        {
            if (openGLSurface != null) return;

            var glRendererType = typeof(IRenderer).Assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer");
            var field = glRendererType.GetField("openGLSurface", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var surface = field.GetValue(Renderer);
            openGLSurface = (IOpenGLGraphicsSurface)surface;
            Console.WriteLine($"Screenshot: GL surface acquired, context={openGLSurface.WindowContext}");
        }

        private void withGLContext(Action action)
        {
            ensureGLContext();
            var windowContext = openGLSurface.WindowContext;
            var previousContext = openGLSurface.CurrentContext;
            bool needSwitch = previousContext != windowContext;

            if (needSwitch)
                openGLSurface.MakeCurrent(windowContext);

            try
            {
                action();
            }
            finally
            {
                if (needSwitch)
                {
                    if (previousContext != IntPtr.Zero)
                        openGLSurface.MakeCurrent(previousContext);
                    else
                        openGLSurface.ClearCurrent();
                }
            }
        }

        private void onPreSwap()
        {
            if (!TargetScreenshotMs.HasValue || screenshotTaken) return;

            recordClock.CurrentFrame++;

            if (wrappedClock == null || !wrappedClock.Started)
            {
                if (recordClock.CurrentFrame % 100 == 0)
                    Console.WriteLine($"Fast-forward: frame {recordClock.CurrentFrame}");
                return;
            }

            // Previous frame rendered at exact target time, capture it now
            if (screenshotPending)
            {
                screenshotTaken = true;
                screenshotPending = false;
                Console.WriteLine($"Screenshot: capturing at gameplay time {TargetScreenshotMs.Value:F0}ms (frame {recordClock.CurrentFrame})");

                captureFramebuffer();

                RenderPatcher.OnPreSwap -= preSwapHandler;
                Exit();
                return;
            }

            double gameplayTime = wrappedClock.CurrentTime;

            if (gameplayTime % 5000 < 20)
                Console.WriteLine($"Gameplay time: {gameplayTime:F0}ms / {TargetScreenshotMs.Value:F0}ms");

            if (gameplayTime < TargetScreenshotMs.Value) return;

            // Overshot, snap clock to exact target time so the NEXT frame renders at precisely that time
            wrappedClock.TimeOffset -= gameplayTime - TargetScreenshotMs.Value;
            screenshotPending = true;
        }

        private void captureFramebuffer()
        {
            withGLContext(() =>
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Finish();

                int[] viewport = new int[4];
                GL.GetInteger(GetPName.Viewport, viewport);
                int width = viewport[2];
                int height = viewport[3];

                GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
                var pixels = new byte[width * height * 3];
                GL.ReadPixels(0, 0, width, height, osuTK.Graphics.ES30.PixelFormat.Rgb, PixelType.UnsignedByte, pixels);

                using var image = new Image<Rgba32>(width, height);
                int stride = width * 3;
                for (int y = 0; y < height; y++)
                {
                    int srcRow = (height - 1 - y) * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int px = srcRow + x * 3;
                        image[x, y] = new Rgba32(pixels[px], pixels[px + 1], pixels[px + 2], 255);
                    }
                }

                string output = ScreenshotOutputPath ?? "screenshot.png";
                using (var fs = File.Create(output))
                    image.SaveAsPng(fs);
                Console.WriteLine($"Screenshot saved to {output}");
            });
        }
    }
}
