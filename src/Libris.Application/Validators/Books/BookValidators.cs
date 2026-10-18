using FluentValidation;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Application.Validators.Books;

public class CreateBookRequestValidator : AbstractValidator<CreateBookRequest>
{
    public CreateBookRequestValidator()
    {
        Include(new BookFieldsValidator<CreateBookRequest>(
            x => x.Title, x => x.Authors, x => x.TotalPages, x => x.PublishedYear, x => x.Synopsis,
            x => x.Publisher, x => x.Genres, x => x.StartedAt, x => x.FinishedAt));

        RuleFor(x => x.TotalPages)
            .NotNull().When(x => x.Ownership == BookOwnership.Owned)
            .WithMessage("Número de páginas é obrigatório para livros do acervo.");

        RuleFor(x => x.Status)
            .Null().When(x => x.Ownership == BookOwnership.Wishlist)
            .WithMessage("Livro da wishlist não tem situação de leitura.");

        RuleFor(x => x)
            .Must(x => x.StartedAt is null && x.FinishedAt is null)
            .When(x => x.Ownership == BookOwnership.Wishlist || x.Status is null or BookStatus.NotStarted)
            .WithMessage("Datas de leitura só valem para livros iniciados.");

        RuleFor(x => x.StartedAt)
            .LessThanOrEqualTo(x => x.Today).When(x => x.StartedAt is not null)
            .WithMessage("Data de início não pode estar no futuro.");

        RuleFor(x => x.FinishedAt)
            .LessThanOrEqualTo(x => x.Today).When(x => x.FinishedAt is not null)
            .WithMessage("Data de término não pode estar no futuro.");

        Include(new BookFormatsValidator<CreateBookRequest>(x => x.Formats));
        Include(new BookLanguageValidator<CreateBookRequest>(x => x.Language));

        RuleFor(x => x.CurrentPage)
            .Null()
            .When(x => x.Ownership != BookOwnership.Owned || x.Status is not (BookStatus.Reading or BookStatus.Abandoned))
            .WithMessage("A página lida só vale para livros em leitura ou abandonados.");

        RuleFor(x => x.CurrentPage)
            .Must((x, page) => page >= 1 && page <= x.TotalPages)
            .When(x => x.CurrentPage is not null && x.TotalPages is not null)
            .WithMessage(x => $"A página precisa estar entre 1 e {x.TotalPages}.");
    }
}

public class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookRequestValidator()
    {
        Include(new BookFieldsValidator<UpdateBookRequest>(
            x => x.Title, x => x.Authors, x => x.TotalPages, x => x.PublishedYear, x => x.Synopsis,
            x => x.Publisher, x => x.Genres, x => x.StartedAt, x => x.FinishedAt));

        Include(new BookFormatsValidator<UpdateBookRequest>(x => x.Formats));
        Include(new BookLanguageValidator<UpdateBookRequest>(x => x.Language));
    }
}

/// <summary>Formats may be left out (older clients); when sent, at least one valid format is required.</summary>
internal sealed class BookFormatsValidator<T> : AbstractValidator<T>
{
    public BookFormatsValidator(Func<T, IReadOnlyList<BookFormat>?> formats)
    {
        RuleFor(x => formats(x))
            .NotEmpty().When(x => formats(x) is not null)
            .WithMessage("Escolha pelo menos um formato.")
            .OverridePropertyName("formats");

        RuleForEach(x => formats(x))
            .IsInEnum()
            .WithMessage("Formato inválido.")
            .OverridePropertyName("formats");
    }
}

/// <summary>An ISO 639-1 code ("pt", "en"); left out by older clients.</summary>
internal sealed class BookLanguageValidator<T> : AbstractValidator<T>
{
    public BookLanguageValidator(Func<T, string?> language)
    {
        RuleFor(x => language(x))
            .Must(BookLanguage.IsCode).When(x => language(x) is not null)
            .WithMessage("Idioma inválido.")
            .OverridePropertyName("language");
    }
}

public static class BookLanguage
{
    public static bool IsCode(string? value) => value is { Length: 2 } && value.All(char.IsAsciiLetter);
}

public class AcquireRequestValidator : AbstractValidator<AcquireRequest>
{
    public AcquireRequestValidator()
    {
        RuleFor(x => x.TotalPages).InclusiveBetween(1, BookFieldsLimits.MaxPages).When(x => x.TotalPages is not null);
    }
}

public static class BookFieldsLimits
{
    public const int MaxPages = 20000;
}

internal sealed class BookFieldsValidator<T> : AbstractValidator<T>
{
    public BookFieldsValidator(
        Func<T, string> title,
        Func<T, IReadOnlyList<string>> authors,
        Func<T, int?> totalPages,
        Func<T, int?> publishedYear,
        Func<T, string?> synopsis,
        Func<T, string?> publisher,
        Func<T, IReadOnlyList<string>> genres,
        Func<T, DateOnly?> startedAt,
        Func<T, DateOnly?> finishedAt)
    {
        RuleFor(x => title(x))
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Título é obrigatório.")
            .MaximumLength(300).WithMessage("Título pode ter no máximo 300 caracteres.")
            .OverridePropertyName("title");

        RuleFor(x => authors(x))
            .NotNull().WithMessage("Informe pelo menos um autor.")
            .Must(a => a is { Count: >= 1 and <= 10 }).WithMessage("Informe de 1 a 10 autores.")
            .Must(a => a is null || a.All(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 200))
            .WithMessage("Nome de autor inválido.")
            .OverridePropertyName("authors");

        RuleFor(x => totalPages(x))
            .InclusiveBetween(1, BookFieldsLimits.MaxPages).When(x => totalPages(x) is not null)
            .WithMessage($"Número de páginas deve estar entre 1 e {BookFieldsLimits.MaxPages}.")
            .OverridePropertyName("totalPages");

        RuleFor(x => publishedYear(x))
            .InclusiveBetween(1400, 2100).When(x => publishedYear(x) is not null)
            .WithMessage("Ano de publicação inválido.")
            .OverridePropertyName("publishedYear");

        RuleFor(x => synopsis(x)).MaximumLength(5000).OverridePropertyName("synopsis");
        RuleFor(x => publisher(x)).MaximumLength(200).OverridePropertyName("publisher");

        RuleFor(x => genres(x))
            .Must(g => g is null || (g.Count <= 10 && g.All(n => n is null || n.Trim().Length <= 60)))
            .WithMessage("Use no máximo 10 gêneros, com até 60 caracteres cada.")
            .OverridePropertyName("genres");

        RuleFor(x => x)
            .Must(x => startedAt(x) is null || finishedAt(x) is null || finishedAt(x) >= startedAt(x))
            .WithMessage("Data de término não pode ser anterior à de início.")
            .OverridePropertyName("finishedAt");
    }
}
