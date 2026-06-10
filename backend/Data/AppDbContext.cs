using Microsoft.EntityFrameworkCore;
using UMEProje.Models;

namespace UMEProje.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<LabClient> LabClients { get; set; }
        public DbSet<CalibrationSurvey> CalibrationSurveys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // LabClient configuration
            modelBuilder.Entity<LabClient>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.TaxNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ContactEmail).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // One-to-Many: One LabClient has many CalibrationSurveys
                entity.HasMany(e => e.CalibrationSurveys)
                    .WithOne(s => s.LabClient)
                    .HasForeignKey(s => s.LabClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CalibrationSurvey configuration
            modelBuilder.Entity<CalibrationSurvey>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.LabCategory).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // Foreign Key
                entity.HasOne(e => e.LabClient)
                    .WithMany(c => c.CalibrationSurveys)
                    .HasForeignKey(e => e.LabClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
