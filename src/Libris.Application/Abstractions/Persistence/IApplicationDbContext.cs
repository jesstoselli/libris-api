using Microsoft.EntityFrameworkCore;
using Libris.Domain.Entities;

namespace Libris.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Book> Books { get; }
    DbSet<Genre> Genres { get; }
    DbSet<ReadingLog> ReadingLogs { get; }
    DbSet<Cover> Covers { get; }

    Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task AcquireRefreshTokenFamilyLockAsync(Guid familyId, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
