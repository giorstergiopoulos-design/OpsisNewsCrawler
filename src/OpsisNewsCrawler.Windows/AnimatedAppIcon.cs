namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The animated icon of the program (the little bar on which the news, sports, defence &amp; diplomacy and economy symbols pass from right to
/// left) as a control: shown in the header of the settings, both in the program's own window and on the page inside Opsis.
/// It moves only while <c>animateWhen</c> says so (the same condition as the tray icon); otherwise it shows the still picture with all four symbols.
/// </summary>
internal sealed class AnimatedAppIcon : Control
{
    private const int FrameCount = 96;   // 96 frames at 40 ms: a ~3.8 s loop, 25 fps
    private readonly Func<bool> animateWhen;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    private Bitmap[]? frames;
    private Bitmap? still;
    private int frame;
    private int renderedSize;

    public AnimatedAppIcon(Func<bool> animateWhen, int size = 32)
    {
        this.animateWhen = animateWhen;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(AppIconArt.WideWidthFor(size), size);
        timer.Tick += (_, _) =>
        {
            if (!Visible || !IsHandleCreated || !animateWhen()) return;
            frame = (frame + 1) % FrameCount;
            Invalidate();
        };
        timer.Start();
    }

    private void EnsureFrames()
    {
        var size = Math.Max(16, Height);
        if (renderedSize == size * 10000 + Width && still is not null) return;
        DisposeFrames();
        renderedSize = size * 10000 + Width;
        still = AppIconArt.RenderWide(Width, size, 0, showAllFour: true);
        frames = Enumerable.Range(0, FrameCount).Select(i => AppIconArt.RenderWide(Width, size, i / (double)FrameCount, showAllFour: false)).ToArray();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureFrames();
        var picture = animateWhen() ? frames![frame] : still!;
        e.Graphics.DrawImage(picture, (Width - picture.Width) / 2, (Height - picture.Height) / 2, picture.Width, picture.Height);
    }

    private void DisposeFrames()
    {
        still?.Dispose();
        still = null;
        if (frames is null) return;
        foreach (var f in frames) f.Dispose();
        frames = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            DisposeFrames();
        }
        base.Dispose(disposing);
    }
}
