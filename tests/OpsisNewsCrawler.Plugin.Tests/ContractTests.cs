using System.Reflection;
using System.Runtime.Loader;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>
/// The plugin contract is shared with plugins built elsewhere (the "San simera" plugin ships its own copy of this DLL). These tests load that reference copy in its own
/// load context, like the host does for a plugin, and compare the public surface with the contract of this repository.
/// </summary>
public class ContractTests
{
    private static string ReferencePath => Path.Combine(AppContext.BaseDirectory, "reference", "OpsisNewsCrawler.Plugin.Abstractions.san-shmera.dll");

    private static string Describe(MemberInfo m) => m switch
    {
        MethodBase mb => $"{m.MemberType} {m.Name}({string.Join(",", mb.GetParameters().Select(p => p.ParameterType.FullName + (p.HasDefaultValue ? "=" : "")))})" + (m is MethodInfo mi ? ":" + mi.ReturnType.FullName : ""),
        PropertyInfo p => $"Property {p.Name}:{p.PropertyType.FullName}",
        EventInfo e => $"Event {e.Name}:{e.EventHandlerType?.FullName}",
        FieldInfo f => $"Field {f.Name}:{f.FieldType.FullName}",
        _ => m.MemberType + " " + m.Name
    };

    private static SortedDictionary<string, SortedSet<string>> Surface(Assembly a) =>
        new(a.GetExportedTypes().ToDictionary(t => t.FullName!, t => new SortedSet<string>(
            t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(Describe))));

    [Fact]
    public void TheReferenceContractHasTheFrozenAssemblyVersion()
    {
        var ours = typeof(PluginFeeds).Assembly.GetName();
        Assert.Equal(new Version(0, 8, 0, 0), ours.Version);
        var ctx = new AssemblyLoadContext("reference", isCollectible: true);
        try
        {
            var reference = ctx.LoadFromAssemblyPath(ReferencePath);
            Assert.Equal(ours.Version, reference.GetName().Version);
            Assert.Equal(ours.Name, reference.GetName().Name);
        }
        finally { ctx.Unload(); }
    }

    [Fact]
    public void EveryPublicTypeOfThePluginsContractIsInOurContractWithTheSameSignatures()
    {
        var ctx = new AssemblyLoadContext("reference2", isCollectible: true);
        try
        {
            var theirs = Surface(ctx.LoadFromAssemblyPath(ReferencePath));
            var ours = Surface(typeof(PluginFeeds).Assembly);
            foreach (var (type, members) in theirs)
            {
                Assert.True(ours.ContainsKey(type), "missing type " + type);
                foreach (var m in members)
                    Assert.True(ours[type].Contains(m), $"{type}: signature {m} is not in our contract");
            }
        }
        finally { ctx.Unload(); }
    }

    [Fact]
    public void ThePublicSurfaceHasTheTypesThePluginsNeed()
    {
        var surface = Surface(typeof(PluginFeeds).Assembly);
        foreach (var t in new[] { "PluginWidget", "IPluginWidgetProvider", "PluginFeedItem", "PluginFeed", "PluginFeeds", "PluginNavigationItem", "IOpsisNewsCrawlerPlugin", "IPluginProfileAware" })
            Assert.Contains("OpsisNewsCrawler.Plugin.Abstractions." + t, surface.Keys);
    }

    [Fact]
    public void TheContractDllContainsTheFeedRegistryText()
    {
        // the check of the host: an old contract DLL (15 KB) lacks these names and the plugin fails to load
        var bytes = File.ReadAllBytes(typeof(PluginFeeds).Assembly.Location);
        Assert.Contains("PluginFeeds", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void FeedsRegisterReplaceUnregisterAndRaiseChanged()
    {
        var changes = 0;
        void On() => Interlocked.Increment(ref changes);
        PluginFeeds.Changed += On;
        try
        {
            PluginFeeds.Register(new PluginFeed("t-feed", "Test el", "Test", () => new[] { new PluginFeedItem("a", "https://example.com") }));
            PluginFeeds.Register(new PluginFeed("T-FEED", "Test el 2", "Test 2", () => Array.Empty<PluginFeedItem>()));   // same key, any case: replaced
            Assert.Equal(1, PluginFeeds.All().Count(f => f.Key.Equals("t-feed", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("Test 2", PluginFeeds.Find("t-feed")!.TitleEn);
            Assert.Null(PluginFeeds.Find(" "));
            PluginFeeds.Unregister("nope");
            PluginFeeds.Unregister("t-feed");
            Assert.Null(PluginFeeds.Find("t-feed"));
            Assert.Equal(3, changes);   // two registers and one real unregister; an unknown key raises nothing
        }
        finally { PluginFeeds.Changed -= On; }
    }

    private sealed class StubPlugin : IPluginWidgetProvider
    {
        public IReadOnlyList<PluginWidget> GetWidgets() => new[] { new PluginWidget("w", "Imerologio", "Calendar", () => new Control(), "calendar") };
    }

    [Fact]
    public void AWidgetProviderCanReplaceABuiltInWidget()
    {
        var widget = new StubPlugin().GetWidgets().First(w => string.Equals(w.ReplacesBuiltIn, "calendar", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(widget.CreateContent());
    }
}
