using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WpfApp1.Controls;

/// <summary>
/// JSON 表格里可折叠区块的标题：▸ 箭头 + 键名 + 数量徽标。
/// 对应设计稿中的 .toggle / .toggle .count 样式。
/// </summary>
public sealed class GridToggleHeader : StackPanel
{
    private static readonly Brush ArrowBrush = ColorBrush("#2563EB");
    private static readonly Brush LabelBrush = ColorBrush("#111827");
    private static readonly Brush LabelHoverBrush = ColorBrush("#2563EB");
    private static readonly Brush BadgeBackground = ColorBrush("#F3F4F6");
    private static readonly Brush BadgeForeground = ColorBrush("#6B7280");

    private readonly RotateTransform _arrowRotation = new(0);

    public GridToggleHeader(string label, string countText, double indent = 0, bool compact = false)
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        Background = Brushes.Transparent;
        Margin = compact ? new Thickness(indent, 2, 0, 2) : new Thickness(indent, 7, 0, 4);
        Cursor = Cursors.Hand;

        double fontSize = compact ? 12 : 13;

        Arrow = new TextBlock
        {
            Text = "▸",
            FontSize = compact ? 10 : 11,
            Foreground = ArrowBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, compact ? 4 : 6, 0),
            RenderTransform = _arrowRotation,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        Label = new TextBlock
        {
            Text = label,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = LabelBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        BadgeText = new TextBlock
        {
            Text = countText,
            FontSize = compact ? 10 : 11,
            Foreground = BadgeForeground,
            VerticalAlignment = VerticalAlignment.Center
        };

        Badge = new Border
        {
            Background = BadgeBackground,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 0, 7, 1),
            Margin = new Thickness(compact ? 5 : 7, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = BadgeText,
            Visibility = string.IsNullOrEmpty(countText) ? Visibility.Collapsed : Visibility.Visible
        };

        Children.Add(Arrow);
        Children.Add(Label);
        Children.Add(Badge);

        MouseEnter += (_, _) => Label.Foreground = LabelHoverBrush;
        MouseLeave += (_, _) => Label.Foreground = LabelBrush;
    }

    public TextBlock Arrow { get; }

    public TextBlock Label { get; }

    public Border Badge { get; }

    public TextBlock BadgeText { get; }

    public bool IsExpanded { get; private set; }

    /// <summary>更新展开状态：箭头从 ▸ 旋转到 ▾。</summary>
    public void SetExpanded(bool expanded, bool animate = true)
    {
        if (IsExpanded == expanded)
            return;

        IsExpanded = expanded;
        double target = expanded ? 90 : 0;

        if (!animate)
        {
            _arrowRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            _arrowRotation.Angle = target;
            return;
        }

        _arrowRotation.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private static Brush ColorBrush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
}
