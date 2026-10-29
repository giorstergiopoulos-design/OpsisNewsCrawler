using OpsisNewsCrawler.Core;
using System.Drawing.Drawing2D;
using Opsis.UI;

namespace OpsisNewsCrawler.Windows;

// Controls drawn like the Opsis - Desktop Customization UI: rounded dark surfaces, a thin border, accent colour for the
// active state, no classic Windows 3D look. Ported from Opsis' FlatCheckBox / FlatComboBox / FlatSlider / HoverButton /
// NavButton (same MIT project) and wired to <see cref="Theme"/>, so they follow the host's colours.

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

    /// <summary>Hides the typed characters (for a key).</summary>
    public bool Password { get => box.UseSystemPasswordChar; set => box.UseSystemPasswordChar = value; }

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

/// <summary>A thin rounded progress bar (value 0..1) in the colours of the theme.</summary>
internal sealed class FlatProgress : Control
{
    private double value;

    public FlatProgress()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 8;
        Width = 360;
        Margin = new Padding(0, 6, 0, 4);
    }

    public double Value
    {
        get => value;
        set { this.value = Math.Clamp(value, 0, 1); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        using var track = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
        using (var brush = new SolidBrush(Theme.Border)) g.FillPath(brush, track);
        var w = (int)((Width - 1) * value);
        if (w < Height) return;
        using var fill = FlatDraw.Rounded(new Rectangle(0, 0, w, Height - 1), Height / 2);
        using var accent = new SolidBrush(Theme.Accent);
        g.FillPath(accent, fill);
    }
}
