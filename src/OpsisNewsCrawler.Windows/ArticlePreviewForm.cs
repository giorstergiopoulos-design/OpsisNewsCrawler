using OpsisNewsCrawler.Core;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Small card that appears next to the ticker while the mouse rests on a news item:
/// picture, source and time, title and the first lines of the article. It never takes focus.
/// </summary>
internal sealed class ArticlePreviewForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int CardWidth = 400;
    private const int Pad = 14;
    private const int ImageHeight = 170;
    private const int MaxSummaryLines = 6;
    private const int MaxImageBytes = 4 * 1024 * 1024;
    private const int MaxCachedImages = 24;

    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, Bitmap> ImageCache = new();
    private static readonly Queue<string> ImageOrder = new();

    private const TextFormatFlags WrapFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
    private const TextFormatFlags LineFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

    private readonly Font metaFont = new("Segoe UI", 8.5f);
    private readonly Font titleFont = new("Segoe UI Semibold", 11f);
    private readonly Font bodyFont = new("Segoe UI", 9.5f);

    private NewsItem? item;
    private Bitmap? image;
    private bool imageLoading;
    private Rectangle anchor;
    private bool above = true;
    private CancellationTokenSource? cts;

    private Rectangle imageRect, metaRect, titleRect, bodyRect, hintRect;
    private string meta = "";
    private string body = "";

    public ArticlePreviewForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Surface;
        DoubleBuffered = true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler/0.6)");
        return client;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>Shows the card for an item. <paramref name="anchorScreen"/> is the item's rectangle in screen coordinates.</summary>
    public void ShowFor(NewsItem newItem, Rectangle anchorScreen, bool preferAbove)
    {
        CancelImageLoad();
        BackColor = Theme.Surface;
        item = newItem;
        anchor = anchorScreen;
        above = preferAbove;
        image = null;
        imageLoading = false;

        var time = newItem.PublishedAt == DateTimeOffset.MinValue
            ? ""
            : " · " + newItem.PublishedAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        meta = newItem.Source + time;
        body = newItem.Summary is { Length: > 0 } s && !s.StartsWith(newItem.Title, StringComparison.OrdinalIgnoreCase) ? s : "";

        if (!string.IsNullOrEmpty(newItem.ImageUrl))
        {
            if (ImageCache.TryGetValue(newItem.ImageUrl, out var cached))
            {
                image = cached;
            }
            else
            {
                imageLoading = true;
                cts = new CancellationTokenSource();
                _ = LoadImageAsync(newItem, cts.Token);
            }
        }

        ComputeLayout();
        PlaceNearAnchor();
        if (!Visible) Show();
        else Invalidate();
    }

    public void HidePreview()
    {
        CancelImageLoad();
        item = null;
        if (Visible) Hide();
    }

    private void CancelImageLoad()
    {
        if (cts is null) return;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    private async Task LoadImageAsync(NewsItem forItem, CancellationToken token)
    {
        Bitmap? bmp = null;
        try
        {
            using var response = await Http.GetAsync(forItem.ImageUrl!, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxImageBytes) throw new InvalidDataException("image too large");

            var data = await response.Content.ReadAsByteArrayAsync(token);
            if (data.Length > MaxImageBytes) throw new InvalidDataException("image too large");

            using var ms = new MemoryStream(data);
            using var decoded = Image.FromStream(ms);
            bmp = new Bitmap(decoded);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException
                                       or ArgumentException or OutOfMemoryException or IOException)
        {
            bmp = null; // formats GDI+ cannot read (e.g. WebP) simply have no picture
        }

        if (token.IsCancellationRequested || IsDisposed || !ReferenceEquals(item, forItem))
        {
            bmp?.Dispose();
            return;
        }

        if (bmp is not null) RememberImage(forItem.ImageUrl!, bmp);
        image = bmp;
        imageLoading = false;
        ComputeLayout();
        PlaceNearAnchor();
        Invalidate();
    }

    private static void RememberImage(string url, Bitmap bmp)
    {
        if (ImageCache.ContainsKey(url)) return;
        ImageCache[url] = bmp;
        ImageOrder.Enqueue(url);
        while (ImageOrder.Count > MaxCachedImages) ImageCache.Remove(ImageOrder.Dequeue());
    }

    private void ComputeLayout()
    {
        if (item is null) return;

        var w = CardWidth - 2 * Pad;
        var y = Pad;

        if (!string.IsNullOrEmpty(item.ImageUrl) && (image is not null || imageLoading))
        {
            imageRect = new Rectangle(0, 0, CardWidth, ImageHeight);
            y = ImageHeight + Pad;
        }
        else
        {
            imageRect = Rectangle.Empty;
        }

        var metaHeight = TextRenderer.MeasureText(meta, metaFont, new Size(w, 100), LineFlags).Height;
        metaRect = new Rectangle(Pad, y, w, metaHeight);
        y += metaHeight + 6;

        var titleHeight = TextRenderer.MeasureText(item.Title, titleFont, new Size(w, 2000), WrapFlags).Height;
        titleRect = new Rectangle(Pad, y, w, titleHeight);
        y += titleHeight + 8;

        if (body.Length > 0)
        {
            var bodyHeight = Math.Min(
                TextRenderer.MeasureText(body, bodyFont, new Size(w, 2000), WrapFlags).Height,
                bodyFont.Height * MaxSummaryLines);
            bodyRect = new Rectangle(Pad, y, w, bodyHeight);
            y += bodyHeight + 10;
        }
        else
        {
            bodyRect = Rectangle.Empty;
        }

        var hintHeight = metaFont.Height;
        hintRect = new Rectangle(Pad, y, w, hintHeight);
        y += hintHeight + Pad;

        ClientSize = new Size(CardWidth, y);
    }

    private void PlaceNearAnchor()
    {
        var screen = Screen.FromRectangle(anchor);
        var wa = screen.WorkingArea;

        var x = anchor.Left + anchor.Width / 2 - Width / 2;
        x = Math.Clamp(x, wa.Left + 8, Math.Max(wa.Left + 8, wa.Right - Width - 8));

        var y = above ? anchor.Top - Height - 8 : anchor.Bottom + 8;
        if (y < wa.Top) y = anchor.Bottom + 8;
        if (y + Height > screen.Bounds.Bottom) y = Math.Max(screen.Bounds.Top, screen.Bounds.Bottom - Height - 8);

        Location = new Point(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        using (var border = new Pen(Theme.Border))
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        if (item is null) return;

        if (!imageRect.IsEmpty)
        {
            if (image is not null) DrawCover(g, image, imageRect);
            else
            {
                using var placeholder = new SolidBrush(Theme.Background);
                g.FillRectangle(placeholder, imageRect);
            }
        }

        TextRenderer.DrawText(g, meta, metaFont, metaRect, Theme.IsDark ? Color.FromArgb(125, 190, 255) : Color.FromArgb(0, 95, 170), LineFlags);
        TextRenderer.DrawText(g, item.Title, titleFont, titleRect, Theme.Text, WrapFlags);
        if (!bodyRect.IsEmpty)
            TextRenderer.DrawText(g, body, bodyFont, bodyRect, Theme.TextMuted, WrapFlags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, Loc.T("pv.click"), metaFont, hintRect, Theme.TextMuted, LineFlags);
    }

    private static void DrawCover(Graphics g, Bitmap img, Rectangle dest)
    {
        // Fill the area and crop the overflow from the centre, like CSS "object-fit: cover".
        var srcAspect = (double)img.Width / img.Height;
        var dstAspect = (double)dest.Width / dest.Height;
        Rectangle src;
        if (srcAspect > dstAspect)
        {
            var w = (int)(img.Height * dstAspect);
            src = new Rectangle((img.Width - w) / 2, 0, w, img.Height);
        }
        else
        {
            var h = (int)(img.Width / dstAspect);
            src = new Rectangle(0, (img.Height - h) / 2, img.Width, h);
        }

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, dest, src, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelImageLoad();
            metaFont.Dispose();
            titleFont.Dispose();
            bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
