using Microsoft.EntityFrameworkCore;
using System.Transactions;
using ProfileJobs.Domain;

namespace ProfileJobs.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        private Guid Id { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            Id = Guid.NewGuid();
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

}
