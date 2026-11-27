using System.IO.Compression;
using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Infrastructure.Context;
using Orbs.Infrastructure.Models;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Orbs.Infrastructure.Tests;

public class DocumentReaderTests
{
    private readonly DocumentReader _reader = new();

    [Theory]
    [InlineData("notes.md", null, "text/markdown")]
    [InlineData("Report.PDF", "application/octet-stream", "application/pdf")]
    [InlineData("photo.jpeg", null, "image/jpeg")]
    [InlineData("data.xyz", "text/plain; charset=utf-8", "text/plain")]
    public void Content_types_come_from_the_extension_first(string name, string? declared, string expected)
    {
        _reader.ContentTypeFor(name, declared).ShouldBe(expected);
    }

    [Fact]
    public async Task Pdfs_are_read_page_by_page()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("The router password is on the sticker", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);

        var text = await _reader.ReadTextAsync("manual.pdf", "application/pdf", builder.Build());

        text.ShouldContain("router password is on the sticker");
    }

    [Fact]
    public async Task Word_documents_are_read_paragraph_by_paragraph()
    {
        var text = await _reader.ReadTextAsync("plan.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Docx("First paragraph", "Second paragraph"));

        text.ShouldBe("First paragraph" + Environment.NewLine + "Second paragraph");
    }

    [Fact]
    public async Task Markdown_is_read_as_utf8_without_a_byte_order_mark()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("# Café\n\nHello")).ToArray();

        (await _reader.ReadTextAsync("a.md", "text/markdown", bytes)).ShouldBe("# Café\n\nHello");
    }

    [Fact]
    public void Images_go_out_as_data_urls_to_openai_compatible_models()
    {
        var request = new ChatRequest("gpt", [new ChatTurn(ChatRole.User, "What is this?", Images: [new ChatImage("image/png", [1, 2, 3])])]);

        var json = OpenAiCompatibleModelAdapter.BuildRequest(request).ToJsonString();

        json.ShouldContain("\"type\":\"image_url\"");
        json.ShouldContain("data:image/png;base64,AQID");
    }

    [Fact]
    public void Images_go_out_as_base64_blocks_to_anthropic()
    {
        var request = new ChatRequest("claude", [new ChatTurn(ChatRole.User, "What is this?", Images: [new ChatImage("image/png", [1, 2, 3])])]);

        var json = AnthropicModelAdapter.BuildRequest(request).ToJsonString();

        json.ShouldContain("\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/png\",\"data\":\"AQID\"}");
    }

    private static byte[] Docx(params string[] paragraphs)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var body = string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t>{p}</w:t></w:r></w:p>"));
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"""<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>""");
        }

        return buffer.ToArray();
    }
}
