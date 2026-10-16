using Libris.Application.Abstractions.Covers;
using Libris.Application.UseCases.Covers;
using Microsoft.Extensions.Options;

namespace Libris.Infrastructure.Covers;

public sealed class HttpCoverSource : ICoverSource
{
    public const string HttpClientName = "covers";

    private readonly IHttpClientFactory _httpClients;
    private readonly IOptions<CoverOptions> _options;

    public HttpCoverSource(IHttpClientFactory httpClients, IOptions<CoverOptions> options)
    {
        _httpClients = httpClients;
        _options = options;
    }

    public async Task<CoverDownload> DownloadAsync(string url, CancellationToken ct)
    {
        // Only https and exact allow-listed hosts: the API must not become a proxy to arbitrary (or internal) URLs.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !_options.Value.AllowedDownloadHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return new CoverDownload(CoverDownloadStatus.HostNotAllowed, null);
        }

        try
        {
            using var client = _httpClients.CreateClient(HttpClientName);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new CoverDownload(CoverDownloadStatus.Failed, null);
            }

            if (response.Content.Headers.ContentLength > CoverService.MaxUploadBytes)
            {
                return new CoverDownload(CoverDownloadStatus.TooLarge, null);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length > CoverService.MaxUploadBytes
                ? new CoverDownload(CoverDownloadStatus.TooLarge, null)
                : new CoverDownload(CoverDownloadStatus.Ok, bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new CoverDownload(CoverDownloadStatus.Failed, null);
        }
    }
}
