using FluentValidation;
using Libris.Application.Dtos.Genres;

namespace Libris.Application.Validators.Genres;

public class GenreRequestValidator : AbstractValidator<GenreRequest>
{
    public const int MaxNameLength = 60;

    public GenreRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Nome do gênero é obrigatório.")
            .Must(name => name is null || name.Trim().Length <= MaxNameLength)
            .WithMessage($"Nome do gênero pode ter no máximo {MaxNameLength} caracteres.");
    }
}
