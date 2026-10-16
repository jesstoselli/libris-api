using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Libris.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("books");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Authors).HasColumnName("authors").IsRequired();
        builder.Property(x => x.Isbn).HasColumnName("isbn").HasMaxLength(13);
        builder.Property(x => x.TotalPages).HasColumnName("total_pages");
        builder.Property(x => x.Publisher).HasColumnName("publisher").HasMaxLength(200);
        builder.Property(x => x.PublishedYear).HasColumnName("published_year");
        builder.Property(x => x.Synopsis).HasColumnName("synopsis").HasMaxLength(5000);
        builder.Property(x => x.CoverId).HasColumnName("cover_id");
        builder.Property(x => x.Ownership).HasColumnName("ownership").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at");
        builder.Property(x => x.AddedAt).HasColumnName("added_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Cover>().WithMany().HasForeignKey(x => x.CoverId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Logs).WithOne().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Genres).WithMany(x => x.Books).UsingEntity(
            "book_genres",
            r => r.HasOne(typeof(Genre)).WithMany().HasForeignKey("genre_id").OnDelete(DeleteBehavior.Cascade),
            l => l.HasOne(typeof(Book)).WithMany().HasForeignKey("book_id").OnDelete(DeleteBehavior.Cascade));

        builder.HasIndex(x => new { x.UserId, x.Isbn })
            .IsUnique()
            .HasFilter("isbn IS NOT NULL")
            .HasDatabaseName("ix_books_user_isbn");
    }
}
