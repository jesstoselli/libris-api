using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Covers;
using Libris.Domain.Entities;
using Libris.Infrastructure.Covers;
using Libris.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Libris.Api.Tests.Covers;

public sealed class CoversTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public CoversTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_Jpeg_StoresWebpResizedTo600High()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await UploadAsync(client, await ImageBytesAsync(900, 1350, jpeg: true), "capa.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var cover = await response.ReadAsync<CoverDto>();
        cover.Url.Should().Be($"/v1/covers/{cover.CoverId}");
        using var image = await GetImageAsync(client, cover.Url);
        image.Height.Should().Be(600);
        image.Width.Should().Be(400);
        (await GetFormatAsync(client, cover.Url)).Should().Be(SKEncodedImageFormat.Webp);
    }

    [Fact]
    public async Task Upload_SmallImage_IsNotUpscaled()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await UploadAsync(client, await ImageBytesAsync(200, 300, jpeg: false), "capa.png");

        using var image = await GetImageAsync(client, (await response.ReadAsync<CoverDto>()).Url);
        image.Height.Should().Be(300);
    }

    [Fact]
    public async Task Upload_NotAnImage_Returns400()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await UploadAsync(client, "isso não é uma imagem"u8.ToArray(), "capa.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("invalid_image");
    }

    [Fact]
    public async Task Upload_Over5MB_Returns413()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await UploadAsync(client, new byte[(5 * 1024 * 1024) + 1], "capa.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await UploadAsync(client, await ImageBytesAsync(200, 300, jpeg: true), "capa.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_IsAnonymous_WithImmutableCache()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var upload = await UploadAsync(client, await ImageBytesAsync(200, 300, jpeg: true), "capa.jpg");
        var cover = await upload.ReadAsync<CoverDto>();
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync(new Uri(cover.Url, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        response.Headers.CacheControl!.ToString().Should().Contain("immutable").And.Contain("max-age=31536000");
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync(new Uri($"/v1/covers/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FromUrl_AllowedHost_Downloads()
    {
        var image = await ImageBytesAsync(300, 450, jpeg: true);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(image) });
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(WithCoverDownloads(handler));

        using var response = await client.PostAsJsonAsync(
            "/v1/covers/from-url",
            new CoverFromUrlRequest("https://covers.openlibrary.org/b/isbn/9788501012791-L.jpg?default=false"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        handler.Requests.Should().ContainSingle().Which.Host.Should().Be("covers.openlibrary.org");
    }

    [Theory]
    [InlineData("https://evil.example/x.jpg")]
    [InlineData("http://covers.openlibrary.org/b/isbn/1-L.jpg")]
    [InlineData("https://covers.openlibrary.org.evil.example/x.jpg")]
    [InlineData("not a url")]
    public async Task FromUrl_DisallowedUrl_Returns400(string url)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(WithCoverDownloads(handler));

        using var response = await client.PostAsJsonAsync("/v1/covers/from-url", new CoverFromUrlRequest(url));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("host_not_allowed");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FromUrl_UpstreamFails_Returns502()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(WithCoverDownloads(handler));

        using var response = await client.PostAsJsonAsync(
            "/v1/covers/from-url", new CoverFromUrlRequest("https://books.google.com/books/content?id=x&zoom=1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Book_WithOtherUsersCover_Returns400()
    {
        using var owner = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var cover = await UploadCoverAsync(owner);

        using var create = await stranger.PostAsJsonAsync(
            "/v1/books", LibraryTestData.NewBook() with { CoverId = cover.CoverId }, LibraryTestData.Json);
        var book = await LibraryTestData.CreateBookAsync(stranger);
        using var update = await stranger.PutAsJsonAsync(
            $"/v1/books/{book.Id}", ToUpdate(book) with { CoverId = cover.CoverId }, LibraryTestData.Json);

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(create)).Should().Be("cover_not_found");
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Book_ReplaceCover_DeletesOldFile()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var first = await UploadCoverAsync(client);
        var second = await UploadCoverAsync(client);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { CoverId = first.CoverId });

        using var update = await client.PutAsJsonAsync(
            $"/v1/books/{book.Id}", ToUpdate(book) with { CoverId = second.CoverId }, LibraryTestData.Json);

        update.EnsureSuccessStatusCode();
        File.Exists(CoverFile(first.CoverId)).Should().BeFalse();
        File.Exists(CoverFile(second.CoverId)).Should().BeTrue();
        (await GetStatusAsync(client, first.Url)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Book_Delete_DeletesCoverFile()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var cover = await UploadCoverAsync(client);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { CoverId = cover.CoverId });

        (await client.DeleteAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative))).EnsureSuccessStatusCode();

        File.Exists(CoverFile(cover.CoverId)).Should().BeFalse();
    }

    [Fact]
    public async Task Janitor_RemovesOnlyOldUnreferencedCovers()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var orphanOld = await UploadCoverAsync(client);
        var orphanFresh = await UploadCoverAsync(client);
        var attachedOld = await UploadCoverAsync(client);
        await LibraryTestData.CreateBookAsync(client, r => r with { CoverId = attachedOld.CoverId });
        var now = DateTime.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Covers
                .Where(c => c.Id == orphanOld.CoverId || c.Id == attachedOld.CoverId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, now.AddHours(-25)));
        }

        await _factory.Services.GetRequiredService<CoverJanitor>().SweepAsync(now, CancellationToken.None);

        File.Exists(CoverFile(orphanOld.CoverId)).Should().BeFalse();
        File.Exists(CoverFile(orphanFresh.CoverId)).Should().BeTrue();
        File.Exists(CoverFile(attachedOld.CoverId)).Should().BeTrue();
        await using var check = _factory.Services.CreateAsyncScope();
        var remaining = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Covers.Select(c => c.Id).ToListAsync();
        remaining.Should().NotContain(orphanOld.CoverId).And.Contain(orphanFresh.CoverId);
    }

    private string CoverFile(Guid coverId) => Path.Combine(_factory.CoversPath, coverId.ToString("N") + ".webp");

    private WebApplicationFactory<Program> WithCoverDownloads(StubHandler handler) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(HttpCoverSource.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler)));

    private static async Task<CoverDto> UploadCoverAsync(HttpClient client)
    {
        using var response = await UploadAsync(client, await ImageBytesAsync(200, 300, jpeg: true), "capa.jpg");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<CoverDto>();
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", fileName);
        return await client.PostAsync(new Uri("/v1/covers", UriKind.Relative), content);
    }

    private static Task<byte[]> ImageBytesAsync(int width, int height, bool jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(107, 3, 2));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 90);
        return Task.FromResult(data.ToArray());
    }

    private static async Task<SKBitmap> GetImageAsync(HttpClient client, string url) =>
        SKBitmap.Decode(await client.GetByteArrayAsync(new Uri(url, UriKind.Relative)));

    private static async Task<SKEncodedImageFormat> GetFormatAsync(HttpClient client, string url)
    {
        using var codec = SKCodec.Create(new MemoryStream(await client.GetByteArrayAsync(new Uri(url, UriKind.Relative))));
        return codec.EncodedFormat;
    }

    private static async Task<HttpStatusCode> GetStatusAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        return response.StatusCode;
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

    private static UpdateBookRequest ToUpdate(BookDto book) => new(
        book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear,
        book.Synopsis, book.CoverId, [.. book.Genres.Select(g => g.Name)], book.StartedAt, book.FinishedAt);
}
