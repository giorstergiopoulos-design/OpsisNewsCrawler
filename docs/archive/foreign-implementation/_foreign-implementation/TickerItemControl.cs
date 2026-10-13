using DeskTicker.Core;
using System.Drawing.Drawing2D;

namespace DeskTicker.Windows;

/// <summary>One headline in the strip: source icon + title. Custom painted, no transparency (avoids flicker while scrolling).</summary>
internal sealed class TickerItemControl : Control
{
    private const int IconSize = 16;
    private const int IconGap = 7;
    private const int ItemHeight = 22;

    private static readonly Font TextFont = new("Segoe UI", 10f);
    private static readonly Font HotFont = new("Segoe UI", 10f, FontStyle.Underline);
    private static readonly Font BadgeFont = new("Segoe UI", 7.5f, FontStyle.Bold);
    private static readonly TextFormatFlags TextFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left;
    private static readonly Color[] BadgeColors =
    {
        Color.FromArgb(0, 120, 212), Color.FromArgb(16, 137, 62), Color.FromArgb(202, 80, 16),
        Color.FromArgb(136, 23, 152), Color.FromArgb(0, 153, 188), Color.FromArgb(194, 57, 179),
        Color.FromArgb(218, 59, 1), Color.FromArgb(73, 130, 5)
    };

    public readonly NewsItem Item;
    public Image? Favicon;

    private readonly bool showIcon;
    private readonly string text;
    private bool hot;

    public event Action<TickerItemControl>? Opened;

    public TickerItemControl(NewsItem item, bool showSourceIcon, Color back)
    {
        Item = item;
        var hasLink = !string.IsNullOrEmpty(item.Url);
        showIcon = showSourceIcon && hasLink;
        text = showIcon ? item.Title : $"{item.Source}: {item.Title}";

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = back;
        Cursor = hasLink ? Cursors.Hand : Cursors.Default;
        Margin = new Padding(0, 0, 28, 0);

        var textSize = TextRenderer.MeasureText(text, TextFont, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        Size = new Size((showIcon ? IconSize + IconGap : 0) + textSize.Width + 4, ItemHeight);
    }

    public void SetIcon(Image? image)
    {
        Favicon = image;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hot = !string.IsNullOrEmpty(Item.Url);
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hot = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!string.IsNullOrEmpty(Item.Url)) Opened?.Invoke(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        var x = 0;
        if (showIcon)
        {
            var iconRect = new Rectangle(0, (Height - IconSize) / 2, IconSize, IconSize);
            if (Favicon is not null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Favicon, iconRect);
            }
            else
            {
                DrawBadge(g, iconRect);
            }
            x = IconSize + IconGap;
        }

        TextRenderer.DrawText(g, text, hot ? HotFont : TextFont,
            new Rectangle(x, 0, Width - x, Height), hot ? Color.Gainsboro : Color.White, TextFlags);
    }

    // Fallback while the real icon loads (or when the site has none): coloured circle with the first letter.
    private void DrawBadge(Graphics g, Rectangle rect)
    {
        var source = string.IsNullOrWhiteSpace(Item.Source) ? "?" : Item.Source.Trim();
        var color = BadgeColors[(source.Sum(c => (int)c) % BadgeColors.Length)];
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(color)) g.FillEllipse(brush, rect);
        TextRenderer.DrawText(g, char.ToUpperInvariant(source[0]).ToString(), BadgeFont, rect, Color.White,
            TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
