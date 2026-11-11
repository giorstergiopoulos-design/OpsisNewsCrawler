using System.Reflection;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>The "History flow" page of the settings with a feed that a plugin registered: it lists the feed, previews its lines, switches it off and puts it in a bar.</summary>
[Collection("plugin-feeds")]
public class HistoryPageTests
{
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(60000), "timeout");
        if (error is not null) throw new Exception("failed on the UI thread", error);
    }

    [Fact]
    public void ARegisteredFeedIsListedPreviewedSwitchedOffAndPutInTheMainBar()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var items = new List<PluginFeedItem>();
        PluginFeeds.Register(new PluginFeed("san-simera-test", "Σαν σήμερα (τεστ)", "On this day (test)", () => items.ToList()));
        try
        {
            OnSta(() =>
            {
                Application.EnableVisualStyles();
                using var host = new TickerHost(Path.Combine(dir, "config.json"));
                using var panel = new SettingsPanel(host, showCloseButton: false);
                using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(1180, 760), ShowInTaskbar = false };
                panel.Dock = DockStyle.Fill;
                form.Controls.Add(panel);
                form.Show();
                Application.DoEvents();
                panel.LoadFromConfig();
                Application.DoEvents();

                var list = Field<Control>(panel, "historyList");
                var preview = Field<Control>(panel, "historyPreview");
                var status = Field<Label>(panel, "historyStatus");
                var used = Field<Label>(panel, "historyUsed");
                Assert.Equal(1, (int)list.GetType().GetProperty("Items")!.GetValue(list)!.GetType().GetProperty("Count")!.GetValue(list.GetType().GetProperty("Items")!.GetValue(list))!);
                // the plugin has not filled its feed yet: the page says so instead of showing an empty list
                Assert.Equal(Loc.T("history.loading"), status.Text);

                // the feed is filled (in the background, a few seconds after the plugin started) and announces it
                items.Add(new PluginFeedItem("1969: first line"));
                items.Add(new PluginFeedItem("1821: second line", "https://example.com"));
                typeof(PluginFeeds).GetMethod("Register")!.Invoke(null, new object[] { PluginFeeds.Find("san-simera-test")! });
                for (var i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(20); }
                Assert.Equal(2, ((System.Collections.ICollection)preview.GetType().GetProperty("Items")!.GetValue(preview)!).Count);
                Assert.Contains("2", status.Text);
                Assert.Equal(Loc.T("history.notUsed"), used.Text);

                // switch it off: the setting is saved
                var checkedField = list.GetType().GetMethod("SetChecked")!;
                checkedField.Invoke(list, new object[] { 0, false });
                var raise = list.GetType().GetField("ItemChecked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(list) as Delegate;
                raise!.DynamicInvoke(0, false);
                panel.FlushPendingChanges();
                Assert.Contains("san-simera-test", host.Config.DisabledPluginFeeds);

                // and on again, and into the right half of the main bar
                raise.DynamicInvoke(0, true);
                typeof(SettingsPanel).GetMethod("AddSelectedFeedToMainBar", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, null);
                panel.FlushPendingChanges();
                Assert.DoesNotContain("san-simera-test", host.Config.DisabledPluginFeeds);
                Assert.Equal("feed:san-simera-test", host.Config.MainSlotB);
                Assert.Contains(Loc.T("card.mainBar"), used.Text);
                form.Close();
            });
        }
        finally
        {
            PluginFeeds.Unregister("san-simera-test");
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
