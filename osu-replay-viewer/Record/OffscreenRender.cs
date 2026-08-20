using HarmonyLib;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Platform;
using osuTK.Graphics.ES30;
using System;
using System.Reflection;
using Size = System.Drawing.Size;

namespace osu_replay_renderer_netcore.Record;

// Renders the scene into an offscreen framebuffer at a fixed resolution instead of the window backbuffer.
// Window managers refuse to size windows past the monitor's work area (eg. Cinnamon clamps to 1920x1008 on my system), so reaching resolutions above the display requires not using the window at all.
// SwapBuffers being blocked by RenderPatcher means the real backbuffer is never presented, so nothing is lost by redirecting the render target.
public static class OffscreenRender
{
    private static readonly Harmony harmony = new("osureplayrenderer.offscreen");
    private static bool clientSizePatched;

    public static bool Active { get; private set; }
    public static Size Resolution { get; private set; }
    public static int Framebuffer { get; private set; }

    public static void Activate(Size resolution)
    {
        if (Active && Resolution == resolution) return;

        Resolution = resolution;
        Active = true;
        ApplyClientSizePatch();
    }

    private static void ApplyClientSizePatch()
    {
        if (clientSizePatched) return;
        clientSizePatched = true;

        var windows = new[]
        {
            "osu.Framework.Platform.SDL3.SDL3Window",
            "osu.Framework.Platform.SDL2.SDL2Window"
        };

        foreach (var name in windows)
        {
            var windowType = typeof(IWindow).Assembly.GetType(name);
            var getter = windowType?.GetMethod("get_ClientSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (getter == null) continue;
            harmony.Patch(getter, postfix: new HarmonyMethod(typeof(OffscreenRender), nameof(ClientSizePostfix)));
        }
    }

    private static void ClientSizePostfix(ref Size __result)
    {
        if (Active)
            __result = Resolution;
    }

    // Creates the offscreen framebuffer and routes the framework's backbuffer rendering to it
    public static void EnsureFramebuffer(IRenderer renderer)
    {
        if (Framebuffer != 0) return;

        var glRendererType = typeof(IRenderer).Assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer");

        // The GL context is not guaranteed to be current when DrawFrame starts (it is lost between frames on the draw thread with multi-threaded execution): establish it the same way GLRenderer.BeginFrame does
        var surfaceField = glRendererType.GetField("openGLSurface", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var openGLSurface = (IOpenGLGraphicsSurface)surfaceField.GetValue(renderer);
        openGLSurface.MakeCurrent(openGLSurface.WindowContext);

        GL.GenFramebuffers(1, out int fbo);
        GL.GenRenderbuffers(1, out int color);
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, color);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, (RenderbufferInternalFormat)0x8058 /* GL_RGBA8 */, Resolution.Width, Resolution.Height);

        GL.GenRenderbuffers(1, out int depthStencil);
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthStencil);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.Depth24Stencil8, Resolution.Width, Resolution.Height);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, color);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthStencil);

        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("Failed to create the offscreen framebuffer");

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        var field = glRendererType.GetField("backbufferFramebuffer", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(renderer, fbo);

        Framebuffer = fbo;
        Console.WriteLine($"Offscreen render target ready ({Resolution.Width}x{Resolution.Height}, fbo={fbo})");
    }
}
