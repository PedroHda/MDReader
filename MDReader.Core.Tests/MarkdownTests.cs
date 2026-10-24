using System.Text;
using MDReader.Core;
using Xunit;

namespace MDReader.Core.Tests;

public sealed class MarkdownTests
{
    [Theory]
    [InlineData("# Título", "<h1")]
    [InlineData("**negrito**", "<strong>negrito</strong>")]
    [InlineData("*itálico*", "<em>itálico</em>")]
    [InlineData("~~tachado~~", "<del>tachado</del>")]
    [InlineData("- um\n- dois", "<ul>")]
    [InlineData("1. um\n2. dois", "<ol>")]
    [InlineData("> citação", "<blockquote>")]
    [InlineData("`código`", "<code>código</code>")]
    [InlineData("```csharp\nConsole.WriteLine(1);\n```", "language-csharp")]
    [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 |", "<table>")]
    [InlineData("- [x] feito\n- [ ] pendente", "type=\"checkbox\"")]
    [InlineData("[Site](https://example.com)", "href=\"https://example.com\"")]
    [InlineData("![Foto](https://example.com/foto.png)", "<img")]
    [InlineData("---", "<hr")]
    public void RendersMarkdownFeatures(string markdown, string expectedHtml)
    {
        Assert.Contains(expectedHtml, new MarkdownHtmlRenderer().Render(markdown));
    }

    [Fact]
    public void MobileHtmlDoesNotExecuteEmbeddedMarkup()
    {
        var html = new MarkdownHtmlRenderer().Render("<script>alert(1)</script>\n\n<iframe src='file:///private'></iframe>");
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe ", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Security-Policy", html);
        Assert.Contains("default-src 'none'", html);
        Assert.Contains("name=\"viewport\"", html);
    }

    [Fact]
    public void DesktopRetainsRelativeResourceBaseAndEmbeddedHtml()
    {
        var html = new MarkdownHtmlRenderer().Render("<b>HTML</b>", "file:///C:/docs/?a=1&b=2", allowEmbeddedHtml: true);
        Assert.Contains("<b>HTML</b>", html);
        Assert.Contains("file:///C:/docs/?a=1&amp;b=2", html);
        Assert.DoesNotContain("Content-Security-Policy", html);
    }

    [Theory]
    [InlineData("arquivo.md", true)]
    [InlineData("arquivo.MD", true)]
    [InlineData("arquivo.Markdown", true)]
    [InlineData("arquivo.md.exe", false)]
    [InlineData("arquivo.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ChecksTheActualExtension(string? name, bool expected)
    {
        Assert.Equal(expected, MarkdownContentReader.HasMarkdownExtension(name));
    }

    [Fact]
    public async Task ReadsUtf8WithoutChangingLineBreaksAndLeavesSourceOpen()
    {
        const string expected = "# Ação\r\n\n**conteúdo**";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(expected));
        Assert.Equal(expected, await MarkdownContentReader.ReadAsync(stream, "documento.md"));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task DetectsUtf16Bom()
    {
        const string expected = "# Olá";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(expected)).ToArray();
        using var stream = new MemoryStream(bytes);
        Assert.Equal(expected, await MarkdownContentReader.ReadAsync(stream, "documento.markdown"));
    }

    [Fact]
    public async Task EmptyFileIsValid()
    {
        using var stream = new MemoryStream();
        Assert.Equal(string.Empty, await MarkdownContentReader.ReadAsync(stream, "vazio.md"));
    }

    [Fact]
    public async Task RejectsInvalidExtensionsBeforeReading()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("texto"));
        await Assert.ThrowsAsync<InvalidDataException>(() => MarkdownContentReader.ReadAsync(stream, "arquivo.txt"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task RejectsBinaryContent()
    {
        using var stream = new MemoryStream(new byte[] { 65, 0, 66 });
        await Assert.ThrowsAsync<InvalidDataException>(() => MarkdownContentReader.ReadAsync(stream, "binario.md"));
    }

    [Fact]
    public async Task RejectsInvalidUtf8()
    {
        using var stream = new MemoryStream(new byte[] { 0xC3, 0x28 });
        await Assert.ThrowsAsync<DecoderFallbackException>(() => MarkdownContentReader.ReadAsync(stream, "texto.md"));
    }

    [Fact]
    public async Task EnforcesMaximumSizeEvenWhenSourceLengthIsUnknown()
    {
        using var stream = new NonSeekableStream(new byte[MarkdownContentReader.MaximumBytes + 1]);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => MarkdownContentReader.ReadAsync(stream, "grande.md"));
        Assert.Contains("5 MB", error.Message);
    }

    [Fact]
    public async Task AcceptsFileExactlyAtSizeLimit()
    {
        var bytes = Enumerable.Repeat((byte)'a', MarkdownContentReader.MaximumBytes).ToArray();
        using var stream = new MemoryStream(bytes);
        Assert.Equal(MarkdownContentReader.MaximumBytes, (await MarkdownContentReader.ReadAsync(stream, "limite.md")).Length);
    }

    [Fact]
    public async Task SupportsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("texto"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MarkdownContentReader.ReadAsync(stream, "arquivo.md", cancellation.Token));
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
