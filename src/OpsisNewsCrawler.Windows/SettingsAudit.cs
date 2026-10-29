using System.Reflection;
using System.Text;
using System.Text.Json;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// <c>--audit-settings &lt;report.txt&gt; --config &lt;file&gt;</c>: changes every check box, drop-down, number box and slider of the settings panel one at a time,
/// presses Apply, and reports (1) controls whose change reaches no setting at all and (2) controls whose new value is not shown again after the
/// settings are read back. Meant for a throw-away config; the program's start-with-Windows entry is never touched.
/// </summary>
public static class SettingsAudit
{
    // Controls that act outside the settings file (registry, files): not changed by the audit.
    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "startWithWindows" };

    public static void Run(TickerHost host, string reportPath)
    {
        var report = new StringBuilder();
        var panel = new SettingsPanel(host, showCloseButton: false);
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 760), ShowInTaskbar = false };
        panel.Dock = DockStyle.Fill;
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();

        var apply = typeof(SettingsPanel).GetMethod("ApplySettings", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("ApplySettings not found");
        var fields = typeof(SettingsPanel).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType.Name is "FlatCheck" or "FlatCombo" or "FlatNumber" or "FlatSlider" && !Skipped.Contains(f.Name))
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        int noEffect = 0, notShown = 0, ok = 0, skipped = 0;
        foreach (var field in fields)
        {
            var control = field.GetValue(panel);
            if (control is not Control c || !c.Enabled)
            {
                skipped++;
                report.AppendLine($"SKIP      {field.Name} (not created or disabled)");
                continue;
            }
            try
            {
                panel.LoadFromConfig();
                var before = Snapshot(host.Config);
                var (describe, change, back) = Prepare(control);
                if (change is null) { skipped++; report.AppendLine($"SKIP      {field.Name} (no safe change)"); continue; }

                change();
                Application.DoEvents();
                apply.Invoke(panel, null);
                Application.DoEvents();
                var after = Snapshot(host.Config);
                var changed = Diff(before, after);

                // Read the settings back into the panel: the changed control must show the new value again.
                var shown = describe();
                panel.LoadFromConfig();
                Application.DoEvents();
                var reread = describe();

                if (changed.Count == 0)
                {
                    noEffect++;
                    report.AppendLine($"NO-EFFECT {field.Name}: changed to {shown}, Apply changed no setting");
                }
                else if (!string.Equals(shown, reread, StringComparison.Ordinal))
                {
                    notShown++;
                    report.AppendLine($"NOT-SHOWN {field.Name}: set {shown} but after reading the settings back it shows {reread} (changed: {string.Join(", ", changed)})");
                }
                else
                {
                    ok++;
                    report.AppendLine($"OK        {field.Name}: {string.Join(", ", changed)}");
                }

                back();
                apply.Invoke(panel, null);
                Application.DoEvents();
            }
            catch (Exception ex) when (ex is TargetInvocationException or InvalidOperationException or ArgumentException or NullReferenceException)
            {
                var inner = ex is TargetInvocationException { InnerException: { } i } ? i : ex;
                report.AppendLine($"ERROR     {field.Name}: {inner.GetType().Name}: {inner.Message}");
            }
        }

        report.AppendLine();
        report.AppendLine("Keyboard checks of the custom controls:");
        var kbdFailed = KeyboardChecks(report);
        report.Insert(0, $"Settings audit of {fields.Count} controls: {ok} ok, {noEffect} change nothing, {notShown} not shown again, {skipped} skipped; keyboard checks failed: {kbdFailed}{Environment.NewLine}{Environment.NewLine}");
        File.WriteAllText(reportPath, report.ToString());
        form.Close();
    }

    private static (Func<string> Describe, Action? Change, Action Back) Prepare(object control)
    {
        switch (control)
        {
            case FlatCheck check:
            {
                var original = check.Checked;
                return (() => check.Checked.ToString(), () => check.Checked = !original, () => check.Checked = original);
            }
            case FlatCombo combo:
            {
                var original = combo.SelectedIndex;
                var count = (int?)combo.GetType().GetProperty("Count")?.GetValue(combo) ?? 0;
                if (count < 2) return (() => combo.SelectedIndex.ToString(), null, () => { });
                var next = (original + 1) % count;
                return (() => combo.SelectedIndex.ToString(), () => combo.SelectedIndex = next, () => combo.SelectedIndex = original);
            }
            case FlatNumber number:
            {
                var original = number.Value;
                var next = original < Max(number) ? original + 1 : original - 1;
                return (() => number.Value.ToString(), () => number.Value = next, () => number.Value = original);
            }
            case FlatSlider slider:
            {
                var original = slider.Value;
                var next = original < Max(slider) ? original + 1 : original - 1;
                return (() => slider.Value.ToString(), () => slider.Value = next, () => slider.Value = original);
            }
        }
        return (() => "", null, () => { });
    }

    private static void Key(Control c, Keys key, bool alt = false)
    {
        var args = new KeyEventArgs(key | (alt ? Keys.Alt : Keys.None));
        c.GetType().GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, new object[] { args });
    }

    private static bool Selectable(Control c) =>
        (bool)typeof(Control).GetMethod("GetStyle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, new object[] { ControlStyles.Selectable })!;

    private static int KeyboardChecks(StringBuilder report)
    {
        var failed = 0;
        void Check(string name, bool ok)
        {
            if (!ok) failed++;
            report.AppendLine($"{(ok ? "OK  " : "FAIL")}      {name}");
        }

        using var combo = new FlatCombo();
        combo.SetItems(new[] { "a", "b", "c" }, 0);
        Key(combo, Keys.Down);
        Check("combo: Down selects the next item", combo.SelectedIndex == 1);
        Key(combo, Keys.End);
        Check("combo: End selects the last item", combo.SelectedIndex == 2);
        Key(combo, Keys.Up);
        Key(combo, Keys.Up);
        Key(combo, Keys.Up);
        Check("combo: Up stops at the first item", combo.SelectedIndex == 0);
        Check("combo: reachable with Tab", Selectable(combo));

        using var slider = new FlatSlider();
        slider.SetRange(0, 100);
        slider.Value = 50;
        Key(slider, Keys.Right);
        Check("slider: Right adds 1", slider.Value == 51);
        Key(slider, Keys.PageUp);
        Check("slider: PageUp adds 10 %", slider.Value == 61);
        Key(slider, Keys.Home);
        Check("slider: Home goes to the minimum", slider.Value == 0);
        Key(slider, Keys.Left);
        Check("slider: Left stops at the minimum", slider.Value == 0);
        Check("slider: reachable with Tab", Selectable(slider));

        using var button = new FlatButton();
        Check("button: reachable with Tab", Selectable(button) && button.TabStop);
        using var check = new FlatCheck();
        Check("check box: reachable with Tab", Selectable(check) && check.TabStop);
        return failed;
    }
    private static int Max(object control) =>
        control.GetType().GetField("maximum", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(control) is int m ? m : int.MaxValue;

    private static Dictionary<string, string> Snapshot(AppConfig config)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(config));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
    }

    private static List<string> Diff(Dictionary<string, string> a, Dictionary<string, string> b) =>
        b.Where(kv => !a.TryGetValue(kv.Key, out var old) || old != kv.Value).Select(kv => kv.Key).ToList();
}

/// <summary><c>--snapshot-geo &lt;png&gt; &lt;query&gt; --config &lt;file&gt;</c>: types the query into the place search of the weather page, searches (real network), ticks the first two places, adds them and saves a picture.</summary>
public static class GeoSnapshot
{
    public static void Run(TickerHost host, string png, string query)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 1500), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        panel.SelectTab(6);
        Application.DoEvents();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var t = typeof(SettingsPanel);
        var q = (FlatText)t.GetField("geoQuery", flags)!.GetValue(panel)!;
        q.Value = query;
        Wait((Task)t.GetMethod("RunGeoSearchAsync", flags)!.Invoke(panel, null)!);
        var list = (FlatList)t.GetField("geoList", flags)!.GetValue(panel)!;
        list.SetChecked(0, true);
        if (((System.Collections.IList)t.GetField("geoHits", flags)!.GetValue(panel)!).Count > 1) list.SetChecked(1, true);
        Wait((Task)t.GetMethod("AddGeoPlacesAsync", flags)!.Invoke(panel, null)!);
        for (var i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(40); }
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        bmp.Save(png);
        form.Close();
    }

    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(25);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(30); }
    }
}

/// <summary><c>--snapshot-tall &lt;png&gt; &lt;tab&gt; --config &lt;file&gt;</c>: one settings page on a tall window, so that cards far down the page are in the picture.</summary>
public static class TallSnapshot
{
    public static void Run(TickerHost host, string png, int tab, int height)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, height), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        panel.SelectTab(tab);
        for (var i = 0; i < 12; i++) { Application.DoEvents(); Thread.Sleep(40); }
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        bmp.Save(png);
        form.Close();
    }
}

/// <summary><c>--audit-layout &lt;report&gt; --config &lt;file&gt;</c>: for every page of the settings, reports the cards wider than the common card width and controls that stick out of their card.</summary>
public static class LayoutAudit
{
    public static void Run(TickerHost host, string report)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 2600), ShowInTaskbar = false };
        var panel = new SettingsPanel(host, showCloseButton: false) { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.LoadFromConfig();
        var sb = new System.Text.StringBuilder();
        for (var tab = 0; tab < panel.TabCount; tab++)
        {
            panel.SelectTab(tab);
            for (var i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(30); }
            foreach (var card in All(panel).OfType<FlatCard>().Where(c => ShownNow(c)))
            {
                // The normal width is 785 (the 780 minimum plus its border); anything wider has content that does not fit.
                if (card.Width > FlatCard.CardWidth + 10)
                {
                    var wide = All(card).Where(c => c.Right > 0).OrderByDescending(c => c.PointToScreen(new Point(c.Width, 0)).X).FirstOrDefault();
                    var heading = All(card.Parent!).OfType<Label>().FirstOrDefault(l => l.Parent == card)?.Text ?? "";
                    var rows = All(card).OfType<FlowLayoutPanel>().Where(f => f.Width > FlatCard.CardWidth - 30).Select(f => f.Controls.OfType<Control>().Select(c => c is Label lb ? lb.Text : c.GetType().Name).FirstOrDefault()).Where(s => s != null).Take(3);
                    sb.AppendLine($"tab {tab}: card '{heading}' is {card.Width}px wide (normal {FlatCard.CardWidth + 5}); wide rows: {string.Join(" | ", rows)}");
                }
                foreach (Control inner in Descendants(card))
                {
                    var right = inner.PointToScreen(new Point(inner.Width, 0)).X - card.PointToScreen(Point.Empty).X;
                    if (right > card.Width + 1 && inner.Visible) sb.AppendLine($"tab {tab}: {inner.GetType().Name} '{(inner is Label l ? l.Text : inner.Name)}' ends {right - card.Width}px outside its card");
                }
            }
        }
        File.WriteAllText(report, sb.Length == 0 ? "no card is wider than the common width; nothing sticks out\n" : sb.ToString());
        form.Close();
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in All(c)) yield return d;
        }
    }

    private static IEnumerable<Control> Descendants(Control root) => All(root);

    private static bool ShownNow(Control c)
    {
        for (Control? x = c; x is not null; x = x.Parent) if (!x.Visible) return false;
        return true;
    }
}
