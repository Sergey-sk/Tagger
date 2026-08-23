using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using Tagger.model;

namespace Tagger
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<FileRecord> Files { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<SavedSearch> SavedSearches { get; set; } = null!;
        public DbSet<FileTag> FileTags { get; set; } = null!;

        public ApplicationDbContext() : base()
        {

        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    if (!optionsBuilder.IsConfigured)
        //    {
        //        var config = new ConfigurationBuilder()
        //            .SetBasePath(Directory.GetCurrentDirectory())
        //            .AddJsonFile("appsettings.json")
        //            .Build();

        //        optionsBuilder.UseSqlite(config.GetConnectionString("DefaultConnection"));
        //    }
        //}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileRecord>(entity =>
            {
                entity.ToTable("Files");

                entity.HasIndex(fr => fr.Path).IsUnique();
                entity.HasIndex(f => f.Name);
                entity.HasIndex(f => new { f.Path, f.Name });

                entity.HasMany(fr => fr.Tags)
                      .WithMany(t => t.Files)
                      .UsingEntity<FileTag>(
                            l => l.HasOne(ft => ft.Tag)
                                  .WithMany()
                                  .HasForeignKey(ft => ft.TagId)
                                  .OnDelete(DeleteBehavior.Cascade),
                            r => r.HasOne(ft => ft.File)
                                  .WithMany()
                                  .HasForeignKey(ft => ft.FileId)
                                  .OnDelete(DeleteBehavior.Cascade),
                            j =>
                            {
                                j.ToTable("FileTags");
                                j.HasKey(ft => new { ft.FileId, ft.TagId });
                            }
                      );
            });
        }
    }
}
