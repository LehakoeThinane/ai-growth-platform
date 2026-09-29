using AiGrowthPlatform.Business.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiGrowthPlatform.Business.Infrastructure.Data;

public class BusinessDbContext : DbContext
{
    public BusinessDbContext(
        DbContextOptions<BusinessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("business");

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.WebsiteUrl)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(2000);

            entity.Property(x => x.Price)
                .HasPrecision(18, 2);

            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}
