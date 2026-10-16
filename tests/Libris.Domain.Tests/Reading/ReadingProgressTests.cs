using FluentAssertions;
using Libris.Domain.Entities;
using Libris.Domain.Reading;

namespace Libris.Domain.Tests.Reading;

public sealed class ReadingProgressTests
{
    private static readonly DateTime Morning = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private static ReadingLog Log(int page, DateOnly date, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Page = page,
        CreatedAt = createdAt ?? Morning,
    };

    [Fact]
    public void Deltas_FirstLogCountsFromZero()
    {
        var deltas = ReadingProgress.Deltas([Log(20, new DateOnly(2026, 10, 2))]);

        deltas.Should().ContainSingle().Which.PagesRead.Should().Be(20);
    }

    [Fact]
    public void Deltas_ChainInDateOrder_RegardlessOfInputOrder()
    {
        var logs = new[]
        {
            Log(86, new DateOnly(2026, 10, 11)),
            Log(20, new DateOnly(2026, 10, 2)),
            Log(51, new DateOnly(2026, 10, 6)),
        };

        var deltas = ReadingProgress.Deltas(logs);

        deltas.Select(d => d.PagesRead).Should().Equal(20, 31, 35);
        deltas.Select(d => d.Log.Page).Should().Equal(20, 51, 86);
    }

    [Fact]
    public void Deltas_SameDate_OrderedByCreation()
    {
        var day = new DateOnly(2026, 10, 14);
        var logs = new[]
        {
            Log(112, day, Morning.AddHours(11)),
            Log(100, day, Morning),
        };

        ReadingProgress.Deltas(logs).Select(d => d.PagesRead).Should().Equal(100, 12);
        ReadingProgress.CurrentPage(logs).Should().Be(112);
    }

    [Fact]
    public void Deltas_LowerPageThanPrevious_CountsZero()
    {
        var logs = new[]
        {
            Log(140, new DateOnly(2026, 10, 14)),
            Log(120, new DateOnly(2026, 10, 15)),
        };

        ReadingProgress.Deltas(logs).Select(d => d.PagesRead).Should().Equal(140, 0);
        ReadingProgress.CurrentPage(logs).Should().Be(120);
    }

    [Fact]
    public void Deltas_TypoThenCorrectionThenProgress_CountsEachPageOnce()
    {
        var logs = new[]
        {
            Log(140, new DateOnly(2026, 10, 14)),
            Log(120, new DateOnly(2026, 10, 15)),
            Log(150, new DateOnly(2026, 10, 16)),
        };

        var deltas = ReadingProgress.Deltas(logs);

        deltas.Select(d => d.PagesRead).Should().Equal(140, 0, 10);
        deltas.Select(d => d.FromPage).Should().Equal(0, 140, 140);
        ReadingProgress.CurrentPage(logs).Should().Be(150);
    }

    [Fact]
    public void CurrentPage_NoLogs_IsZero() => ReadingProgress.CurrentPage([]).Should().Be(0);

    [Fact]
    public void Percent_140Of512_Is27() => ReadingProgress.Percent(140, 512).Should().Be(27);

    [Fact]
    public void Percent_NullTotal_IsZero() => ReadingProgress.Percent(140, null).Should().Be(0);

    [Fact]
    public void Percent_OverTotal_CapsAt100() => ReadingProgress.Percent(600, 512).Should().Be(100);
}
