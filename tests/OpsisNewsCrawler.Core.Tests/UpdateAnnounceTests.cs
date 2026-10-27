using System.Net;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

[Collection("UpdateChecker cache")]
public sealed class UpdateAnnounceTests
{
    [Theory]
    [InlineData("", "v0.15.0", true)]
    [InlineData("v0.14.7", "v0.15.0", true)]
    [InlineData("v0.15.0", "v0.15.0", false)]      // already told: never again for the same version
    [InlineData("v0.15.0", "v0.15.1", true)]
    [InlineData("v0.15.0", "", false)]
    [InlineData(null, null, false)]
    public void AnnouncesOncePerVersion(string? last, string? found, bool expected) =>
        Assert.Equal(expected, UpdateAnnouncePolicy.ShouldAnnounce(last, found));

    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<string?> IfNoneMatch = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IfNoneMatch.Add(request.Headers.TryGetValues("If-None-Match", out var v) ? v.First() : null);
            if (IfNoneMatch.Count == 1)
            {
                var ok = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    { "tag_name": "v9.0.0", "draft": false, "prerelease": false,
                      "html_url": "https://github.com/giorstergiopoulos-design/OpsisNewsCrawler/releases/tag/v9.0.0", "assets": [] }
                    """)
                };
                ok.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"abc\"");
                return Task.FromResult(ok);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        }
    }

    [Fact]
    public async Task SecondCheckSendsTheEtagAndAcceptsA304()
    {
        UpdateChecker.ResetCache();
        var h = new Handler();
        var first = await UpdateChecker.CheckAsync(new Version(0, 1), default, h);
        var second = await UpdateChecker.CheckAsync(new Version(0, 1), default, h);
        Assert.Equal(new Version(9, 0, 0), first!.Version);
        Assert.Equal(new Version(9, 0, 0), second!.Version);       // answered from the remembered release
        Assert.Null(h.IfNoneMatch[0]);
        Assert.Equal("\"abc\"", h.IfNoneMatch[1]);
        UpdateChecker.ResetCache();
    }
}
