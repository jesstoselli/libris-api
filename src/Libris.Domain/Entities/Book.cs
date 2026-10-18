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
    public DateOnly? StartedAt { get; set; }
    public DateOnly? FinishedAt { get; set; }
    public DateOnly AddedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Genre> Genres { get; } = new List<Genre>();
    public ICollection<ReadingLog> Logs { get; } = new List<ReadingLog>();

    public void ChangeStatus(BookStatus status, DateOnly today)
    {
        EnsureOwned();

        Status = status;
        switch (status)
        {
            case BookStatus.Reading:
                StartedAt ??= today;
                break;
            case BookStatus.Read:
                FinishedAt ??= today;
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
