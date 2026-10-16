namespace Libris.Domain.Entities;

public class ReadingLog
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public DateOnly Date { get; set; }
    public int Page { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
