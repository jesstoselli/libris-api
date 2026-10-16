using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Libris.Application.Abstractions.Auth;
using Libris.Application.Abstractions.Persistence;
using Libris.Application.UseCases.Auth;
using Libris.Application.UseCases.Genres;
using Libris.Application.UseCases.Books;
using Libris.Application.UseCases.Reading;
using Libris.Application.UseCases.Covers;
using Libris.Application.Abstractions.Covers;
using Libris.Infrastructure.Covers;
using Libris.Infrastructure.Isbn;
using Libris.Application.Abstractions.Isbn;
using Libris.Application.UseCases.IsbnLookup;
using Libris.Application.Abstractions.Books;
using Libris.Infrastructure.Books;
using Libris.Application.Validators.Auth;
using Libris.Infrastructure.Auth;
using Libris.Infrastructure.Persistence;

namespace Libris.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Resolved lazily from the container so test hosts can inject the
        // connection string after Program.cs has started building services.
        services.AddSingleton(serviceProvider =>
        {
            var connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("Postgres");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");
            }

            return new NpgsqlDataSourceBuilder(connectionString).Build();
        });
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<GenreService>();
        services.AddScoped<BookService>();
        services.AddScoped<ReadingLogService>();

        services.Configure<CoverOptions>(configuration.GetSection(CoverOptions.SectionName));
        services.AddScoped<CoverService>();
        services.AddSingleton<ICoverStorage, FileSystemCoverStorage>();
        services.AddSingleton<ICoverImageProcessor, CoverImageProcessor>();
        services.AddSingleton<ICoverSource, HttpCoverSource>();
        services.AddHttpClient(HttpCoverSource.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<CoverJanitor>();

        services.Configure<IsbnOptions>(configuration.GetSection(IsbnOptions.SectionName));
        const string userAgent = "Libris/1.0 (personal use)";
        services.AddHttpClient<OpenLibraryIsbnSource>(client =>
        {
            client.BaseAddress = new Uri("https://openlibrary.org/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        });
        services.AddHttpClient<GoogleBooksIsbnSource>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/books/v1/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        });
        // Order is priority: Open Library first, Google Books only fills what is missing.
        services.AddTransient<IIsbnSource>(sp => sp.GetRequiredService<OpenLibraryIsbnSource>());
        services.AddTransient<IIsbnSource>(sp => sp.GetRequiredService<GoogleBooksIsbnSource>());
        services.AddScoped<IsbnLookupService>();
        services.AddHostedService(sp => sp.GetRequiredService<CoverJanitor>());
        services.AddSingleton<IBookSearch, PostgresBookSearch>();

        services.AddValidatorsFromAssemblyContaining<SignupRequestValidator>();

        return services;
    }
}
