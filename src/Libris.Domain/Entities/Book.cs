using Libris.Domain.Enums;

namespace Libris.Domain.Entities;

public class Book
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public List<string> Authors { get; set; } = [];
    public string? Isbn { get; set; }
    public int? TotalPages { get; set; }
    public string? Publisher { get; set; }
    public int? PublishedYear { get; set; }
    public string? Synopsis { get; set; }
    public Guid? CoverId { get; set; }
    public BookOwnership Ownership { get; set; }

    /// <summary>Formats are two flags rather than a list: a book can be both, and "has a digital copy" stays a simple filter.</summary>
    public bool IsPhysical { get; set; } = true;
    public bool IsDigital { get; set; }

    /// <summary>ISO 639-1 ("pt", "en"…). Everything registered before languages existed is Portuguese.</summary>
    public string Language { get; set; } = "pt";
    public BookStatus? Status { get; set; }

    /// <summary>The series as the reader names it; the number may be a decimal (1.5 for a novella) or 0 for a prequel.</summary>
    public string? Series { get; private set; }
    public decimal? SeriesNumber { get; private set; }

    /// <summary>0.5 to 5 in halves; only a book read or given up on is rated, and changing its status keeps the rating.</summary>
    public decimal? Rating { get; private set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? FinishedAt { get; set; }
    public DateOnly AddedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Genre> Genres { get; } = new List<Genre>();
    public ICollection<ReadingLog> Logs { get; } = new List<ReadingLog>();

    /// <param name="withoutDate">A book read long ago: "Lido" without a finish, so it stays out of the monthly numbers.</param>
    /// <param name="finishedOn">"Lido" on a remembered day, which replaces any finish the book had.</param>
    /// <summary>A blank name takes the book out of its series, number included.</summary>
    public void SetSeries(string? name, decimal? number)
    {
        var trimmed = name?.Trim();
        Series = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        SeriesNumber = Series is null ? null : number;
    }

    /// <returns>false for a book that cannot be rated (on the wishlist, or not read nor given up on).</returns>
    public bool Rate(decimal? rating)
    {
        if (rating is not null && (Ownership != BookOwnership.Owned || Status is not (BookStatus.Read or BookStatus.Abandoned)))
        {
            return false;
        }

        Rating = rating;
        return true;
    }

    public void ChangeStatus(BookStatus status, DateOnly today, bool withoutDate = false, DateOnly? finishedOn = null)
    {
        EnsureOwned();

        Status = status;
        switch (status)
        {
            case BookStatus.Reading:
                StartedAt ??= today;
                break;
            case BookStatus.Read:
                if (finishedOn is { } day)
                {
                    FinishedAt = day;
                }
                else if (!withoutDate)
                {
                    FinishedAt ??= today;
                }

                break;
            case BookStatus.NotStarted:
                StartedAt = null;
                FinishedAt = null;
                break;
            case BookStatus.Abandoned:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    public void ApplyNewLog(ReadingLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        EnsureOwned();

        if (Status == BookStatus.NotStarted)
        {
            Status = BookStatus.Reading;
            StartedAt ??= log.Date;
        }

        if (TotalPages is { } total && log.Page >= total)
        {
            Status = BookStatus.Read;
            FinishedAt ??= log.Date;
        }
    }

    /// <summary>Moves a wishlist book into the collection ("Comprei").</summary>
    /// <returns>false when the page count is still unknown; owned books need one for progress.</returns>
    public bool Acquire(DateOnly today, int? totalPages)
    {
        if (Ownership == BookOwnership.Owned)
        {
            throw new InvalidOperationException("Book is already owned.");
        }

        var pages = totalPages ?? TotalPages;
        if (pages is null)
        {
            return false;
        }

        TotalPages = pages;
        Ownership = BookOwnership.Owned;
        Status = BookStatus.NotStarted;
        AddedAt = today;
        return true;
    }

    /// <summary>Physical first, so every client lists them in the same order.</summary>
    public IReadOnlyList<BookFormat> Formats =>
        [.. new[] { (IsPhysical, BookFormat.Physical), (IsDigital, BookFormat.Digital) }.Where(f => f.Item1).Select(f => f.Item2)];

    public void SetFormats(IReadOnlyCollection<BookFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        if (formats.Count == 0)
        {
            throw new ArgumentException("A book needs at least one format.", nameof(formats));
        }

        IsPhysical = formats.Contains(BookFormat.Physical);
        IsDigital = formats.Contains(BookFormat.Digital);
    }

    private void EnsureOwned()
    {
        if (Ownership != BookOwnership.Owned)
        {
            throw new InvalidOperationException("Only owned books have a reading status.");
        }
    }
}
