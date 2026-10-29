using System.Globalization;
using System.Text;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The market-analysis window: candlestick chart, a simple back-test, a fear &amp; greed gauge of the watched instruments and an optional
/// assistant (the user's own Anthropic key). Ideas from stonks-cli (MIT: charts, portfolio, AI chat) and greek-stocks-ai (indicators, fear &amp; greed),
/// written from scratch; every number comes from the prices this program has stored. Not investment advice.
/// </summary>
internal sealed class AnalysisForm : Form
{
    private sealed record Item(string Code, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly TickerHost host;
    private readonly FlatNav nav = new() { Dock = DockStyle.Fill };
    private List<Item> items = new();
    private bool syncing;

    // chart page
    private FlatComboBox chartInstrument = null!, chartRange = null!;
    private CandleChart chart = null!;
    private Label chartInfo = null!;
    private FlatCheckBox chartBands = null!;
    private FlatCheckBox chartMacd = null!;

    // back-test page
    private FlatComboBox btInstrument = null!, btStrategy = null!;
    private FlatNumber btFast = null!, btSlow = null!, btBuy = null!, btSell = null!;
    private Label btSummary = null!, btNote = null!;
    private LineChart btChart = null!;
    private DataGridView btTrades = null!;

    // fear & greed page
    private FearGreedGauge gauge = null!;
    private FlowLayoutPanel fgRows = null!;
    private Label fgNote = null!;

    // assistant page
    private TextBox transcript = null!;
    private FlatText question = null!, keyBox = null!;
    private FlatButton sendButton = null!, saveKey = null!, removeKey = null!;
    private FlatCheckBox shareData = null!;
    private FlatComboBox modelBox = null!;
    private Panel keyPanel = null!, askPanel = null!;
    private readonly List<AiMessage> conversation = new();
    private CancellationTokenSource? askCts;

    public AnalysisForm(TickerHost host)
    {
        this.host = host;
        Text = Loc.T("app.name") + " — " + Loc.T("analysis.title");
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(900, 560);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;

        nav.AddGroup(Loc.T("analysis.group"));
        nav.AddPage(Loc.T("analysis.tab.chart"), BuildChartPage());
        nav.AddPage(Loc.T("analysis.tab.backtest"), BuildBacktestPage());
        nav.AddPage(Loc.T("analysis.tab.fear"), BuildFearPage());
        nav.AddPage(Loc.T("analysis.tab.mood"), BuildMoodPage());
        nav.AddPage(Loc.T("analysis.tab.ai"), BuildAiPage());
        Controls.Add(nav);
        AcceptButton = sendButton;   // Enter sends the question

        LoadItems();
        nav.SelectedIndexChanged += (_, _) => Refill();
        host.QuotesUpdated += OnQuotes;
        FormClosed += (_, _) =>
        {
            host.QuotesUpdated -= OnQuotes;
            askCts?.Cancel();
        };
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape && !question.ContainsFocus) Close(); };
        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Theme.Apply(this);
        host.RefreshPortfolio();   // make sure the prices are being fetched
        host.StartHistoryBackfill(); // only does something when the user switched Yahoo history on
        Refill();
    }

    /// <summary>Test aid: the page shown first (0 chart, 1 back-test, 2 fear &amp; greed, 3 assistant).</summary>
    public void ShowPage(int index) => nav.SelectedIndex = index;

    private void OnQuotes()
    {
        if (IsDisposed) return;
        BeginInvoke(new Action(() => { LoadItems(); Refill(); }));
    }

    // ---- instruments ---------------------------------------------------------------------------------------------

    private void LoadItems()
    {
        var greek = Loc.IsGreek;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (inst, _) in host.QuoteService.LastQuotes) map[inst.StoreKey] = $"{inst.Symbol} — {(greek ? inst.NameEl : inst.NameEn)}";
        foreach (var w in host.Config.Watchlist) map[w.StoreKey] = $"{w.Symbol} — {(greek ? w.NameEl : w.NameEn)}";
        foreach (var p in host.Config.Portfolio) map[p.StoreKey] = $"{p.Symbol} — {p.DisplayName(greek)}";
        foreach (var code in host.QuoteService.Store.Codes()) map.TryAdd(code, code);
        var store = host.QuoteService.Store;
        items = map.Select(kv => new Item(kv.Key, kv.Value)).Where(i => store.Candles(i.Code).Count > 0)
            .OrderByDescending(i => store.Candles(i.Code).Count).ThenBy(i => i.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        syncing = true;
        foreach (var combo in new[] { chartInstrument, btInstrument })
        {
            var keep = combo.SelectedIndex >= 0 && combo.SelectedIndex < itemsPrevious.Count ? itemsPrevious[combo.SelectedIndex] : null;
            var index = keep is null ? 0 : Math.Max(0, items.FindIndex(i => i.Code == keep.Code));
            combo.SetItems(items.Select(i => i.Label), items.Count == 0 ? -1 : index);
        }
        itemsPrevious = items;
        syncing = false;
    }

    private List<Item> itemsPrevious = new();

    private Item? Selected(FlatComboBox combo) => combo.SelectedIndex >= 0 && combo.SelectedIndex < items.Count ? items[combo.SelectedIndex] : null;

    private IReadOnlyList<Candle> CandlesOf(Item? item) => item is null ? Array.Empty<Candle>() : host.QuoteService.Store.Candles(item.Code);

    private void Refill()
    {
        if (IsDisposed || syncing) return;
        switch (nav.SelectedIndex)
        {
            case 0: FillChart(); break;
            case 1: FillBacktest(); break;
            case 2: FillFear(); break;
            case 3: FillMood(); break;
        }
    }

    private static Panel NewPage()
    {
        var page = new Panel { AutoScroll = false, Padding = new Padding(18, 8, 18, 12) };
        // wrapped notes and summaries follow the width of the window
        page.Resize += (_, _) =>
        {
            foreach (var label in page.Controls.OfType<Label>())
                label.MaximumSize = new Size(Math.Max(200, page.ClientSize.Width - 40), 0);
        };
        return page;
    }

    private Control Bar(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private static Label Muted(string text, int width = 900) =>
        new() { Text = text, AutoSize = true, MaximumSize = new Size(width, 0), Tag = "muted", UseMnemonic = false, Margin = new Padding(0, 4, 0, 4) };

    private static Control Labelled(string text, Control control)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 14, 0) };
        flow.Controls.Add(new Label { Text = text, AutoSize = false, Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 9.5f)).Width + 12, Height = 32, TextAlign = ContentAlignment.MiddleLeft });
        flow.Controls.Add(control);
        return flow;
    }

    // ---- 1. chart ------------------------------------------------------------------------------------------------

    private Control BuildChartPage()
    {
        var page = NewPage();
        chartInstrument = new FlatComboBox { Width = 420 };
        chartRange = new FlatComboBox { Width = 170 };
        chartRange.SetItems(new[] { Loc.T("analysis.range.30"), Loc.T("analysis.range.60"), Loc.T("analysis.range.130"), Loc.T("analysis.range.all") }, 1);
        chartInfo = Muted("");
        chartBands = new FlatCheckBox { Text = Loc.T("analysis.bollinger"), Margin = new Padding(8, 6, 0, 0) };
        chartBands.CheckedChanged += (_, _) => FillChart();
        chartMacd = new FlatCheckBox { Text = Loc.T("analysis.macd"), Margin = new Padding(8, 6, 0, 0) };
        chartMacd.CheckedChanged += (_, _) => FillChart();
        chart = new CandleChart { Dock = DockStyle.Fill };
        chartInstrument.SelectedIndexChanged += (_, _) => { if (!syncing) FillChart(); };
        chartRange.SelectedIndexChanged += (_, _) => FillChart();
        var legend = Muted(Loc.T("analysis.legend"));
        legend.Dock = DockStyle.Bottom;
        page.Controls.Add(chart);
        page.Controls.Add(legend);
        page.Controls.Add(chartInfo);
        page.Controls.Add(Bar(Labelled(Loc.T("analysis.instrument"), chartInstrument), Labelled(Loc.T("analysis.range"), chartRange), chartBands, chartMacd));
        chartInfo.Dock = DockStyle.Top;
        return page;
    }

    private void FillChart()
    {
        var candles = CandlesOf(Selected(chartInstrument));
        var shown = chartRange.SelectedIndex switch { 0 => 30, 1 => 60, 2 => 130, _ => 100000 };
        chart.ShowBands = chartBands.Checked;
        chart.ShowMacd = chartMacd.Checked;
        chart.SetData(candles, shown, Loc.T(host.Config.HistoryFromYahoo ? "analysis.empty" : "analysis.emptyNoYahoo"));
        var exact = candles.Count(c => c.Exact);
        chartInfo.Text = candles.Count == 0 ? "" : Loc.T("analysis.info", candles.Count, exact);
    }

    // ---- 2. back-test --------------------------------------------------------------------------------------------

    private Control BuildBacktestPage()
    {
        var page = NewPage();
        btInstrument = new FlatComboBox { Width = 420 };
        btStrategy = new FlatComboBox { Width = 300 };
        btStrategy.SetItems(new[] { Loc.T("bt.sma"), Loc.T("bt.rsi"), Loc.T("bt.macd") }, 0);
        btFast = new FlatNumber(); btFast.Configure(2, 100, 1); btFast.SetValueSilently(20);
        btSlow = new FlatNumber(); btSlow.Configure(3, 250, 1); btSlow.SetValueSilently(50);
        btBuy = new FlatNumber(); btBuy.Configure(5, 50, 1); btBuy.SetValueSilently(30);
        btSell = new FlatNumber(); btSell.Configure(50, 95, 1); btSell.SetValueSilently(70);
        var run = new FlatButton { Text = Loc.T("bt.run") };
        run.SetPrimary(true);
        var fastLabel = Labelled(Loc.T("bt.fast"), btFast); var slowLabel = Labelled(Loc.T("bt.slow"), btSlow);
        var buyLabel = Labelled(Loc.T("bt.buyBelow"), btBuy); var sellLabel = Labelled(Loc.T("bt.sellAbove"), btSell);
        void Toggle() { var kind = btStrategy.SelectedIndex; fastLabel.Visible = slowLabel.Visible = kind == 0; buyLabel.Visible = sellLabel.Visible = kind == 1; }
        btStrategy.SelectedIndexChanged += (_, _) => { Toggle(); FillBacktest(); };
        Toggle();
        run.Click += (_, _) => FillBacktest();
        btInstrument.SelectedIndexChanged += (_, _) => { if (!syncing) FillBacktest(); };

        btSummary = new Label { UseMnemonic = false, AutoSize = true, MaximumSize = new Size(1000, 0), Font = new Font("Segoe UI Semibold", 10.5f), Margin = new Padding(0, 6, 0, 6) };
        btNote = Muted(Loc.T("bt.disclaimer"));
        btChart = new LineChart { Dock = DockStyle.Top, Height = 230, NameA = Loc.T("bt.strategy"), NameB = Loc.T("bt.buyhold") };
        btTrades = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Theme.Background, GridColor = Theme.Border, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeight = 32, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        btTrades.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Surface, ForeColor = Theme.TextMuted, SelectionBackColor = Theme.Surface, SelectionForeColor = Theme.TextMuted, Font = new Font("Segoe UI Semibold", 9f) };
        btTrades.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Text, SelectionBackColor = FlatDraw.SurfaceHover, SelectionForeColor = Theme.Text };
        foreach (var key in new[] { "bt.col.entry", "bt.col.exit", "bt.col.entryPrice", "bt.col.exitPrice", "bt.col.return" }) btTrades.Columns.Add(key, Loc.T(key));

        page.Controls.Add(btTrades);
        page.Controls.Add(btNote);
        page.Controls.Add(btChart);
        page.Controls.Add(btSummary);
        page.Controls.Add(Bar(Labelled(Loc.T("analysis.instrument"), btInstrument), Labelled(Loc.T("bt.strategy"), btStrategy), fastLabel, slowLabel, buyLabel, sellLabel, run));
        btSummary.Dock = DockStyle.Top; btNote.Dock = DockStyle.Bottom;
        return page;
    }

    private void FillBacktest()
    {
        var candles = CandlesOf(Selected(btInstrument));
        var closes = candles.Select(c => c.Close).ToList();
        btTrades.Rows.Clear();
        if (candles.Count == 0) { btSummary.Text = Loc.T("analysis.empty"); btChart.SetData(Array.Empty<double>(), Array.Empty<double>()); return; }
        var strategy = btStrategy.SelectedIndex switch { 0 => BacktestStrategy.SmaCross, 1 => BacktestStrategy.RsiReversion, _ => BacktestStrategy.MacdCross };
        var result = Backtest.Run(closes, strategy, btFast.Value, Math.Max(btSlow.Value, btFast.Value + 1), 14, btBuy.Value, btSell.Value);
        if (result.Problem is not null)
        {
            btSummary.Text = Loc.T("bt.notEnough", result.Problem, closes.Count);
            btChart.SetData(Array.Empty<double>(), Array.Empty<double>());
            return;
        }
        var bh = new List<double> { 100 };
        for (var i = 1; i < closes.Count; i++) bh.Add(closes[0] > 0 ? (double)(closes[i] / closes[0]) * 100 : 100);
        btChart.SetData(result.Equity, bh);
        btSummary.Text = Loc.T("bt.result", result.StrategyPercent.ToString("+0.0;-0.0;0.0"), result.BuyHoldPercent.ToString("+0.0;-0.0;0.0"), result.Trades.Count,
            result.Trades.Any(t => !t.Open) ? result.WinRatePercent.ToString("0") : "–", result.MaxDrawdownPercent.ToString("0.0"), result.DaysInMarket, result.Days);
        string Day(int i) => DateTime.TryParseExact(candles[i].Day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy") : candles[i].Day;
        foreach (var t in result.Trades)
            btTrades.Rows.Add(Day(t.EntryIndex), t.ExitIndex is { } x ? Day(x) : Loc.T("bt.open"), t.EntryPrice.ToString("N2"), t.ExitPrice.ToString("N2"), t.ReturnPercent.ToString("+0.0;-0.0;0.0") + " %");
    }

    // ---- 3. fear & greed -----------------------------------------------------------------------------------------

    private Control BuildFearPage()
    {
        var page = NewPage();
        gauge = new FearGreedGauge { Dock = DockStyle.Top, Height = 300 };
        fgRows = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        fgNote = Muted("");
        fgNote.Dock = DockStyle.Bottom;
        page.Controls.Add(fgRows);
        page.Controls.Add(gauge);
        page.Controls.Add(fgNote);
        return page;
    }

    private void FillFear()
    {
        var store = host.QuoteService.Store;
        var series = items.Select(i => (IReadOnlyList<decimal>)store.Candles(i.Code).Select(c => c.Close).ToList()).ToList();
        var result = FearGreed.Compute(series);
        fgRows.Controls.Clear();
        if (result.Score is { } score && result.Level is { } level)
            gauge.SetValue(score, Loc.T("fg.level." + (int)level));
        else
            gauge.SetValue(null, Loc.T("fg.notEnough", result.Instruments, result.Missing));
        foreach (var comp in result.Components)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 3, 0, 3) };
            row.Controls.Add(new Label { Text = Loc.T("fg.comp." + comp.Id), AutoSize = false, Width = 260, Height = 28, TextAlign = ContentAlignment.MiddleLeft });
            row.Controls.Add(new ScoreBar { Value = comp.Score, Width = 300, Height = 18, Margin = new Padding(0, 5, 10, 0) });
            row.Controls.Add(new Label { Text = $"{comp.Score:0}  ·  {comp.Detail}", AutoSize = false, Width = 220, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" });
            fgRows.Controls.Add(row);
        }
        fgNote.Text = Loc.T("fg.note", result.Instruments);
        Theme.Apply(fgRows);
    }

    private sealed class ScoreBar : Control
    {
        public double Value { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            using var back = FlatDraw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 6);
            using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, back);
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, back);
            var w = (int)((Width - 2) * Math.Clamp(Value, 0, 100) / 100);
            if (w < 4) return;
            var color = Value < 25 ? Color.FromArgb(214, 66, 66) : Value < 45 ? Color.FromArgb(232, 140, 60) : Value < 55 ? Color.FromArgb(220, 200, 70) : Value < 75 ? Color.FromArgb(140, 200, 80) : Color.FromArgb(46, 170, 98);
            using var fill = FlatDraw.Rounded(new Rectangle(1, 1, w, Height - 3), 5);
            using var fb = new SolidBrush(color);
            g.FillPath(fb, fill);
        }
    }

    // ---- 4. assistant --------------------------------------------------------------------------------------------

    private Control BuildAiPage()
    {
        var page = NewPage();
        var intro = Muted(Loc.T("ai.intro"));
        intro.Dock = DockStyle.Top;

        keyBox = new FlatText { Width = 520, Password = true };
        saveKey = new FlatButton { Text = Loc.T("ai.saveKey") };
        saveKey.SetPrimary(true);
        removeKey = new FlatButton { Text = Loc.T("ai.removeKey") };
        modelBox = new FlatComboBox { Width = 220 };
        modelBox.SetItems(AiAssistant.Models, Math.Max(0, Array.IndexOf(AiAssistant.Models, host.Config.AiModel)));
        shareData = new FlatCheckBox { Text = Loc.T("ai.share"), Checked = host.Config.AiShareData };
        keyPanel = new Panel { Dock = DockStyle.Top, AutoSize = true };
        keyPanel.Controls.Add(Bar(Labelled(Loc.T("ai.keyLabel"), keyBox), saveKey, removeKey, Labelled(Loc.T("ai.model"), modelBox)));
        var privacy = Muted(Loc.T("ai.privacy"));
        privacy.Dock = DockStyle.Top;

        transcript = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), Tag = "surface" };
        question = new FlatText { Width = 700 };
        sendButton = new FlatButton { Text = Loc.T("ai.send") };
        sendButton.SetPrimary(true);
        askPanel = new Panel { Dock = DockStyle.Bottom, AutoSize = true };
        askPanel.Controls.Add(Bar(question, sendButton, shareData));

        saveKey.Click += (_, _) =>
        {
            var key = keyBox.Value.Trim();
            if (key.Length < 10) return;
            host.Config.AiKeyProtected = SecretStore.Protect(key);
            host.Config.AiModel = AiAssistant.Models[Math.Max(0, modelBox.SelectedIndex)];
            host.SaveConfig();
            keyBox.Value = "";
            UpdateAiState();
        };
        removeKey.Click += (_, _) =>
        {
            host.Config.AiKeyProtected = "";
            host.SaveConfig();
            conversation.Clear();
            transcript.Clear();
            UpdateAiState();
        };
        modelBox.SelectedIndexChanged += (_, _) => { host.Config.AiModel = AiAssistant.Models[Math.Max(0, modelBox.SelectedIndex)]; host.SaveConfig(); };
        shareData.CheckedChanged += (_, _) => { host.Config.AiShareData = shareData.Checked; host.SaveConfig(); };
        sendButton.Click += async (_, _) => await SendAsync();
        page.Controls.Add(transcript);
        page.Controls.Add(askPanel);
        page.Controls.Add(privacy);
        page.Controls.Add(keyPanel);
        page.Controls.Add(intro);
        UpdateAiState();
        return page;
    }

    private void UpdateAiState()
    {
        var hasKey = SecretStore.Unprotect(host.Config.AiKeyProtected).Length > 0;
        askPanel.Visible = hasKey;
        saveKey.Visible = !hasKey;
        removeKey.Visible = hasKey;
        keyBox.Visible = !hasKey;
        if (!hasKey && transcript.TextLength == 0) transcript.Text = Loc.T("ai.noKey");
    }

    // ---- news mood -------------------------------------------------------------------------------------------

    private ScoreBar moodBar = null!;
    private Label moodSummary = null!, moodNote = null!;
    private DataGridView moodShares = null!, moodHeadlines = null!;

    private static DataGridView NewGrid(params string[] columnKeys)
    {
        var grid = new DataGridView
        {
            ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Theme.Background, GridColor = Theme.Border, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeight = 30, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Surface, ForeColor = Theme.TextMuted, SelectionBackColor = Theme.Surface, SelectionForeColor = Theme.TextMuted, Font = new Font("Segoe UI Semibold", 9f) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Text, SelectionBackColor = FlatDraw.SurfaceHover, SelectionForeColor = Theme.Text };
        foreach (var key in columnKeys) grid.Columns.Add(key, Loc.T(key));
        return grid;
    }

    private Control BuildMoodPage()
    {
        var page = NewPage();
        moodSummary = new Label { UseMnemonic = false, AutoSize = true, MaximumSize = new Size(1000, 0), Font = new Font("Segoe UI Semibold", 10.5f), Margin = new Padding(0, 6, 0, 4), Dock = DockStyle.Top };
        moodBar = new ScoreBar { Dock = DockStyle.Top, Height = 20 };
        moodNote = Muted(Loc.T("mood.note"));
        moodNote.Dock = DockStyle.Bottom;
        moodShares = NewGrid("mood.col.share", "mood.col.count", "mood.col.mood");
        moodShares.Dock = DockStyle.Left;
        moodShares.Width = 360;
        moodShares.Columns[1].FillWeight = 40; moodShares.Columns[2].FillWeight = 55;
        moodHeadlines = NewGrid("mood.col.mood", "mood.col.headline", "mood.col.source");
        moodHeadlines.Dock = DockStyle.Fill;
        moodHeadlines.Columns[0].FillWeight = 32; moodHeadlines.Columns[1].FillWeight = 100; moodHeadlines.Columns[2].FillWeight = 26;
        var spacer = new Panel { Dock = DockStyle.Left, Width = 12 };
        page.Controls.Add(moodHeadlines);
        page.Controls.Add(spacer);
        page.Controls.Add(moodShares);
        page.Controls.Add(moodNote);
        page.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        page.Controls.Add(moodBar);
        page.Controls.Add(moodSummary);
        return page;
    }

    private static string MoodText(double score) => Loc.T(NewsSentiment.Classify(score) switch { Mood.Positive => "mood.positive", Mood.Negative => "mood.negative", _ => "mood.neutral" });

    private static Color MoodColor(double score) => NewsSentiment.Classify(score) switch { Mood.Positive => ChartColors.Up, Mood.Negative => ChartColors.Down, _ => Theme.TextMuted };

    private void FillMood()
    {
        var news = host.RecentNews.Where(n => n.Kind == NewsItemKind.News).ToList();
        moodShares.Rows.Clear();
        moodHeadlines.Rows.Clear();
        if (news.Count == 0)
        {
            moodSummary.Text = Loc.T("mood.none");
            moodBar.Value = 50;
            return;
        }
        var scored = news.Select(n => (Item: n, Score: NewsSentiment.ScoreOf(n))).ToList();
        var average = scored.Average(x => x.Score);
        var good = scored.Count(x => NewsSentiment.Classify(x.Score) == Mood.Positive);
        var bad = scored.Count(x => NewsSentiment.Classify(x.Score) == Mood.Negative);
        moodSummary.Text = Loc.T("mood.overall", MoodText(average), average.ToString("+0.00;-0.00;0.00"), news.Count, good, scored.Count - good - bad, bad);
        moodBar.Value = (average + 1) * 50;
        foreach (var share in NewsSentiment.PerShare(news, host.Config.Watchlist))
        {
            var row = moodShares.Rows[moodShares.Rows.Add($"{share.Instrument.Symbol} — {share.Instrument.DisplayName(Loc.IsGreek)}", share.Headlines, $"{MoodText(share.Average)} ({share.Average:+0.00;-0.00;0.00})")];
            row.Cells[2].Style.ForeColor = MoodColor(share.Average);
        }
        foreach (var (item, score) in scored.OrderByDescending(x => Math.Abs(x.Score)).ThenByDescending(x => x.Item.PublishedAt).Take(60))
        {
            var row = moodHeadlines.Rows.Add($"{MoodText(score)} ({score:+0.00;-0.00;0.00})", item.Title, item.Source);
            moodHeadlines.Rows[row].Cells[0].Style.ForeColor = MoodColor(score);
        }
    }

    private string DataBlock()
    {
        var sb = new StringBuilder();
        var store = host.QuoteService.Store;
        var greek = Loc.IsGreek;
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["EUR"] = 1m };
        var portfolio = host.Config.Portfolio;
        if (portfolio.Count > 0)
        {
            sb.AppendLine("Portfolio (symbol, quantity, average price, currency):");
            foreach (var p in portfolio.Take(60)) sb.AppendLine($"- {p.Symbol} {p.Quantity} @ {p.AvgCost} {p.Currency}");
        }
        sb.AppendLine("Latest prices (symbol, price, change %, RSI14):");
        foreach (var (inst, q) in host.QuoteService.LastQuotes.Take(80))
        {
            var rsi = Technical.Rsi(store.History(inst.StoreKey).ToList());
            sb.AppendLine($"- {inst.Symbol} {q.Price} {(q.ChangePercent is { } c ? c.ToString("+0.00;-0.00") + "%" : "n/a")} RSI {(rsi is { } r ? r.ToString("0") : "n/a")}");
        }
        var fg = FearGreed.Compute(items.Select(i => (IReadOnlyList<decimal>)store.Candles(i.Code).Select(c => c.Close).ToList()).ToList());
        if (fg.Score is { } s) sb.AppendLine($"Fear and greed gauge of the watched instruments (0 fear - 100 greed): {s:0}");
        _ = greek; _ = rates;
        return sb.ToString();
    }

    private async Task SendAsync()
    {
        var text = question.Value.Trim();
        var key = SecretStore.Unprotect(host.Config.AiKeyProtected);
        if (text.Length == 0 || key.Length == 0 || !sendButton.Enabled) return;
        sendButton.Enabled = false;
        question.Value = "";
        Append(Loc.T("ai.you"), text);
        conversation.Add(new AiMessage("user", text));
        Append(Loc.T("ai.assistant"), Loc.T("ai.thinking"));
        askCts?.Cancel();
        askCts = new CancellationTokenSource();
        try
        {
            var system = AiAssistant.SystemPrompt(Loc.IsGreek, host.Config.AiShareData ? DataBlock() : null);
            var answer = await AiAssistant.AskAsync(key, host.Config.AiModel, system, conversation.TakeLast(20).ToList(), askCts.Token);
            conversation.Add(new AiMessage("assistant", answer));
            ReplaceLast(answer);
        }
        catch (AiException ex)
        {
            conversation.RemoveAt(conversation.Count - 1);   // the question that got no answer
            ReplaceLast(Loc.T("ai.error", ex.Message));
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!IsDisposed) sendButton.Enabled = true;
        }
    }

    private void Append(string who, string text)
    {
        if (transcript.Text == Loc.T("ai.noKey")) transcript.Clear();
        transcript.AppendText((transcript.TextLength > 0 ? "\r\n\r\n" : "") + who + ":\r\n" + text);
    }

    /// <summary>Replaces the text of the last block (the "thinking" line) with the answer or the error.</summary>
    private void ReplaceLast(string text)
    {
        var marker = Loc.T("ai.assistant") + ":\r\n";
        var at = transcript.Text.LastIndexOf(marker, StringComparison.Ordinal);
        if (at < 0) { Append(Loc.T("ai.assistant"), text); return; }
        transcript.Text = transcript.Text[..(at + marker.Length)] + text.Replace("\n", "\r\n");
        transcript.SelectionStart = transcript.TextLength;
        transcript.ScrollToCaret();
    }
}
