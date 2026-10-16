namespace Libris.Domain.Entities;

public class Cover
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
