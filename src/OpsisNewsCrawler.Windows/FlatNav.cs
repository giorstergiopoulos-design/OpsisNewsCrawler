using System.Drawing.Drawing2D;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The settings navigation: one left rail with group headings (Settings / Sources / Information) and the pages under them,
/// a search box on top and a titled content pane on the right. It replaces the two rows of "pills" the settings window used to have.
/// The rail scrolls when a translation makes it longer than the window.
/// </summary>
internal sealed class FlatNav : Control
{
    private const int RailWidth = 250;
    private const int SearchTop = 12;
    private const int ListTop = 56;
    private const int ItemHeight = 36;
    private const int GroupHeight = 34;
    private const int TitleHeight = 54;
    private static readonly Font GroupFont = new("Segoe UI Semibold", 8.5f);
    private static readonly Font TitleFont = new("Segoe UI Semibold", 13f);

    private sealed record Entry(string Title, Control? Page, int Leaf);

    private readonly List<Entry> entries = new();
    private readonly List<Control> pages = new();
    private readonly TextBox search;
    private readonly List<(Rectangle Rect, int Entry)> hits = new();
    private int selected = -1;     // leaf index of the shown page
    private int hover = -1;        // entry index under the pointer
    private int scrollY;
    private string filter = "";

    public event EventHandler? SelectedIndexChanged;

    public FlatNav()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9.5f);
        search = new TextBox
        {
            Size = new Size(RailWidth - 28, 27), BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = Loc.T("settings.search"), Font = new Font("Segoe UI", 9f), Tag = "surface"
        };
        search.TextChanged += (_, _) =>
        {
            filter = search.Text.Trim();
            scrollY = 0;
            var firstMatch = entries.FirstOrDefault(e => e.Page is not null && Matches(e));
            if (selected >= 0 && !Matches(entries.First(e => e.Leaf == selected)) && firstMatch is not null) SelectedIndex = firstMatch.Leaf;
            Invalidate();
        };
        Controls.Add(search);
    }

    private bool Rtl => RightToLeft == RightToLeft.Yes;
    private int RailLeft => Rtl ? Math.Max(0, Width - RailWidth) : 0;
    private int ContentLeft => Rtl ? 0 : RailWidth + 1;
    private int ContentWidth => Math.Max(0, Width - RailWidth - 1);

    /// <summary>Number of pages (group headings do not count).</summary>
    public int Count => pages.Count;

    public string SelectedTitle => selected >= 0 ? entries.First(e => e.Leaf == selected).Title : "";

    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value < 0 || value >= pages.Count || value == selected) return;
            selected = value;
            for (var i = 0; i < pages.Count; i++) pages[i].Visible = i == value;
            LayoutPages();
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AddGroup(string title) => entries.Add(new Entry(title, null, -1));

    /// <summary>Adds a page under the last group; returns its index.</summary>
    public int AddPage(string title, Control page)
    {
        var leaf = pages.Count;
        page.Dock = DockStyle.None;
        page.Visible = false;
        pages.Add(page);
        entries.Add(new Entry(title, page, leaf));
        Controls.Add(page);
        if (selected < 0) SelectedIndex = 0;
        Invalidate();
        return leaf;
    }

    private bool Matches(Entry e) => filter.Length == 0 || e.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    private IEnumerable<int> VisibleEntries()
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Page is not null) { if (Matches(e)) yield return i; continue; }
            // a group heading stays only while one of its pages matches
            var any = false;
            for (var j = i + 1; j < entries.Count && entries[j].Page is not null; j++) if (Matches(entries[j])) { any = true; break; }
            if (any) yield return i;
        }
    }

    private int ListHeight => VisibleEntries().Sum(i => entries[i].Page is null ? GroupHeight : ItemHeight);

    private void Measure()
    {
        hits.Clear();
        var view = Math.Max(ItemHeight, Height - ListTop - 10);
        scrollY = Math.Clamp(scrollY, 0, Math.Max(0, ListHeight - view));
        var y = ListTop - scrollY;
        foreach (var i in VisibleEntries())
        {
            var h = entries[i].Page is null ? GroupHeight : ItemHeight;
            hits.Add((new Rectangle(RailLeft + 10, y, RailWidth - 20, h), i));
            y += h;
        }
    }

    private void LayoutPages()
    {
        search.Location = new Point(RailLeft + 14, SearchTop);
        foreach (var p in pages) p.SetBounds(ContentLeft, TitleHeight, ContentWidth, Math.Max(0, Height - TitleHeight));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutPages();
        Invalidate();
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        LayoutPages();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.X < RailLeft || e.X >= RailLeft + RailWidth) return;
        scrollY -= Math.Sign(e.Delta) * ItemHeight * 2;
        Measure();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = -1;
        foreach (var (rect, entry) in hits) if (entries[entry].Page is not null && rect.Contains(e.Location)) h = entry;
        if (h == hover) return;
        hover = h;
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        foreach (var (rect, entry) in hits)
            if (entries[entry].Page is { } && rect.Contains(e.Location)) { SelectedIndex = entries[entry].Leaf; return; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { search.Focus(); search.SelectAll(); return true; }
        if (keyData == Keys.Escape && search.Focused && search.TextLength > 0) { search.Clear(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);
        Measure();
        search.BackColor = Theme.Surface;
        search.ForeColor = Theme.Text;
        var rtlFlag = Rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;

        using (var rail = new SolidBrush(Theme.Surface)) g.FillRectangle(rail, RailLeft, 0, RailWidth, Height);
        using (var line = new Pen(Theme.Border))
        {
            var x = Rtl ? RailLeft : RailWidth;
            g.DrawLine(line, x, 0, x, Height);
        }

        var clip = g.Save();
        g.SetClip(new Rectangle(RailLeft, ListTop - 4, RailWidth, Math.Max(0, Height - ListTop + 4)));
        foreach (var (rect, i) in hits)
        {
            var entry = entries[i];
            if (rect.Bottom < ListTop - 4 || rect.Top > Height) continue;
            if (entry.Page is null)
            {
                var text = new Rectangle(rect.Left + 8, rect.Top + 8, rect.Width - 12, rect.Height - 8);
                TextRenderer.DrawText(g, Loc.Upper(entry.Title), GroupFont, text, Theme.Accent,
                    rtlFlag | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                continue;
            }
            var active = entry.Leaf == selected;
            var body = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - 2);
            if (active)
            {
                using var path = FlatDraw.Rounded(body, 8);
                using var brush = new SolidBrush(FlatDraw.SurfaceHover);
                g.FillPath(brush, path);
                using var pen = new Pen(Theme.Accent, 3f);
                var bx = Rtl ? body.Right - 2 : body.Left + 2;
                g.DrawLine(pen, bx, body.Top + 8, bx, body.Bottom - 8);
            }
            else if (i == hover)
            {
                using var path = FlatDraw.Rounded(body, 8);
                using var brush = new SolidBrush(FlatDraw.Blend(Theme.Surface, Theme.Text, Theme.IsDark ? .06 : .04));
                g.FillPath(brush, path);
            }
            var textRect = new Rectangle(body.Left + 16, body.Top, body.Width - 28, body.Height);
            TextRenderer.DrawText(g, entry.Title, Font, textRect, active ? Theme.Text : Theme.TextMuted,
                rtlFlag | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        if (hits.Count == 0)
            TextRenderer.DrawText(g, Loc.T("settings.noResults"), Font, new Rectangle(RailLeft + 14, ListTop + 4, RailWidth - 28, 44), Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        g.Restore(clip);

        // the title of the page that is shown
        if (selected >= 0)
        {
            var title = new Rectangle(ContentLeft + 22, 10, Math.Max(0, ContentWidth - 44), 34);
            TextRenderer.DrawText(g, SelectedTitle, TitleFont, title, Theme.Text,
                rtlFlag | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            using var line = new Pen(Theme.Border);
            g.DrawLine(line, ContentLeft + 18, TitleHeight - 2, ContentLeft + ContentWidth - 18, TitleHeight - 2);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var p in pages) p.Dispose();
        base.Dispose(disposing);
    }
}
