using DeskTicker.Core;
using System.Drawing.Drawing2D;

namespace DeskTicker.Windows;

/// <summary>Hover card with the picture, source, age, title and summary of a headline. Never takes focus.</summary>
internal sealed class PreviewPopup : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int PopupWidth = 380;
    private const int Pad = 12;
    private const int MaxImageHeight = 190;
    private const int MaxSummaryLines = 5;
    private const int MaxTitleLines = 3;
    private const int IconSize = 16;

    private static readonly Font SourceFont = new("Segoe UI", 8.5f);
    private static readonly Font TitleFont = new("Segoe UI Semibold", 11f);
    private static readonly Font BodyFont = new("Segoe UI", 9.5f);
    private static readonly TextFormatFlags WrapFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top;

    private NewsItem? item;
    private Image? icon;
    private Image? picture;
    private string language = "el";
    private Rectangle tickerBounds;
    private int cursorX;
    private bool below;
    private Rectangle imageRect, headerRect, titleRect, bodyRect;

    public PreviewPopup()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 38);
        DoubleBuffered = true;
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

    public void ShowFor(NewsItem newsItem, Image? sourceIcon, string lang, Rectangle ticker, int mouseX, bool showBelow)
    {
        item = newsItem;
        icon = sourceIcon;
        picture = null;
        language = lang;
        tickerBounds = ticker;
        cursorX = mouseX;
        below = showBelow;

        Relayout();
        if (!Visible) Show();
        Invalidate();
    }

    /// <summary>Called when the picture has been downloaded; ignored if another headline is shown by now.</summary>
    public void SetPicture(NewsItem forItem, Image image)
    {
        if (!Visible || !ReferenceEquals(item, forItem)) return;
        picture = image;
        Relayout();
        Invalidate();
    }

    public void HidePopup()
    {
        item = null;
        picture = null;
        if (Visible) Hide();
    }

    private void Relayout()
    {
        if (item is null) return;
        var width = PopupWidth - 2 * Pad;
        var y = 0;

        if (picture is not null && picture.Width > 0)
        {
            var height = (int)Math.Min(MaxImageHeight, PopupWidth * (double)picture.Height / picture.Width);
            imageRect = new Rectangle(0, 0, PopupWidth, Math.Max(60, height));
            y = imageRect.Height;
        }
        else
        {
            imageRect = Rectangle.Empty;
        }

        y += Pad;
        headerRect = new Rectangle(Pad, y, width, IconSize + 2);
        y += headerRect.Height + 6;

        var titleHeight = TextRenderer.MeasureText(item.Title, TitleFont, new Size(width, 0), WrapFlags).Height;
        titleRect = new Rectangle(Pad, y, width, Math.Min(titleHeight, TitleFont.Height * MaxTitleLines));
        y += titleRect.Height;

        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            y += 8;
            var bodyHeight = TextRenderer.MeasureText(item.Summary, BodyFont, new Size(width, 0), WrapFlags).Height;
            bodyRect = new Rectangle(Pad, y, width, Math.Min(bodyHeight, BodyFont.Height * MaxSummaryLines));
            y += bodyRect.Height;
        }
        else
        {
            bodyRect = Rectangle.Empty;
        }

        y += Pad;
        ClientSize = new Size(PopupWidth, y);

        var area = Screen.FromRectangle(tickerBounds).WorkingArea;
        var x = Math.Clamp(cursorX - PopupWidth / 2, area.Left + 8, Math.Max(area.Left + 8, area.Right - PopupWidth - 8));
        var top = below ? tickerBounds.Bottom + 6 : tickerBounds.Top - y - 6;
        Location = new Point(x, Math.Max(area.Top, top));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (item is null) return;
        var g = e.Graphics;

        if (picture is not null && !imageRect.IsEmpty) DrawCover(g, picture, imageRect);

        var textX = headerRect.Left;
        if (icon is not null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, new Rectangle(headerRect.Left, headerRect.Top, IconSize, IconSize));
            textX += IconSize + 6;
        }

        var age = RelativeTime(item.PublishedAt, language);
        var header = age.Length > 0 ? $"{item.Source}  ·  {age}" : item.Source;
        TextRenderer.DrawText(g, header, SourceFont, new Rectangle(textX, headerRect.Top, headerRect.Right - textX, headerRect.Height),
            Color.FromArgb(170, 170, 182), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(g, item.Title, TitleFont, titleRect, Color.White, WrapFlags | TextFormatFlags.EndEllipsis);

        if (!bodyRect.IsEmpty && item.Summary is not null)
            TextRenderer.DrawText(g, item.Summary, BodyFont, bodyRect, Color.FromArgb(205, 205, 214), WrapFlags | TextFormatFlags.EndEllipsis);

        using var border = new Pen(Color.FromArgb(70, 70, 82));
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    // Fill the rectangle with the picture, cropping the overflow (like CSS object-fit: cover).
    private static void DrawCover(Graphics g, Image img, Rectangle target)
    {
        var targetRatio = (double)target.Width / target.Height;
        var imageRatio = (double)img.Width / img.Height;
        Rectangle source;
        if (imageRatio > targetRatio)
        {
            var w = (int)(img.Height * targetRatio);
            source = new Rectangle((img.Width - w) / 2, 0, w, img.Height);
        }
        else
        {
            var h = (int)(img.Width / targetRatio);
            source = new Rectangle(0, (img.Height - h) / 2, img.Width, h);
        }
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, target, source, GraphicsUnit.Pixel);
    }

    private static string RelativeTime(DateTimeOffset published, string lang)
    {
        if (published == DateTimeOffset.MinValue) return "";
        var span = DateTimeOffset.UtcNow - published;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var greek = lang != "en";

        if (span.TotalMinutes < 1) return greek ? "τώρα" : "just now";
        if (span.TotalMinutes < 60) return greek ? $"πριν {(int)span.TotalMinutes} λεπτά" : $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return greek ? $"πριν {(int)span.TotalHours} ώρες" : $"{(int)span.TotalHours} h ago";
        return greek ? $"πριν {(int)span.TotalDays} ημέρες" : $"{(int)span.TotalDays} d ago";
    }
}
