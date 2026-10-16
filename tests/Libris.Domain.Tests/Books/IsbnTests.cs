using FluentAssertions;
using Libris.Domain.Books;

namespace Libris.Domain.Tests.Books;

public sealed class IsbnTests
{
    [Theory]
    [InlineData("978-85-01-01279-1", "9788501012791")]
    [InlineData(" 9788501012791 ", "9788501012791")]
    [InlineData("0-15-144647-4", "9780151446476")]
    [InlineData("080442957X", "9780804429573")]
    public void TryNormalize_Valid_ReturnsIsbn13(string input, string expected)
    {
        Isbn.TryNormalize(input, out var isbn13).Should().BeTrue();
        isbn13.Should().Be(expected);
    }

    [Theory]
    [InlineData("9788501012796")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("97885010127AB")]
    public void TryNormalize_Invalid_ReturnsFalse(string? input)
    {
        Isbn.TryNormalize(input, out _).Should().BeFalse();
    }
}
