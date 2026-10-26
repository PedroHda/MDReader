using MDReader.Core;
using Xunit;

namespace MDReader.Core.Tests;

public sealed class ReaderFeaturesTests
{
    [Fact]
    public void FragmentLinksStayInsideTheDocumentDespiteImageBase()
    {
        var document = new MarkdownHtmlRenderer().RenderDocument("# Capítulo\n\n[Ir](#capitulo)", "https://files.mdreader/");
        Assert.Equal("capitulo", document.Headings.Single().Id);
        Assert.Contains("href=\"https://reader.mdreader/index.html#capitulo\"", document.Html);
    }

    [Fact]
    public void NullHistoryEntriesAreIgnored()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"Documents\":[null, {\"Location\":null}],\"Settings\":null}");
            Assert.Empty(new ReaderStateStore(path).State.Documents);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ContentsFollowHeadingOrderAndPreserveFormattedText()
    {
        var document = new MarkdownHtmlRenderer().RenderDocument("# **Primeiro**\n\n## Código `C#`\n\n## Código `C#`");
        Assert.Equal(new[] { "Primeiro", "Código C#", "Código C#" }, document.Headings.Select(h => h.Text));
        Assert.Equal(new[] { 1, 2, 2 }, document.Headings.Select(h => h.Level));
        Assert.Equal(3, document.Headings.Select(h => h.Id).Distinct().Count());
        foreach (var heading in document.Headings) Assert.Contains($"id=\"{heading.Id}\"", document.Html);
    }

    [Fact]
    public void AppearanceIsAppliedAndClamped()
    {
        var settings = new ReaderSettings { DarkMode = true, FontSize = 100, ZoomPercent = -5, ContentWidth = -10 };
        var html = new MarkdownHtmlRenderer().Render("# Olá", settings: settings);
        Assert.Contains("class=\"dark\"", html);
        Assert.Contains("--font-size: 32px", html);
        Assert.Contains("--content-width: 840px", html);
        Assert.Equal(75, settings.ZoomPercent);
    }

    [Theory]
    [InlineData("../private.png")]
    [InlineData("%2e%2e/private.png")]
    [InlineData("assets/%2e%2e/private.png")]
    [InlineData("assets%5csecret.png")]
    [InlineData("file:C:/secret.png")]
    [InlineData("secret.json")]
    [InlineData("secret.md")]
    [InlineData("image.png.exe")]
    [InlineData("assets//photo.png")]
    public void ResourcePathsDoNotEscapeTheSelectedFolder(string path) => Assert.Null(LocalResourcePath.ImageSegments(path));

    [Fact]
    public void ImageNamesAreDecodedForProviderLookup()
    {
        Assert.Equal(new[] { "imagens", "ação.png" }, LocalResourcePath.ImageSegments("/imagens/a%C3%A7%C3%A3o.png"));
    }

    [Fact]
    public void HistoryPreferencesFavoritesAndPositionsSurviveRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new ReaderStateStore(path);
            store.State.Settings.DarkMode = true;
            var first = store.Open("content://docs/first", "primeiro.md");
            first.Favorite = true; first.PreviewPosition = .65; first.RawPosition = .25;
            store.Open("content://docs/second", "segundo.md");
            Assert.True(store.Save());
            var restored = new ReaderStateStore(path);
            Assert.True(restored.State.Settings.DarkMode);
            Assert.Equal("content://docs/second", restored.State.LastDocument);
            Assert.Equal("primeiro.md", restored.Recent.First().FileName);
            Assert.Equal(.65, restored.Recent.First().PreviewPosition);
            Assert.Equal(.25, restored.Recent.First().RawPosition);
            restored.Open("content://docs/first", "novo-nome.md");
            Assert.Equal(2, restored.State.Documents.Count);
            Assert.True(restored.Find("content://docs/first")!.Favorite);
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); }
    }

    [Fact]
    public void CorruptSettingsDoNotPreventOpeningTheApp()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{invalid}");
            var store = new ReaderStateStore(path);
            Assert.Empty(store.State.Documents);
            Assert.NotNull(store.LastError);
            Assert.Equal(16, store.State.Settings.FontSize);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(-3, 0)]
    [InlineData(3, 1)]
    [InlineData(.5, .5)]
    public void ReadingPositionIsAlwaysValid(double input, double expected) => Assert.Equal(expected, ReaderStateStore.ClampPosition(input));
}
