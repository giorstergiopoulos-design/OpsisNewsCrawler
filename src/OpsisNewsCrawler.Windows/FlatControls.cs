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

    public FlatCard(string title)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18, 34, 18, 14);
        Margin = new Padding(0, 8, 0, 8);
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
/// The tab buttons in ONE row. When they do not all fit they scroll sideways: with the mouse wheel, with the arrows at both
/// ends, or by choosing a tab (it is brought into view).
/// </summary>
internal sealed class FlatTabs : Control
{
    private const int HeaderHeight = 44;
    private const int ArrowWidth = 28;
    private readonly List<(string Title, Control Page)> pages = new();
    private readonly List<Rectangle> hitRects = new();
    private int selected = -1;
    private int hover = -1;
    private int hoverArrow; // -1 left, 1 right
    private int scrollX;
    private int stripWidth;

    public FlatTabs()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9.5f);
    }

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

    private bool Overflows => stripWidth > Width;
    private int ViewLeft => Overflows ? ArrowWidth : 0;
    private int ViewWidth => Overflows ? Math.Max(1, Width - 2 * ArrowWidth) : Width;
    private int MaxScroll => Math.Max(0, stripWidth - ViewWidth);

    /// <summary>Measures every tab button and puts its rectangle where it is drawn now (scrolled).</summary>
    private void Measure()
    {
        hitRects.Clear();
        var x = 0;
        var widths = new List<int>();
        foreach (var (title, _) in pages)
        {
            var w = TextRenderer.MeasureText(title, Font).Width + 30;
            widths.Add(w);
            x += w + 6;
        }
        stripWidth = Math.Max(0, x - 6);
        scrollX = Math.Clamp(scrollX, 0, MaxScroll);
        x = ViewLeft - scrollX;
        foreach (var w in widths)
        {
            hitRects.Add(new Rectangle(x, 4, w, 34));
            x += w + 6;
        }
    }

    private void EnsureVisible(int index)
    {
        Measure();
        if (!Overflows || index < 0 || index >= hitRects.Count) return;
        var r = hitRects[index];
        if (r.Left < ViewLeft) scrollX -= ViewLeft - r.Left + 8;
        else if (r.Right > ViewLeft + ViewWidth) scrollX += r.Right - (ViewLeft + ViewWidth) + 8;
        scrollX = Math.Clamp(scrollX, 0, MaxScroll);
    }

    private void LayoutPages()
    {
        Measure();
        var top = HeaderHeight + 8;
        foreach (var (_, page) in pages) page.SetBounds(0, top, Width, Math.Max(0, Height - top));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutPages();
        if (selected >= 0) EnsureVisible(selected);
    }

    private Rectangle LeftArrow => new(0, 4, ArrowWidth, 34);
    private Rectangle RightArrow => new(Width - ArrowWidth, 4, ArrowWidth, 34);

    private void ScrollBy(int delta)
    {
        var before = scrollX;
        scrollX = Math.Clamp(scrollX + delta, 0, MaxScroll);
        if (scrollX != before) Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Overflows || e.Y > HeaderHeight) return;
        ScrollBy(-Math.Sign(e.Delta) * 140);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var arrow = Overflows ? (LeftArrow.Contains(e.Location) ? -1 : RightArrow.Contains(e.Location) ? 1 : 0) : 0;
        var inView = e.X >= ViewLeft && e.X < ViewLeft + ViewWidth;
        var h = inView ? hitRects.FindIndex(r => r.Contains(e.Location)) : -1;
        if (h != hover || arrow != hoverArrow)
        {
            hover = h;
            hoverArrow = arrow;
            Cursor = h >= 0 || arrow != 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = -1;
        hoverArrow = 0;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (Overflows && LeftArrow.Contains(e.Location)) { ScrollBy(-220); return; }
        if (Overflows && RightArrow.Contains(e.Location)) { ScrollBy(220); return; }
        if (e.X < ViewLeft || e.X >= ViewLeft + ViewWidth) return;
        var h = hitRects.FindIndex(r => r.Contains(e.Location));
        if (h >= 0) SelectedIndex = h;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);
        Measure();

        var state = g.Save();
        g.SetClip(new Rectangle(ViewLeft, 0, ViewWidth, HeaderHeight));
        for (var i = 0; i < pages.Count && i < hitRects.Count; i++)
        {
            var r = hitRects[i];
            if (r.Right < ViewLeft || r.Left > ViewLeft + ViewWidth) continue;
            var active = i == selected;
            using var path = FlatDraw.Rounded(r, 8);
            using (var brush = new SolidBrush(active ? FlatDraw.SurfaceHover : i == hover ? Theme.Surface : Theme.Background)) g.FillPath(brush, path);
            using (var pen = new Pen(active ? Theme.Accent : Theme.Border, active ? 1.6f : 1f)) g.DrawPath(pen, path);
            TextRenderer.DrawText(g, pages[i].Title, Font, r, active ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        g.Restore(state);

        if (Overflows)
        {
            DrawArrow(g, LeftArrow, -1, scrollX > 0, hoverArrow == -1);
            DrawArrow(g, RightArrow, 1, scrollX < MaxScroll, hoverArrow == 1);
        }
    }

    private static void DrawArrow(Graphics g, Rectangle r, int direction, bool enabled, bool hot)
    {
        using var path = FlatDraw.Rounded(r, 8);
        using (var brush = new SolidBrush(hot && enabled ? Theme.Surface : Theme.Background)) g.FillPath(brush, path);
        using (var border = new Pen(Theme.Border)) g.DrawPath(border, path);
        var cx = r.Left + r.Width / 2f;
        var cy = r.Top + r.Height / 2f;
        using var pen = new Pen(enabled ? Theme.Text : Theme.Border, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[] { new PointF(cx - direction * 3, cy - 6), new PointF(cx + direction * 3, cy), new PointF(cx - direction * 3, cy + 6) });
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
