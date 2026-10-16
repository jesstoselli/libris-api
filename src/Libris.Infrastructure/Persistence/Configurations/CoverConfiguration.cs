using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Libris.Infrastructure.Persistence.Configurations;

public class CoverConfiguration : IEntityTypeConfiguration<Cover>
{
    public void Configure(EntityTypeBuilder<Cover> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("covers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
