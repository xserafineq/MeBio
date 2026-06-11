namespace MeBio.Controls;

public class DailyLoginStatProxy
{
    public DateTime Date { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
}

public class LoginChartDrawable : IDrawable
{
    public IList<DailyLoginStatProxy>? Stats { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#1A2332");
        canvas.FillRectangle(dirtyRect);

        if (Stats is null || Stats.Count == 0)
            return;

        var padding = 16f;
        var count = Stats.Count;
        var slotWidth = (dirtyRect.Width - padding * 2) / count;
        var barWidth = slotWidth - 8;
        var maxVal = Stats.Max(s => Math.Max(s.SuccessCount, s.FailCount));
        if (maxVal == 0) maxVal = 1;

        var chartHeight = dirtyRect.Height - padding * 2 - 18;
        var x = dirtyRect.Left + padding;

        foreach (var stat in Stats)
        {
            var successH = (float)(stat.SuccessCount / (double)maxVal * chartHeight);
            var failH = (float)(stat.FailCount / (double)maxVal * chartHeight);
            var baseY = dirtyRect.Bottom - padding - 18;

            canvas.FillColor = Color.FromArgb("#00E676");
            canvas.FillRectangle(x, baseY - successH, barWidth / 2 - 2, successH);

            canvas.FillColor = Color.FromArgb("#FF5252");
            canvas.FillRectangle(x + barWidth / 2 + 2, baseY - failH, barWidth / 2 - 2, failH);

            canvas.FontColor = Color.FromArgb("#8899AA");
            canvas.FontSize = 10;
            canvas.DrawString(stat.Date.ToString("dd.MM"), x, baseY + 14, barWidth, 14,
                HorizontalAlignment.Center, VerticalAlignment.Top);

            x += slotWidth;
        }
    }
}

public class LoginChartView : GraphicsView
{
    public static readonly BindableProperty StatsProperty =
        BindableProperty.Create(nameof(Stats), typeof(IList<DailyLoginStatProxy>), typeof(LoginChartView), null,
            propertyChanged: OnStatsChanged);

    private readonly LoginChartDrawable _drawable = new();

    public LoginChartView()
    {
        Drawable = _drawable;
    }

    public IList<DailyLoginStatProxy>? Stats
    {
        get => (IList<DailyLoginStatProxy>?)GetValue(StatsProperty);
        set => SetValue(StatsProperty, value);
    }

    private static void OnStatsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (LoginChartView)bindable;
        view._drawable.Stats = (IList<DailyLoginStatProxy>?)newValue;
        view.Invalidate();
    }
}
