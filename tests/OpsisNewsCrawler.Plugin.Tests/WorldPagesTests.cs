using System.Reflection;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>The settings pages "weather by continent and country" and "news by continent and country", driven like a user would: choose, tick, add, and read the saved settings.</summary>
[Collection("plugin-feeds")]
public class WorldPagesTests
{
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static void Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(120000), "timeout");
        if (error is not null) throw new Exception("failed on the UI thread", error);
    }

    private static void Select(Control combo, int index) => combo.GetType().GetProperty("SelectedIndex")!.SetValue(combo, index);

    [Fact]
    public void ATickedWorldCityJoinsTheWeatherListAndANewsFeedJoinsTheCategory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
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

                // weather: Europe is first; pick the country that is called "France" and tick Paris
                var continent = Field<Control>(panel, "worldContinent");
                var country = Field<Control>(panel, "worldCountry");
                var list = Field<Control>(panel, "worldList");
                Select(continent, 0);
                var countries = WorldPlaces.Continents[0].Countries.OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
                Select(country, countries.FindIndex(c => c.NameEn == "France"));
                var france = countries.First(c => c.NameEn == "France");
                var paris = france.Cities.First(c => c.NameEn == "Paris");
                var raise = list.GetType().GetField("ItemChecked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(list) as Delegate;
                raise!.DynamicInvoke(france.Cities.ToList().IndexOf(paris), true);
                panel.FlushPendingChanges();
                Assert.Contains(paris.Id, host.Config.WeatherCities);
                Assert.Contains(host.Config.CustomPlaces, c => c.Id == paris.Id && c.NameEn == "Paris");
                Assert.NotNull(GreekCities.Find(paris.Id));

                // and off again: it leaves the weather list
                raise.DynamicInvoke(france.Cities.ToList().IndexOf(paris), false);
                panel.FlushPendingChanges();
                Assert.DoesNotContain(paris.Id, host.Config.WeatherCities);

                // news: the feeds of the United Kingdom are added to the selected category, a second time adds nothing
                var feedContinent = Field<Control>(panel, "worldFeedContinent");
                var feedCountry = Field<Control>(panel, "worldFeedCountry");
                Select(feedContinent, 0);
                var ukIndex = WorldFeeds.Countries("europe").ToList().FindIndex(c => c.Code == "GB");
                Assert.True(ukIndex >= 0);
                Select(feedCountry, ukIndex);
                var category = Field<System.Collections.IList>(panel, "workCategories")[0]!;
                var feedsOf = category.GetType().GetProperty("Feeds")!;
                var before = ((System.Collections.IList)feedsOf.GetValue(category)!).Count;
                var categoryList = Field<Control>(panel, "categoryList");
                Select(categoryList, 0);
                Call(panel, "AddWorldFeeds", false);
                var after = ((System.Collections.IList)feedsOf.GetValue(category)!).Count;
                Assert.Equal(before + WorldFeeds.Of("europe", "GB").Count, after);
                Call(panel, "AddWorldFeeds", false);
                Assert.Equal(after, ((System.Collections.IList)feedsOf.GetValue(category)!).Count);
                form.Close();
            });
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } GreekCities.SetCustom(null); }
    }
}
