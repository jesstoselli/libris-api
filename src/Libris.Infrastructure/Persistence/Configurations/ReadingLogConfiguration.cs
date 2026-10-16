using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Libris.Infrastructure.Persistence.Configurations;

public class ReadingLogConfiguration : IEntityTypeConfiguration<ReadingLog>
{
    public void Configure(EntityTypeBuilder<ReadingLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reading_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BookId).HasColumnName("book_id");
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.Page).HasColumnName("page");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.BookId, x.Date }).HasDatabaseName("ix_reading_logs_book_date");
    }
}
