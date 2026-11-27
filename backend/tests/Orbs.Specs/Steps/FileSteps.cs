using System.Net.Http.Headers;
using System.Text;
using Orbs.Application.Context;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class FileSteps(SpecContext context)
{
    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [When("I upload {string} saying {string}")]
    public Task WhenIUpload(string fileName, string text) => UploadAsync(fileName, Encoding.UTF8.GetBytes(text), "text/markdown");

    [Given("I upload the picture {string}")]
    public Task GivenIUploadAPicture(string fileName) => UploadAsync(fileName, TinyPng, "image/png");

    private async Task UploadAsync(string fileName, byte[] data, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        var source = await ApiClient.ReadAsync<ContextSourceDto>(await context.Api.Http.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form));
        context.Sources[source.Name] = source;
    }
}
