using Microsoft.EntityFrameworkCore;
using System.Transactions;
using GenerateProfilePhotoThumbnail.Domain;

namespace GenerateProfilePhotoThumbnail.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
       : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Data Source=.;Initial Catalog=Profile;User ID=sa;Password=123;Persist Security Info=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("Profile");
            entity.HasKey(l => l.Id);
        });
    }

    public DbSet<Profile> Profile { get; set; }
}
