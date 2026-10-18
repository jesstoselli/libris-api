using FluentAssertions;
using Libris.Domain.Entities;
using Libris.Domain.Enums;

namespace Libris.Domain.Tests.Books;

public sealed class BookRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    private static Book Owned(BookStatus status = BookStatus.NotStarted, int? totalPages = 512) => new()
    {
        Id = Guid.NewGuid(),
        Title = "O Nome da Rosa",
        Authors = ["Umberto Eco"],
        TotalPages = totalPages,
        Ownership = BookOwnership.Owned,
        Status = status,
        AddedAt = new DateOnly(2026, 9, 1),
    };

    private static Book Wishlist(int? totalPages = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Rebecca",
        Authors = ["Daphne du Maurier"],
        TotalPages = totalPages,
        Ownership = BookOwnership.Wishlist,
        AddedAt = new DateOnly(2026, 9, 1),
    };

    private static ReadingLog AddLog(Book book, int page, DateOnly date)
    {
        var log = new ReadingLog { Id = Guid.NewGuid(), BookId = book.Id, Date = date, Page = page, CreatedAt = DateTime.UtcNow };
        book.Logs.Add(log);
        return log;
    }

    [Fact]
    public void ChangeStatus_ToReading_FillsStartedWhenEmpty()
    {
        var book = Owned();

        book.ChangeStatus(BookStatus.Reading, Today);

        book.Status.Should().Be(BookStatus.Reading);
        book.StartedAt.Should().Be(Today);
    }

    [Fact]
    public void ChangeStatus_ToReading_KeepsExistingStarted()
    {
        var book = Owned(BookStatus.Abandoned);
        book.StartedAt = new DateOnly(2026, 10, 2);

        book.ChangeStatus(BookStatus.Reading, Today);

        book.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
    }

    [Fact]
    public void ChangeStatus_ToRead_FillsFinishedWhenEmpty()
    {
        var book = Owned(BookStatus.Reading);

        book.ChangeStatus(BookStatus.Read, Today);

        book.FinishedAt.Should().Be(Today);
    }

    [Fact]
    public void ChangeStatus_ToAbandoned_TouchesNoDates()
    {
        var book = Owned(BookStatus.Reading);
        book.StartedAt = new DateOnly(2026, 10, 2);

        book.ChangeStatus(BookStatus.Abandoned, Today);

        book.Status.Should().Be(BookStatus.Abandoned);
        book.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
        book.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_ToNotStarted_ClearsBothDates()
    {
        var book = Owned(BookStatus.Read);
        book.StartedAt = new DateOnly(2026, 10, 2);
        book.FinishedAt = new DateOnly(2026, 10, 8);

        book.ChangeStatus(BookStatus.NotStarted, Today);

        book.StartedAt.Should().BeNull();
        book.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_OnWishlist_Throws()
    {
        var act = () => Wishlist().ChangeStatus(BookStatus.Reading, Today);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyNewLog_OnNotStarted_SetsReadingAndStartedFromLogDate()
    {
        var book = Owned();
        var log = AddLog(book, 20, new DateOnly(2026, 10, 2));

        book.ApplyNewLog(log);

        book.Status.Should().Be(BookStatus.Reading);
        book.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
    }

    [Fact]
    public void ApplyNewLog_OnNotStartedWithOldLogs_StillStartsReading()
    {
        var book = Owned();
        AddLog(book, 20, new DateOnly(2026, 10, 2));
        var log = AddLog(book, 40, new DateOnly(2026, 10, 10));

        book.ApplyNewLog(log);

        book.Status.Should().Be(BookStatus.Reading);
        book.StartedAt.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void ApplyNewLog_AtLastPage_SetsReadAndFinishedFromLogDate()
    {
        var book = Owned(BookStatus.Reading, totalPages: 246);
        var log = AddLog(book, 246, new DateOnly(2026, 10, 8));

        book.ApplyNewLog(log);

        book.Status.Should().Be(BookStatus.Read);
        book.FinishedAt.Should().Be(new DateOnly(2026, 10, 8));
    }

    [Fact]
    public void ApplyNewLog_AtLastPage_KeepsExistingFinished()
    {
        var book = Owned(BookStatus.Reading, totalPages: 246);
        book.FinishedAt = new DateOnly(2026, 10, 1);
        var log = AddLog(book, 246, new DateOnly(2026, 10, 8));

        book.ApplyNewLog(log);

        book.FinishedAt.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void ApplyNewLog_MidBookWhileReading_ChangesNothing()
    {
        var book = Owned(BookStatus.Reading);
        book.StartedAt = new DateOnly(2026, 10, 2);
        var log = AddLog(book, 140, new DateOnly(2026, 10, 14));

        book.ApplyNewLog(log);

        book.Status.Should().Be(BookStatus.Reading);
        book.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
        book.FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Acquire_SetsOwnedNotStartedAndAddedToday()
    {
        var book = Wishlist(totalPages: 380);

        book.Acquire(Today, totalPages: null).Should().BeTrue();

        book.Ownership.Should().Be(BookOwnership.Owned);
        book.Status.Should().Be(BookStatus.NotStarted);
        book.AddedAt.Should().Be(Today);
    }

    [Fact]
    public void Acquire_WithoutTotalPages_ReturnsFalseAndChangesNothing()
    {
        var book = Wishlist();

        book.Acquire(Today, totalPages: null).Should().BeFalse();

        book.Ownership.Should().Be(BookOwnership.Wishlist);
        book.Status.Should().BeNull();
        book.AddedAt.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void Acquire_WithTotalPagesArgument_SetsIt()
    {
        var book = Wishlist();

        book.Acquire(Today, totalPages: 380).Should().BeTrue();

        book.TotalPages.Should().Be(380);
    }

    [Fact]
    public void Acquire_AlreadyOwned_Throws()
    {
        var act = () => Owned().Acquire(Today, totalPages: null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Formats_ListPhysicalFirst_AndDefaultToPhysical()
    {
        var book = new Book();
        book.Formats.Should().Equal(BookFormat.Physical);

        book.SetFormats([BookFormat.Digital, BookFormat.Physical]);

        book.Formats.Should().Equal(BookFormat.Physical, BookFormat.Digital);
    }

    [Fact]
    public void Formats_NeedAtLeastOne()
    {
        var act = () => new Book().SetFormats([]);

        act.Should().Throw<ArgumentException>();
    }
}
