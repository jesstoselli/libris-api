using FluentAssertions;
using Libris.Infrastructure.Isbn;

namespace Libris.Api.Tests.Isbn;

public sealed class AuthorNamesTests
{
    [Theory]
    [InlineData("OZ, AMOS", "Amos Oz")]
    [InlineData("ASSIS, MACHADO DE", "Machado de Assis")]
    [InlineData("LISPECTOR, CLARICE", "Clarice Lispector")]
    [InlineData("Itamar Vieira Jr.", "Itamar Vieira Jr.")]
    [InlineData("George Orwell", "George Orwell")]
    [InlineData("  Conceição Evaristo ", "Conceição Evaristo")]
    public void Normalize_CatalogStyleBecomesReadingOrder(string raw, string expected) =>
        AuthorNames.Normalize(raw).Should().Be(expected);
}
