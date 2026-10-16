using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Genres;

namespace Libris.Api.Tests.Genres;

public sealed class GenresEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public GenresEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_ThenList_ReturnsGenreWithZeroBooks()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var created = await client.PostAsJsonAsync("/v1/genres", new GenreRequest("Terror"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres");
        list.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "Terror", BookCount = 0 });
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await CreateAsync(client, "Terror");
        await CreateAsync(client, "Fantasia");

        var list = await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres");

        list!.Select(g => g.Name).Should().Equal("Fantasia", "Terror");
    }

    [Fact]
    public async Task Create_SameNameDifferentCase_Returns409()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await CreateAsync(client, "Terror");

        using var response = await client.PostAsJsonAsync("/v1/genres", new GenreRequest("terror"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Create_InvalidName_Returns400(string name)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/genres", new GenreRequest(name));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_ToExistingName_Returns409()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await CreateAsync(client, "Terror");
        var gothic = await CreateAsync(client, "Gótico");

        using var response = await client.PutAsJsonAsync($"/v1/genres/{gothic.Id}", new GenreRequest("TERROR"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rename_ChangesName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var genre = await CreateAsync(client, "Gotico");

        using var response = await client.PutAsJsonAsync($"/v1/genres/{genre.Id}", new GenreRequest("Gótico"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<GenreDto>())!.Name.Should().Be("Gótico");
    }

    [Fact]
    public async Task Delete_RemovesGenre_Returns204()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var genre = await CreateAsync(client, "Terror");

        using var response = await client.DeleteAsync(new Uri($"/v1/genres/{genre.Id}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres")).Should().BeEmpty();
    }

    [Fact]
    public async Task OtherUsersGenre_UpdateAndDelete_Return404()
    {
        using var owner = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var genre = await CreateAsync(owner, "Terror");

        using var update = await stranger.PutAsJsonAsync($"/v1/genres/{genre.Id}", new GenreRequest("Meu"));
        using var delete = await stranger.DeleteAsync(new Uri($"/v1/genres/{genre.Id}", UriKind.Relative));

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetFromJsonAsync<List<GenreDto>>("/v1/genres")).Should().BeEmpty();
    }

    private static async Task<GenreDto> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/v1/genres", new GenreRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GenreDto>())!;
    }
}
