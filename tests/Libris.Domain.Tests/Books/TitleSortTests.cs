using FluentAssertions;
using Libris.Domain.Books;

namespace Libris.Domain.Tests.Books;

public sealed class TitleSortTests
{
    [Theory]
    [InlineData("O Colecionador de Ossos", "pt", "Colecionador de Ossos")]
    [InlineData("A Cabeça do Santo", "pt", "Cabeça do Santo")]
    [InlineData("Os Sertões", "pt", "Sertões")]
    [InlineData("Um Defeito de Cor", "pt", "Defeito de Cor")]
    [InlineData("The Name of the Rose", "en", "Name of the Rose")]
    [InlineData("An Officer and a Spy", "en", "Officer and a Spy")]
    [InlineData("A Study in Scarlet", "en", "Study in Scarlet")]
    [InlineData("El Túnel", "es", "Túnel")]
    [InlineData("L'Étranger", "fr", "Étranger")]
    [InlineData("Les Misérables", "fr", "Misérables")]
    [InlineData("Il Nome della Rosa", "it", "Nome della Rosa")]
    [InlineData("Der Prozess", "de", "Prozess")]
    [InlineData("the road", "en", "road")]
    public void Key_DropsTheLanguagesLeadingArticle(string title, string language, string expected) =>
        TitleSort.Key(title, language).Should().Be(expected);

    [Theory]
    [InlineData("O Pioneers!", "en")]
    [InlineData("The Hobbit", "pt")]
    [InlineData("Ossos", "pt")]
    [InlineData("Orgulho e Preconceito", "pt")]
    [InlineData("Theodora", "en")]
    [InlineData("Anathem", "en")]
    [InlineData("Livro", "nl")]
    public void Key_KeepsTitlesWithoutThatLanguagesArticle(string title, string language) =>
        TitleSort.Key(title, language).Should().Be(title);

    [Fact]
    public void Key_KeepsATitleThatIsOnlyTheArticle() =>
        TitleSort.Key("O", "pt").Should().Be("O");
}
