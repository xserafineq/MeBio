using MeBio.Helpers;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.Controls;

public class MethodEffectivenessProxy
{
    public string Label { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public int TotalAttempts { get; set; }
    public int SuccessCount { get; set; }
    public Color BarColor { get; set; } = Colors.Gray;
}

public class MethodEffectivenessChartDrawable : IDrawable
{
    public IList<MethodEffectivenessProxy>? Stats { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#1A2332");
        canvas.FillRectangle(dirtyRect);

        if (Stats is null || Stats.Count == 0)
        {
            canvas.FontColor = Color.FromArgb("#8899AA");
            canvas.FontSize = 13;
            canvas.DrawString("Brak danych logowania", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var padding = 20f;
        var bottomLabelH = 36f;
        var topLabelH = 22f;
        var chartHeight = dirtyRect.Height - padding * 2 - bottomLabelH - topLabelH;
        var count = Stats.Count;
        var slotWidth = (dirtyRect.Width - padding * 2) / count;
        var barWidth = Math.Min(72f, slotWidth - 16f);
        var x = dirtyRect.Left + padding + (slotWidth - barWidth) / 2;
        var baseY = dirtyRect.Bottom - padding - bottomLabelH;

        foreach (var stat in Stats)
        {
            var barH = (float)(stat.SuccessRate / 100.0 * chartHeight);

            canvas.FillColor = Color.FromArgb("#2A3548");
            canvas.FillRectangle(x, baseY - chartHeight, barWidth, chartHeight);

            canvas.FillColor = stat.BarColor;
            canvas.FillRectangle(x, baseY - barH, barWidth, barH);

            canvas.FontColor = Color.FromArgb("#E8EEF5");
            canvas.FontSize = 11;
            canvas.DrawString($"{stat.SuccessRate:F0}%", x, baseY - barH - topLabelH, barWidth, topLabelH,
                HorizontalAlignment.Center, VerticalAlignment.Bottom);

            canvas.FontColor = Color.FromArgb("#8899AA");
            canvas.FontSize = 10;
            canvas.DrawString(stat.Label, x, baseY + 4, barWidth, 16,
                HorizontalAlignment.Center, VerticalAlignment.Top);

            canvas.DrawString($"{stat.SuccessCount}/{stat.TotalAttempts}", x, baseY + 20, barWidth, 14,
                HorizontalAlignment.Center, VerticalAlignment.Top);

            x += slotWidth;
        }
    }
}

public class MethodEffectivenessChartView : GraphicsView
{
    public static readonly BindableProperty StatsProperty =
        BindableProperty.Create(nameof(Stats), typeof(IList<MethodEffectivenessProxy>), typeof(MethodEffectivenessChartView), null,
            propertyChanged: OnStatsChanged);

    private readonly MethodEffectivenessChartDrawable _drawable = new();

    public MethodEffectivenessChartView()
    {
        Drawable = _drawable;
    }

    public IList<MethodEffectivenessProxy>? Stats
    {
        get => (IList<MethodEffectivenessProxy>?)GetValue(StatsProperty);
        set => SetValue(StatsProperty, value);
    }

    private static void OnStatsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MethodEffectivenessChartView)bindable;
        view._drawable.Stats = (IList<MethodEffectivenessProxy>?)newValue;
        view.Invalidate();
    }
}

public static class MethodEffectivenessMapper
{
    public static List<MethodEffectivenessProxy> ToProxies(IEnumerable<MethodEffectivenessStat> stats) =>
        stats.Select(s => new MethodEffectivenessProxy
        {
            Label = LoginMethodLabels.ToDisplayName(s.Method),
            SuccessRate = s.SuccessRate,
            TotalAttempts = s.TotalAttempts,
            SuccessCount = s.SuccessCount,
            BarColor = LoginMethodLabels.ToBadgeColor(s.Method)
        }).ToList();
}
