using LibraryRecommendation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryRecommendation.Api.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.Property(b => b.Title).HasMaxLength(200).IsRequired();
            entity.Property(b => b.Category).HasMaxLength(BookCategories.MaxLength).IsRequired();
            entity.Property(b => b.Author).HasMaxLength(150).IsRequired();
            entity.Property(b => b.Genre).HasMaxLength(100).IsRequired();
            entity.Property(b => b.Description).HasMaxLength(2000).IsRequired();

            entity.HasIndex(b => b.Genre);
            entity.HasIndex(b => b.Category);
        });
    }
}
