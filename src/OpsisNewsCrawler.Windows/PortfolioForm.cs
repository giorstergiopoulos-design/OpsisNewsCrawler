using System.Globalization;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The portfolio window: what the user owns (quantity, average price), with live prices, value and profit/loss in euro, the change today
/// and two simple indicators (RSI, SMA crossing) computed from the closing prices the program has stored. The idea comes from the terminal
/// dashboard of stonks-cli (MIT), rebuilt here as a window. Not investment advice.
/// </summary>
internal sealed class PortfolioForm : Form
{
    private readonly TickerHost host;
    private readonly DataGridView grid = new();
    private readonly Label summary = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label hint = new() { AutoSize = true, Tag = "muted", MaximumSize = new Size(960, 0), Padding = new Padding(2, 4, 0, 4) };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 20000 };
    private bool refreshing;

    public PortfolioForm(TickerHost host)
    {
        this.host = host;
        Text = Loc.T("app.name") + " — " + Loc.T("portfolio.title");
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 560);
        MinimumSize = new Size(820, 380);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;

        var add = new FlatButton { Text = Loc.T("btn.addPosition") };
        var edit = new FlatButton { Text = Loc.T("btn.editPosition") };
        var remove = new FlatButton { Text = Loc.T("btn.removePosition") };
        var refresh = new FlatButton { Text = Loc.T("tray.refresh") };
        add.SetPrimary(true);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 10, 10, 4), WrapContents = false };
        bar.Controls.AddRange(new Control[] { add, edit, remove, refresh });

        var summaryHost = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(14, 0, 14, 0) };
        summary.Font = new Font("Segoe UI Semibold", 11f);
        summaryHost.Controls.Add(summary);

        ConfigureGrid();
        hint.Text = Loc.T("portfolio.empty");
        var top = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 0, 10, 0) };
        top.Controls.Add(hint);

        Controls.Add(grid);
        Controls.Add(summaryHost);
        Controls.Add(top);
        Controls.Add(bar);
        grid.BringToFront();

        add.Click += (_, _) => EditPosition(null);
        edit.Click += (_, _) => EditSelected();
        remove.Click += (_, _) => RemoveSelected();
        refresh.Click += (_, _) => host.RefreshPortfolio();
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        host.QuotesUpdated += OnQuotes;
        timer.Tick += (_, _) => host.RefreshPortfolio();
        timer.Start();
        FormClosed += (_, _) =>
        {
            timer.Stop();
            host.QuotesUpdated -= OnQuotes;
            host.SetPortfolioOpen(false);
        };
        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Theme.Apply(this);
        host.SetPortfolioOpen(true);
        _ = ShowAsync();
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ScrollBars = ScrollBars.Vertical;
        grid.BackgroundColor = Theme.Background;
        grid.GridColor = Theme.Border;
        grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 30;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface, ForeColor = Theme.TextMuted, SelectionBackColor = Theme.Surface, SelectionForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI Semibold", 9f), Alignment = DataGridViewContentAlignment.MiddleRight, Padding = new Padding(6, 0, 6, 0)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background, ForeColor = Theme.Text, SelectionBackColor = FlatDraw.SurfaceHover, SelectionForeColor = Theme.Text,
            Alignment = DataGridViewContentAlignment.MiddleRight, Padding = new Padding(6, 0, 6, 0)
        };
        void Col(string name, string header, int width, bool left = false)
        {
            var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (left) { c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft; c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft; }
            grid.Columns.Add(c);
        }
        Col("symbol", Loc.T("col.symbol"), 80, left: true);
        Col("name", Loc.T("col.name"), 150, left: true);
        grid.Columns["name"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        Col("qty", Loc.T("col.qty"), 95);
        Col("avg", Loc.T("col.avg"), 135);
        Col("price", Loc.T("col.price"), 90);
        Col("day", Loc.T("col.day"), 85);
        Col("value", Loc.T("col.value"), 110);
        Col("pnl", Loc.T("col.pnl"), 150);
        Col("pnlpct", Loc.T("col.pnlpct"), 85);
        Col("rsi", Loc.T("col.rsi"), 55);
        Col("signal", Loc.T("col.signal"), 100);
    }

    private void OnQuotes()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(() => _ = ShowAsync()));
        else _ = ShowAsync();
    }

    private static readonly CultureInfo Gr = CultureInfo.GetCultureInfo("el-GR");
    private static string Money(decimal v) => v.ToString("#,##0.00", Gr);
    private static string Signed(decimal v) => (v > 0 ? "+" : "") + v.ToString("#,##0.00", Gr);

    private async Task ShowAsync()
    {
        if (refreshing || IsDisposed) return;
        refreshing = true;
        try
        {
            var rates = await host.Ecb.GetAsync(CancellationToken.None);
            if (IsDisposed) return;
            var positions = host.Config.Portfolio;
            var result = PortfolioCalc.Compute(positions, host.QuoteService.LatestByKey, rates, p => host.QuoteService.Store.History(p.StoreKey));
            var greek = Loc.IsGreek;
            var selected = grid.CurrentRow?.Index ?? -1;
            grid.Rows.Clear();
            foreach (var row in result.Rows)
            {
                var p = row.Position;
                var i = grid.Rows.Add(p.Symbol, p.DisplayName(greek), p.Quantity.ToString("0.####", Gr), Money(p.AvgCost) + " " + p.Currency,
                    row.Price is { } price ? Money(price) : "…",
                    row.ChangePercent is { } c ? (c > 0 ? "+" : "") + c.ToString("0.00", Gr) + " %" : "",
                    row.Value is { } v ? Money(v) : "…",
                    row.Pnl is { } pnl ? Signed(pnl) : "",
                    row.PnlPercent is { } pp ? (pp > 0 ? "+" : "") + pp.ToString("0.00", Gr) + " %" : "",
                    row.Rsi is { } rsi ? rsi.ToString("0", Gr) : "",
                    row.Signal);
                var up = Color.FromArgb(46, 190, 100);
                var down = Color.FromArgb(235, 80, 80);
                foreach (var name in new[] { "day", "pnl", "pnlpct" })
                {
                    var positive = name == "day" ? row.ChangePercent : row.Pnl;
                    if (positive is { } x && x != 0) grid.Rows[i].Cells[name].Style.ForeColor = x > 0 ? up : down;
                }
                if (row.Signal.StartsWith("▲")) grid.Rows[i].Cells["signal"].Style.ForeColor = up;
                else if (row.Signal.StartsWith("▼")) grid.Rows[i].Cells["signal"].Style.ForeColor = down;
            }
            if (selected >= 0 && selected < grid.Rows.Count) grid.CurrentCell = grid.Rows[selected].Cells[0];
            hint.Visible = positions.Count == 0 || !result.Complete;
            hint.Text = positions.Count == 0 ? Loc.T("portfolio.empty") : Loc.T("portfolio.incomplete");
            summary.Text = positions.Count == 0 ? "" : Loc.T("portfolio.summary", Money(result.Value) + " €", Signed(result.Pnl) + " €", result.PnlPercent.ToString("0.00", Gr), Signed(result.DayChange) + " €");
            summary.ForeColor = result.Pnl > 0 ? Color.FromArgb(46, 190, 100) : result.Pnl < 0 ? Color.FromArgb(235, 80, 80) : Theme.Text;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void EditSelected()
    {
        if (grid.CurrentRow is { } row && row.Index >= 0 && row.Index < host.Config.Portfolio.Count) EditPosition(host.Config.Portfolio[row.Index]);
    }

    private void RemoveSelected()
    {
        if (grid.CurrentRow is not { } row || row.Index < 0 || row.Index >= host.Config.Portfolio.Count) return;
        host.Config.Portfolio.RemoveAt(row.Index);
        host.SaveConfig();
        _ = ShowAsync();
    }

    private void EditPosition(PortfolioPosition? existing)
    {
        using var dialog = new PositionDialog(host, existing);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } result) return;
        var list = host.Config.Portfolio;
        var at = existing is null ? -1 : list.IndexOf(existing);
        if (at >= 0) list[at] = result;
        else list.Add(result);
        host.SaveConfig();
        host.RefreshPortfolio();
        _ = ShowAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Add or change one position: choose the instrument (search), the quantity, the average price paid and its currency.</summary>
internal sealed class PositionDialog : Form
{
    private readonly FlatText search = new();
    private readonly FlatList list = new();
    private readonly FlatText quantity = new();
    private readonly FlatText avgCost = new();
    private readonly FlatComboBox currency = new();
    private readonly List<QuoteInstrument> all;
    private List<QuoteInstrument> shown = new();

    public PortfolioPosition? Result { get; private set; }

    public PositionDialog(TickerHost host, PortfolioPosition? existing)
    {
        Text = Loc.T("dlg.position");
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 520);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;

        // Every instrument the program knows: the watch list plus the downloaded lists of the Euronext markets.
        all = host.Config.Watchlist.Concat(EuronextCatalog.Markets.SelectMany(m => EuronextCatalog.LoadCached(m.Mic)))
            .Where(i => i.Source != QuoteSource.Currency)
            .GroupBy(i => $"{i.Source}|{i.Market}|{i.Code}").Select(g => g.First())
            .OrderBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase).ToList();

        var y = 14;
        Label Caption(string text, int top) => new() { Text = text, Left = 16, Top = top, AutoSize = true, Tag = "muted" };
        Controls.Add(Caption(Loc.T("lbl.instrument"), y));
        search.SetBounds(16, y + 22, 528, 32);
        list.SetBounds(16, y + 62, 528, 250);
        list.SetShowChecks(false);
        Controls.Add(search);
        Controls.Add(list);
        y += 326;
        Controls.Add(Caption(Loc.T("lbl.quantity"), y));
        Controls.Add(Caption(Loc.T("lbl.avgCost"), y));
        Controls[^1].Left = 200;
        Controls.Add(Caption(Loc.T("lbl.currency"), y));
        Controls[^1].Left = 400;
        quantity.SetBounds(16, y + 22, 170, 32);
        avgCost.SetBounds(200, y + 22, 186, 32);
        currency.SetBounds(400, y + 22, 144, 32);
        currency.SetItems(new[] { "EUR", "USD", "GBP", "CHF", "JPY", "NOK" }, 0);
        Controls.Add(quantity);
        Controls.Add(avgCost);
        Controls.Add(currency);
        var ok = new FlatButton { Text = Loc.T("btn.ok"), Left = 16, Top = 470 };
        ok.SetPrimary(true);
        var cancel = new FlatButton { Text = Loc.T("btn.cancel"), Left = 110, Top = 470, DialogResult = DialogResult.Cancel };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        search.TextValueChanged += (_, _) => Fill();
        Fill();
        if (existing is not null)
        {
            search.Value = existing.Symbol;
            Fill();
            if (shown.Count > 0) list.SetChecked(0, true);
            quantity.Value = existing.Quantity.ToString("0.####", CultureInfo.CurrentCulture);
            avgCost.Value = existing.AvgCost.ToString("0.####", CultureInfo.CurrentCulture);
            var idx = Array.IndexOf(new[] { "EUR", "USD", "GBP", "CHF", "JPY", "NOK" }, existing.Currency);
            currency.SelectSilently(Math.Max(0, idx));
        }
        list.ItemChecked += (index, isChecked) =>
        {
            if (!isChecked) return;
            for (var i = 0; i < shown.Count; i++) if (i != index) list.SetChecked(i, false); // one choice only
            if (index >= 0 && index < shown.Count) currency.SelectSilently(CurrencyIndexFor(shown[index]));
        };
        ok.Click += (_, _) => Accept(existing);
        Theme.Apply(this);
        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
    }

    private static int CurrencyIndexFor(QuoteInstrument i) => i.Unit.StartsWith("$") ? 1 : 0;

    private void Fill()
    {
        var term = search.Value.Trim();
        shown = all.Where(i => term.Length == 0 || i.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               i.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) || i.NameEl.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(200).ToList();
        list.ClearRows();
        foreach (var i in shown) list.AddRow($"{i.Symbol}  —  {i.DisplayName(Loc.IsGreek)}", false);
    }

    private static bool TryNumber(string text, out decimal value) =>
        decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0;

    private void Accept(PortfolioPosition? existing)
    {
        var chosen = Enumerable.Range(0, shown.Count).Where(i => list.GetChecked(i)).Select(i => shown[i]).FirstOrDefault();
        var instrument = chosen ?? (existing is not null ? existing.ToInstrument() : null);
        if (instrument is null || !TryNumber(quantity.Value, out var q) || !TryNumber(avgCost.Value, out var cost))
        {
            MessageBox.Show(this, Loc.T("msg.badNumber"), Loc.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var code = new[] { "EUR", "USD", "GBP", "CHF", "JPY", "NOK" }[Math.Clamp(currency.SelectedIndex, 0, 5)];
        Result = chosen is null && existing is not null
            ? new PortfolioPosition { Source = existing.Source, Market = existing.Market, Code = existing.Code, Symbol = existing.Symbol, NameEl = existing.NameEl, NameEn = existing.NameEn, Quantity = q, AvgCost = cost, Currency = code }
            : PortfolioPosition.From(instrument, q, cost, code);
        DialogResult = DialogResult.OK;
    }
}
