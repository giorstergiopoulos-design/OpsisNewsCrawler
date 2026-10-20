using OpsisNewsCrawler.Core;
using System.Drawing.Drawing2D;

namespace OpsisNewsCrawler.Windows;

// Controls drawn like the Opsis - Desktop Customization UI: rounded dark surfaces, a thin border, accent colour for the
// active state, no classic Windows 3D look. Ported from Opsis' FlatCheckBox / FlatComboBox / FlatSlider / HoverButton /
// NavButton (same MIT project) and wired to <see cref="Theme"/>, so they follow the host's colours.

internal static class FlatDraw
{
    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>The Surface colour moved a little towards the text colour: hover state of rows and buttons.</summary>
    public static Color SurfaceHover => Blend(Theme.Surface, Theme.Text, Theme.IsDark ? 0.10 : 0.07);

    public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}

/// <summary>Pill-shaped button (Opsis HoverButton). Primary = filled with the accent colour.</summary>
internal sealed class FlatButton : Button
{
    private bool hot;
    private bool primary;

    /// <summary>True for buttons that only switch pages: clicking them never counts as an edit of the settings.</summary>
    public bool IsNavigation { get; set; }

    public FlatButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.Selectable, false);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(16, 6, 16, 6);
        Margin = new Padding(0, 3, 8, 3);
    }

    public void SetPrimary(bool value)
    {
        primary = value;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(text.Width + Padding.Horizontal, Math.Max(32, text.Height + Padding.Vertical));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = FlatDraw.Rounded(rect, Height / 2);
        var fill = primary ? (hot ? FlatDraw.Blend(Theme.Accent, Color.White, 0.15) : Theme.Accent)
                           : (hot ? FlatDraw.SurfaceHover : Theme.Surface);
        using (var brush = new SolidBrush(Enabled ? fill : Theme.Surface)) g.FillPath(brush, path);
        if (!primary)
        {
            using var pen = new Pen(hot && Enabled ? Theme.Accent : Theme.Border);
            g.DrawPath(pen, path);
        }
        var color = !Enabled ? Theme.TextMuted : primary ? Color.White : Theme.Text;
        TextRenderer.DrawText(g, Text, Font, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>Rounded check box with the accent-filled tick (Opsis FlatCheckBox).</summary>
internal sealed class FlatCheck : CheckBox
{
    private const int BoxSize = 18;
    private bool hot;

    public FlatCheck()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Font = new Font("Segoe UI", 9.5f);
        AutoSize = false;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 4, 0, 4);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text)) return new Size(BoxSize, BoxSize);
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(BoxSize + 10 + text.Width, Math.Max(BoxSize + 4, text.Height + 4));
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels

        var box = new Rectangle(0, (Height - BoxSize) / 2, BoxSize, BoxSize);
        using var path = FlatDraw.Rounded(box, 5);
        if (Checked)
        {
            using var fill = new SolidBrush(Enabled ? Theme.Accent : Theme.TextMuted);
            g.FillPath(fill, path);
            using var tick = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(tick, new[]
            {
                new PointF(box.X + BoxSize * 0.22f, box.Y + BoxSize * 0.52f),
                new PointF(box.X + BoxSize * 0.42f, box.Y + BoxSize * 0.74f),
                new PointF(box.X + BoxSize * 0.80f, box.Y + BoxSize * 0.28f)
            });
        }
        else
        {
            using var fill = new SolidBrush(Theme.Background);
            g.FillPath(fill, path);
            using var pen = new Pen(hot && Enabled ? Theme.Accent : Theme.TextMuted, 1.5f);
            g.DrawPath(pen, path);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(BoxSize + 10, 0, Math.Max(0, Width - BoxSize - 10), Height),
                Enabled ? Theme.Text : Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }
}

/// <summary>Rounded "button" that opens a flat drop-down list (Opsis FlatComboBox).</summary>
internal sealed class FlatCombo : Panel
{
    private readonly List<string> items = new();
    private int selected = -1;
    private bool hot;
    private bool open;

    public event EventHandler? SelectedIndexChanged;

    public FlatCombo()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Height = 32;
        Width = 260;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        Margin = new Padding(0, 3, 0, 3);
        MouseEnter += (_, _) => { hot = true; Invalidate(); };
        MouseLeave += (_, _) => { hot = false; Invalidate(); };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) OpenDropdown(); };
    }

    public int Count => items.Count;

    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value == selected) return;
            selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Replaces the items without raising SelectedIndexChanged.</summary>
    public void SetItems(IEnumerable<string> newItems, int selectedIndex)
    {
        items.Clear();
        items.AddRange(newItems);
        selected = selectedIndex < items.Count ? selectedIndex : -1;
        Invalidate();
    }

    /// <summary>Changes the selection without raising SelectedIndexChanged.</summary>
    public void SelectSilently(int index)
    {
        selected = index < items.Count ? index : -1;
        Invalidate();
    }

    private void OpenDropdown()
    {
        if (items.Count == 0 || !Enabled) return;
        open = true;
        Invalidate();

        var itemHeight = TextRenderer.MeasureText("Ag", Font).Height + 10;
        var menu = new ContextMenuStrip { Renderer = new FlatMenuRenderer(), BackColor = Theme.Surface, Font = Font, ShowImageMargin = false };
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(items[i])
            {
                Checked = index == selected, ForeColor = Theme.Text, AutoSize = false, Size = new Size(Width, itemHeight)
            };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }
        menu.AutoSize = false;
        menu.Size = new Size(Width, itemHeight * items.Count + 4);
        // No Dispose() here: the drop-down is still inside its own closing code when Closed is raised (same as Opsis).
        menu.Closed += (_, _) => { open = false; Invalidate(); };
        menu.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = FlatDraw.Rounded(rect, 7))
        {
            using var brush = new SolidBrush(open || hot ? FlatDraw.SurfaceHover : Theme.Surface);
            g.FillPath(brush, path);
            using var pen = new Pen(open ? Theme.Accent : Theme.Border);
            g.DrawPath(pen, path);
        }

        var text = selected >= 0 && selected < items.Count ? items[selected] : "";
        TextRenderer.DrawText(g, text, Font, new Rectangle(12, 0, Width - 38, Height), Enabled ? Theme.Text : Theme.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        using var chevron = new Pen(Theme.TextMuted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cx = Width - 18, cy = Height / 2f - 1.5f;
        g.DrawLines(chevron, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
    }
}

/// <summary>Flat look for the drop-down menus (Opsis FlatMenuRenderer).</summary>
internal sealed class FlatMenuRenderer : ToolStripProfessionalRenderer
{
    public FlatMenuRenderer() : base(new FlatColors()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected && e.Item is ToolStripMenuItem ? Theme.Accent : Theme.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Accent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var r = e.ImageRectangle;
        g.DrawLines(pen, new[] { new PointF(r.Left + 2, r.Top + r.Height / 2f), new PointF(r.Left + r.Width / 2.5f, r.Bottom - 3), new PointF(r.Right - 2, r.Top + 2) });
    }

    private sealed class FlatColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => FlatDraw.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => FlatDraw.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => FlatDraw.SurfaceHover;
        public override Color MenuItemBorder => FlatDraw.SurfaceHover;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}

/// <summary>Thin rounded track with an accent fill and a round thumb (Opsis FlatSlider).</summary>
internal sealed class FlatSlider : Control
{
    private const float ThumbRadius = 7f;
    private const float TrackHeight = 4f;
    private int minimum;
    private int maximum = 100;
    private int value;
    private bool dragging;

    public event EventHandler? ValueChanged;

    public FlatSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = 26;
        Width = 300;
        Cursor = Cursors.Hand;
    }

    public void SetRange(int min, int max)
    {
        minimum = min;
        maximum = max;
        Value = Math.Clamp(value, min, max);
        Invalidate();
    }

    public int Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, minimum, maximum);
            if (clamped == this.value) return;
            this.value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private float ThumbX
    {
        get
        {
            var usable = Width - ThumbRadius * 2;
            var frac = maximum > minimum ? (double)(value - minimum) / (maximum - minimum) : 0;
            return ThumbRadius + (float)(frac * usable);
        }
    }

    private int ValueFromX(int x)
    {
        var usable = Width - ThumbRadius * 2;
        if (usable <= 0) return minimum;
        var frac = Math.Clamp((x - ThumbRadius) / usable, 0, 1);
        return minimum + (int)Math.Round(frac * (maximum - minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        dragging = true;
        Value = ValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) Value = ValueFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels

        var midY = Height / 2f;
        var track = new Rectangle((int)ThumbRadius, (int)(midY - TrackHeight / 2), Math.Max(1, (int)(Width - ThumbRadius * 2)), (int)TrackHeight);
        using (var path = FlatDraw.Rounded(track, 2)) using (var brush = new SolidBrush(Theme.Border)) g.FillPath(brush, path);

        var thumbX = ThumbX;
        var fill = new Rectangle((int)ThumbRadius, (int)(midY - TrackHeight / 2), Math.Max(0, (int)(thumbX - ThumbRadius)), (int)TrackHeight);
        if (fill.Width > 1)
            using (var path = FlatDraw.Rounded(fill, 2)) using (var brush = new SolidBrush(Enabled ? Theme.Accent : Theme.TextMuted)) g.FillPath(brush, path);

        using (var brush = new SolidBrush(Theme.Surface)) g.FillEllipse(brush, thumbX - ThumbRadius, midY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
        using (var pen = new Pen(Enabled ? Theme.Accent : Theme.TextMuted, 2f))
            g.DrawEllipse(pen, thumbX - ThumbRadius + 1, midY - ThumbRadius + 1, ThumbRadius * 2 - 2, ThumbRadius * 2 - 2);
    }
}

/// <summary>Number box with two small arrows, drawn flat (replaces NumericUpDown).</summary>
internal sealed class FlatNumber : Panel
{
    private int minimum;
    private int maximum = 100;
    private int increment = 1;
    private int value;
    private readonly TextBox box = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Left };
    private bool hot;

    public event EventHandler? ValueChanged;

    public FlatNumber()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 32;
        Width = 130;
        Font = new Font("Segoe UI", 9.5f);
        Margin = new Padding(0, 3, 0, 3);
        box.Font = Font;
        box.Location = new Point(10, 7);
        box.Width = Width - 38;
        Controls.Add(box);
        box.TextChanged += (_, _) => ParseBox();
        box.Leave += (_, _) => ShowValue();
        box.MouseWheel += (_, e) => Step(Math.Sign(e.Delta));
        MouseEnter += (_, _) => { hot = true; Invalidate(); };
        MouseLeave += (_, _) => { hot = false; Invalidate(); };
        MouseDown += OnArrowClick;
    }

    public void Configure(int min, int max, int step)
    {
        minimum = min;
        maximum = max;
        increment = step;
        SetValueSilently(value);
    }

    public int Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, minimum, maximum);
            if (clamped == this.value)
            {
                ShowValue();
                return;
            }
            this.value = clamped;
            ShowValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the value without raising ValueChanged.</summary>
    public void SetValueSilently(int newValue)
    {
        value = Math.Clamp(newValue, minimum, maximum);
        ShowValue();
    }

    private void ShowValue()
    {
        if (box.Text != value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            box.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ParseBox()
    {
        if (!int.TryParse(box.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v)) return;
        var clamped = Math.Clamp(v, minimum, maximum);
        if (clamped == value) return;
        value = clamped;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Step(int direction)
    {
        if (direction != 0) Value = value + direction * increment;
    }

    private void OnArrowClick(object? sender, MouseEventArgs e)
    {
        if (e.X < Width - 26) return;
        Step(e.Y < Height / 2 ? 1 : -1);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        box.Width = Math.Max(10, Width - 38);
        box.Top = (Height - box.Height) / 2;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels
        box.BackColor = hot ? FlatDraw.SurfaceHover : Theme.Surface;
        box.ForeColor = Theme.Text;

        using (var path = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 7))
        {
            using var brush = new SolidBrush(hot ? FlatDraw.SurfaceHover : Theme.Surface);
            g.FillPath(brush, path);
            using var pen = new Pen(box.Focused ? Theme.Accent : Theme.Border);
            g.DrawPath(pen, path);
        }

        using var arrow = new Pen(Theme.TextMuted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cx = Width - 14;
        g.DrawLines(arrow, new[] { new PointF(cx - 4, Height / 2f - 3), new PointF(cx, Height / 2f - 7), new PointF(cx + 4, Height / 2f - 3) });
        g.DrawLines(arrow, new[] { new PointF(cx - 4, Height / 2f + 3), new PointF(cx, Height / 2f + 7), new PointF(cx + 4, Height / 2f + 3) });
    }
}

/// <summary>Single-line text box inside a rounded flat frame.</summary>
internal sealed class FlatText : Panel
{
    private readonly TextBox box = new() { BorderStyle = BorderStyle.None };

    public FlatText()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 32;
        Width = 320;
        Font = new Font("Segoe UI", 9.5f);
        Margin = new Padding(0, 3, 8, 3);
        box.Font = Font;
        box.Location = new Point(10, 7);
        Controls.Add(box);
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
        box.TextChanged += (_, _) => TextValueChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? TextValueChanged;

    public string Value
    {
        get => box.Text;
        set => box.Text = value;
    }

    public void FocusInner() => box.Focus();

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        box.Width = Math.Max(10, Width - 20);
        box.Top = (Height - box.Height) / 2;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels
        box.BackColor = Theme.Surface;
        box.ForeColor = Theme.Text;
        using var path = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 7);
        using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, path);
        using var pen = new Pen(box.Focused ? Theme.Accent : Theme.Border);
        g.DrawPath(pen, path);
    }
}

/// <summary>List with flat rounded rows, optional tick boxes (Opsis look).</summary>
internal sealed class FlatList : ListBox
{
    private readonly List<bool> checks = new();
    private bool showChecks;

    public event Action<int, bool>? ItemChecked;

    public FlatList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 30;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        Font = new Font("Segoe UI", 9.5f);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void SetShowChecks(bool value)
    {
        showChecks = value;
        Invalidate();
    }

    public void AddRow(string text, bool isChecked = false)
    {
        Items.Add(text);
        checks.Add(isChecked);
    }

    public void ClearRows()
    {
        Items.Clear();
        checks.Clear();
    }

    public bool GetChecked(int index) => index >= 0 && index < checks.Count && checks[index];

    public void SetChecked(int index, bool value)
    {
        if (index < 0 || index >= checks.Count || checks[index] == value) return;
        checks[index] = value;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!showChecks) return;
        var index = IndexFromPoint(e.Location);
        if (index < 0 || e.X > 34) return;
        checks[index] = !checks[index];
        Invalidate();
        ItemChecked?.Invoke(index, checks[index]);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        using (var back = new SolidBrush(Theme.Surface)) g.FillRectangle(back, e.Bounds);
        if (e.Index < 0 || e.Index >= Items.Count) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var selected = (e.State & DrawItemState.Selected) != 0;
        var row = Rectangle.Inflate(e.Bounds, -4, -2);
        if (selected)
        {
            using var path = FlatDraw.Rounded(row, 6);
            using var brush = new SolidBrush(FlatDraw.SurfaceHover);
            g.FillPath(brush, path);
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, row.X, row.Y + 6, 3, row.Height - 12);
        }

        var x = row.X + 10;
        if (showChecks)
        {
            var box = new Rectangle(x, row.Y + (row.Height - 16) / 2, 16, 16);
            using var path = FlatDraw.Rounded(box, 4);
            if (checks[e.Index])
            {
                using var fill = new SolidBrush(Theme.Accent);
                g.FillPath(fill, path);
                using var tick = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(tick, new[] { new PointF(box.X + 3.5f, box.Y + 8.5f), new PointF(box.X + 6.8f, box.Y + 12f), new PointF(box.X + 12.5f, box.Y + 4.5f) });
            }
            else
            {
                using var fill = new SolidBrush(Theme.Background);
                g.FillPath(fill, path);
                using var pen = new Pen(Theme.TextMuted, 1.4f);
                g.DrawPath(pen, path);
            }
            x += 26;
        }

        TextRenderer.DrawText(g, Items[e.Index]?.ToString() ?? "", Font, new Rectangle(x, row.Y, row.Right - x, row.Height),
            showChecks && !checks[e.Index] ? Theme.TextMuted : Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>Rounded card with a heading (replaces GroupBox).</summary>
internal sealed class FlatCard : Panel
{
    private readonly Label heading = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Panel body = new() { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent };

    /// <summary>The common width of all cards (px). Content that is wider makes its card grow, so keep controls inside it.</summary>
    public const int CardWidth = 780;

    public FlatCard(string title)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18, 34, 18, 14);
        Margin = new Padding(0, 8, 0, 8);
        MinimumSize = new Size(CardWidth, 0); // every card of every page has the same width
        heading.Text = Loc.Upper(title);
        heading.Location = new Point(18, 12);
        base.Controls.Add(heading);
        base.Controls.Add(body);
    }

    public new Control.ControlCollection Controls => body.Controls;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels
        using var path = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8);
        using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, path);
        using var pen = new Pen(Theme.Border);
        g.DrawPath(pen, path);
        heading.ForeColor = Theme.Accent;
        heading.BackColor = Theme.Surface;
    }
}

/// <summary>
/// Settings navigation with a fixed left rail and a titled content pane. The navigation scrolls vertically,
/// so every page remains discoverable even when the host window is narrow or translated into a longer language.
/// </summary>
internal sealed class FlatTabs : Control
{
    private const int SidebarWidth = 196;
    private const int HeaderHeight = 58;
    private const int NavTop = 116;
    private const int NavItemHeight = 38;
    private static readonly Font BrandFont = new("Segoe UI Semibold", 12f);
    private static readonly Font PageTitleFont = new("Segoe UI Semibold", 12f);
    private readonly List<(string Title, Control Page)> pages = new();
    private readonly List<Rectangle> hitRects = new();
    private readonly TextBox searchBox;
    private int selected = -1;
    private int hover = -1;
    private int scrollY;
    private string searchText = string.Empty;

    public FlatTabs()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9.5f);
        Cursor = Cursors.Default;
        searchBox = new TextBox
        {
            Location = new Point(12, 76), Size = new Size(SidebarWidth - 24, 27),
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = Loc.T("settings.search"),
            Font = new Font("Segoe UI", 9f), Tag = "surface"
        };
        searchBox.TextChanged += (_, _) =>
        {
            searchText = searchBox.Text.Trim();
            scrollY = 0;
            if (selected >= 0 && !Matches(selected) && FilteredIndices().Count > 0)
                SelectedIndex = FilteredIndices()[0];
            Measure();
            Invalidate();
        };
        Controls.Add(searchBox);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        {
            searchBox.Focus();
            searchBox.SelectAll();
            return true;
        }
        if (keyData == Keys.Escape && searchBox.Focused && searchBox.TextLength > 0)
        {
            searchBox.Clear();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool Matches(int index) => string.IsNullOrWhiteSpace(searchText)
        || pages[index].Title.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);

    private List<int> FilteredIndices() => Enumerable.Range(0, pages.Count).Where(Matches).ToList();

    public int Count => pages.Count;

    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value < 0 || value >= pages.Count || value == selected) return;
            selected = value;
            for (var i = 0; i < pages.Count; i++) pages[i].Page.Visible = i == selected;
            EnsureVisible(selected);
            Invalidate();
        }
    }

    public void AddPage(string title, Control page)
    {
        page.Dock = DockStyle.None;
        page.Visible = false;
        pages.Add((title, page));
        Controls.Add(page);
        LayoutPages();
        if (selected < 0) SelectedIndex = 0;
        Invalidate();
    }

    private int VisibleNavHeight => Math.Max(NavItemHeight, Height - NavTop - 12);
    private int MaxScroll => Math.Max(0, FilteredIndices().Count * NavItemHeight - VisibleNavHeight);

    private void Measure()
    {
        scrollY = Math.Clamp(scrollY, 0, MaxScroll);
        hitRects.Clear();
        var visibleIndices = FilteredIndices();
        for (var i = 0; i < pages.Count; i++) hitRects.Add(Rectangle.Empty);
        for (var row = 0; row < visibleIndices.Count; row++)
        {
            var index = visibleIndices[row];
            hitRects[index] = new Rectangle(12, NavTop + row * NavItemHeight - scrollY, SidebarWidth - 24, NavItemHeight - 3);
        }
    }

    private void EnsureVisible(int index)
    {
        if (index < 0 || index >= pages.Count) return;
        var row = FilteredIndices().IndexOf(index);
        if (row < 0) return;
        var top = row * NavItemHeight;
        if (top < scrollY) scrollY = top;
        else if (top + NavItemHeight > scrollY + VisibleNavHeight)
            scrollY = top + NavItemHeight - VisibleNavHeight;
        scrollY = Math.Clamp(scrollY, 0, MaxScroll);
        Measure();
    }

    private void LayoutPages()
    {
        var x = Math.Min(SidebarWidth + 1, Math.Max(0, Width - 1));
        var top = HeaderHeight;
        foreach (var (_, page) in pages)
            page.SetBounds(x, top, Math.Max(0, Width - x), Math.Max(0, Height - top));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutPages();
        Measure();
        if (selected >= 0) EnsureVisible(selected);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.X >= SidebarWidth) return;
        scrollY = Math.Clamp(scrollY - Math.Sign(e.Delta) * NavItemHeight * 3, 0, MaxScroll);
        Measure();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = e.X < SidebarWidth ? hitRects.FindIndex(r => r.Contains(e.Location)) : -1;
        if (h != hover)
        {
            hover = h;
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
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
        if (e.Button != MouseButtons.Left || e.X >= SidebarWidth) return;
        var h = hitRects.FindIndex(r => r.Contains(e.Location));
        if (h >= 0) SelectedIndex = h;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);
        Measure();

        using (var rail = new SolidBrush(Theme.Surface)) g.FillRectangle(rail, 0, 0, SidebarWidth, Height);
        using (var border = new Pen(Theme.Border)) g.DrawLine(border, SidebarWidth, 0, SidebarWidth, Height);

        TextRenderer.DrawText(g, "OPSIS", BrandFont,
            new Rectangle(18, 15, SidebarWidth - 32, 24), Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        using (var line = new Pen(Theme.Border)) g.DrawLine(line, 14, 55, SidebarWidth - 14, 55);

        var clip = g.Save();
        g.SetClip(new Rectangle(0, NavTop, SidebarWidth, Math.Max(0, Height - NavTop)));
        for (var i = 0; i < pages.Count && i < hitRects.Count; i++)
        {
            var r = hitRects[i];
            if (r.IsEmpty || r.Bottom < NavTop || r.Top > Height) continue;
            var active = i == selected;
            if (active)
            {
                using var path = FlatDraw.Rounded(r, 8);
                using var brush = new SolidBrush(FlatDraw.SurfaceHover);
                g.FillPath(brush, path);
                using var pen = new Pen(Theme.Accent, 2f);
                g.DrawLine(pen, r.Left + 1, r.Top + 7, r.Left + 1, r.Bottom - 7);
            }
            else if (i == hover)
            {
                using var path = FlatDraw.Rounded(r, 8);
                using var brush = new SolidBrush(FlatDraw.Blend(Theme.Surface, Theme.Text, Theme.IsDark ? .05 : .035));
                g.FillPath(brush, path);
            }

            var textRect = new Rectangle(r.Left + 13, r.Top, r.Width - 22, r.Height);
            TextRenderer.DrawText(g, pages[i].Title, Font, textRect, active ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        if (FilteredIndices().Count == 0)
        {
            TextRenderer.DrawText(g, Loc.T("settings.noResults"), Font,
                new Rectangle(14, NavTop + 8, SidebarWidth - 28, 42), Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
        g.Restore(clip);
        searchBox.BackColor = Theme.Surface;
        searchBox.ForeColor = Theme.Text;

        // A clear title band separates navigation from the current page and provides context after scrolling.
        using (var titleBrush = new SolidBrush(Theme.Background)) g.FillRectangle(titleBrush, SidebarWidth + 1, 0, Math.Max(0, Width - SidebarWidth - 1), HeaderHeight);
        if (selected >= 0 && selected < pages.Count)
        {
            TextRenderer.DrawText(g, pages[selected].Title, PageTitleFont,
                new Rectangle(SidebarWidth + 22, 12, Math.Max(0, Width - SidebarWidth - 40), 28), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            using var line = new Pen(Theme.Border);
            g.DrawLine(line, SidebarWidth + 20, HeaderHeight - 1, Width - 18, HeaderHeight - 1);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var (_, page) in pages) page.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Round-cornered colour square; a click opens the colour dialog.</summary>
internal sealed class FlatSwatch : Control
{
    private Color color = Color.FromArgb(0x4A, 0x90, 0xD9);

    public event EventHandler? ColorChanged;

    public FlatSwatch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(44, 32);
        Cursor = Cursors.Hand;
        Margin = new Padding(8, 3, 0, 3);
    }

    public int Rgb
    {
        get => color.ToArgb() & 0xFFFFFF;
        set
        {
            color = Color.FromArgb(255, Color.FromArgb(value));
            Invalidate();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!Enabled) return;
        using var dialog = new ColorDialog { Color = color, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        color = dialog.Color;
        Invalidate();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background); // Clear, not an antialiased fill: that leaves a faint line on the edge pixels
        using var path = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 7);
        using (var brush = new SolidBrush(Enabled ? color : Theme.Surface)) g.FillPath(brush, path);
        using var pen = new Pen(Theme.Border);
        g.DrawPath(pen, path);
    }
}

/// <summary>
/// A row of buttons with one page under it. Used twice by the settings: three big tabs on top (Settings, Sources, Information)
/// and a smaller row of sub-pages inside each of them. Replaces the old side menu.
/// </summary>
internal sealed class FlatStrip : Panel
{
    private readonly FlowLayoutPanel strip;
    private readonly Panel body = new() { Dock = DockStyle.Fill };
    private readonly List<(FlatButton Button, Control Page)> items = new();
    private readonly bool large;
    private int selected = -1;

    public event EventHandler? SelectedIndexChanged;

    public FlatStrip(bool large)
    {
        this.large = large;
        strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, Padding = large ? new Padding(10, 8, 10, 6) : new Padding(8, 6, 8, 4)
        };
        Controls.Add(body);   // Fill first, then Top (WinForms docking order)
        Controls.Add(strip);
    }

    public int Count => items.Count;

    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value < 0 || value >= items.Count || value == selected) return;
            selected = value;
            for (var i = 0; i < items.Count; i++)
            {
                items[i].Page.Visible = i == value;
                items[i].Button.SetPrimary(i == value);
            }
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The page shown now, or null when the strip is empty.</summary>
    public Control? SelectedPage => selected >= 0 && selected < items.Count ? items[selected].Page : null;

    public void AddPage(string title, Control page)
    {
        var button = new FlatButton { Text = title, IsNavigation = true };
        if (large)
        {
            button.Font = new Font("Segoe UI Semibold", 10.5f);
            button.Padding = new Padding(22, 8, 22, 8);
        }
        var index = items.Count;
        button.Click += (_, _) => SelectedIndex = index;
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        body.Controls.Add(page);
        strip.Controls.Add(button);
        items.Add((button, page));
        if (selected < 0) SelectedIndex = 0;
    }
}
