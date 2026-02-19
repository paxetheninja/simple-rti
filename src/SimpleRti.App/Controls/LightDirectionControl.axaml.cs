using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace SimpleRti.App.Controls;

public partial class LightDirectionControl : UserControl
{
    public static readonly StyledProperty<float> LightUProperty =
        AvaloniaProperty.Register<LightDirectionControl, float>(nameof(LightU), 0f);

    public static readonly StyledProperty<float> LightVProperty =
        AvaloniaProperty.Register<LightDirectionControl, float>(nameof(LightV), 0f);

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

    private bool _isDragging;

    public LightDirectionControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    static LightDirectionControl()
    {
        AffectsRender<LightDirectionControl>(LightUProperty, LightVProperty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateLightFromPointer(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
            UpdateLightFromPointer(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void UpdateLightFromPointer(Point pos)
    {
        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;
        double radius = Math.Min(cx, cy) - 4;

        double dx = (pos.X - cx) / radius;
        double dy = -(pos.Y - cy) / radius; // flip Y: up = positive V

        // Clamp to unit circle
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1.0)
        {
            dx /= len;
            dy /= len;
        }

        LightU = (float)dx;
        LightV = (float)dy;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;
        double radius = Math.Min(cx, cy) - 4;

        // Background circle
        context.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 1),
            new Point(cx, cy), radius, radius);

        // Crosshairs
        var linePen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1);
        context.DrawLine(linePen, new Point(cx - radius, cy), new Point(cx + radius, cy));
        context.DrawLine(linePen, new Point(cx, cy - radius), new Point(cx, cy + radius));

        // Light direction dot
        double dotX = cx + LightU * radius;
        double dotY = cy - LightV * radius; // flip Y back for screen coords
        context.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(255, 220, 50)),
            new Pen(new SolidColorBrush(Colors.White), 1.5),
            new Point(dotX, dotY), 6, 6);
    }
}
