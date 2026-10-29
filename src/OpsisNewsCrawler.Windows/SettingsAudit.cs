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
