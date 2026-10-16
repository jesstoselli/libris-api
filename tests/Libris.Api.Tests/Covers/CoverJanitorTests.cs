using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Abstractions.Covers;
using Libris.Application.Dtos.Covers;
using Libris.Infrastructure.Covers;
using Libris.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Libris.Api.Tests.Covers;

public sealed class CoverJanitorTests
{
    [Fact]
    public async Task Janitor_RunsOnConfiguredSchedule()
    {
        var factory = ApiWebApplicationFactory.Create(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Covers:JanitorStartupDelaySeconds"] = "1",
            ["Covers:JanitorIntervalSeconds"] = "1",
        });
        await factory.InitializeAsync();
        try
        {
            using var client = await AuthTestClient.CreateAuthorizedClientAsync(factory);
            var cover = await UploadAsync(client);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Covers
                    .Where(c => c.Id == cover.CoverId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, DateTime.UtcNow.AddHours(-25)));
            }

            var file = Path.Combine(factory.CoversPath, cover.CoverId.ToString("N") + ".webp");
            for (var i = 0; i < 50 && File.Exists(file); i++)
            {
                await Task.Delay(200);
            }

            File.Exists(file).Should().BeFalse();
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task TrySweep_DatabaseDown_ReturnsFalseInsteadOfThrowing()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1"));
        await using var provider = services.BuildServiceProvider();
        var janitor = new CoverJanitor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new NoopStorage(),
            Options.Create(new CoverOptions()),
            NullLogger<CoverJanitor>.Instance);

        var swept = await janitor.TrySweepAsync(DateTime.UtcNow, CancellationToken.None);

        swept.Should().BeFalse();
    }

    private static async Task<CoverDto> UploadAsync(HttpClient client)
    {
        using var bitmap = new SKBitmap(200, 300);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(data.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "capa.png");
        using var response = await client.PostAsync(new Uri("/v1/covers", UriKind.Relative), content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CoverDto>())!;
    }

    private sealed class NoopStorage : ICoverStorage
    {
        public Task SaveAsync(Guid coverId, Stream webp, CancellationToken ct) => Task.CompletedTask;

        public Stream? OpenRead(Guid coverId) => null;

        public void Delete(Guid coverId)
        {
        }
    }
}
