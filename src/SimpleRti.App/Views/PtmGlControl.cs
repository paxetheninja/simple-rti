using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using SimpleRti.Ptm;
using SimpleRti.Renderer;

namespace SimpleRti.App.Views;

public class PtmGlControl : OpenGlControlBase
{
    private PtmRenderer? _renderer;
    private PtmFile? _ptmFile;

    public static readonly StyledProperty<float> LightUProperty =
        AvaloniaProperty.Register<PtmGlControl, float>(nameof(LightU), 0f);

    public static readonly StyledProperty<float> LightVProperty =
        AvaloniaProperty.Register<PtmGlControl, float>(nameof(LightV), 0f);

    public static readonly StyledProperty<RenderMode> ModeProperty =
        AvaloniaProperty.Register<PtmGlControl, RenderMode>(nameof(Mode), RenderMode.Default);

    public static readonly StyledProperty<float> SpecularExponentProperty =
        AvaloniaProperty.Register<PtmGlControl, float>(nameof(SpecularExponent), 32f);

    public static readonly StyledProperty<float> DiffuseGainProperty =
        AvaloniaProperty.Register<PtmGlControl, float>(nameof(DiffuseGain), 1f);

    public float LightU
    {
        get => GetValue(LightUProperty);
        set => SetValue(LightUProperty, value);
    }

    public float LightV
    {
        get => GetValue(LightVProperty);
        set => SetValue(LightVProperty, value);
    }

    public RenderMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public float SpecularExponent
    {
        get => GetValue(SpecularExponentProperty);
        set => SetValue(SpecularExponentProperty, value);
    }

    public float DiffuseGain
    {
        get => GetValue(DiffuseGainProperty);
        set => SetValue(DiffuseGainProperty, value);
    }

    static PtmGlControl()
    {
        LightUProperty.Changed.AddClassHandler<PtmGlControl>((c, _) => c.RequestNextFrameRendering());
        LightVProperty.Changed.AddClassHandler<PtmGlControl>((c, _) => c.RequestNextFrameRendering());
        ModeProperty.Changed.AddClassHandler<PtmGlControl>((c, _) => c.RequestNextFrameRendering());
        SpecularExponentProperty.Changed.AddClassHandler<PtmGlControl>((c, _) => c.RequestNextFrameRendering());
        DiffuseGainProperty.Changed.AddClassHandler<PtmGlControl>((c, _) => c.RequestNextFrameRendering());
    }

    public void LoadPtm(PtmFile ptm)
    {
        _ptmFile = ptm;
        _renderer = null;
        Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        // Defer initialization to first render so PtmFile can be set
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_ptmFile == null)
        {
            gl.ClearColor(0.15f, 0.15f, 0.15f, 1f);
            gl.Clear(GlConsts.GL_COLOR_BUFFER_BIT);
            return;
        }

        if (_renderer == null)
        {
            _renderer = new PtmRenderer();
            _renderer.Init(gl, _ptmFile);
        }

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        int w = (int)(Bounds.Width * scaling);
        int h = (int)(Bounds.Height * scaling);

        _renderer.Render(gl, fb, w, h, LightU, LightV, Mode, SpecularExponent, DiffuseGain);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Deinit(gl);
        _renderer = null;
    }
}
