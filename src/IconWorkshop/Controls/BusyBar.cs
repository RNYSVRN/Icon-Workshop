using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace IconWorkshop.Controls;

/// <summary>
/// Indeterminate progress bar in the Windows accent color (like the indeterminate
/// progress in Windows 11): a rounded segment glides over a subtle track.
/// Only runs while visible.
/// </summary>
public sealed class BusyBar : Grid
{
    private readonly Rectangle _segment;
    private readonly TranslateTransform _move = new();

    public BusyBar()
    {
        Height = 3;
        ClipToBounds = true;
        IsHitTestVisible = false;

        var track = new Rectangle { RadiusX = 1.5, RadiusY = 1.5, Opacity = 0.25 };
        track.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        _segment = new Rectangle { RadiusX = 1.5, RadiusY = 1.5, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = _move };
        _segment.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        Children.Add(track);
        Children.Add(_segment);

        SizeChanged += (_, _) => Restart();
        IsVisibleChanged += (_, _) => Restart();
    }

    private void Restart()
    {
        _move.BeginAnimation(TranslateTransform.XProperty, null);
        if (!IsVisible || ActualWidth <= 0) return;

        double w = ActualWidth;
        _segment.Width = w * 0.35;
        var anim = new DoubleAnimation(-_segment.Width, w, TimeSpan.FromSeconds(1.4))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        _move.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
