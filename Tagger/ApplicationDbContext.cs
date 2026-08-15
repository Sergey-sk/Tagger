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

                entity.HasMany(fr => fr.Tags)
                       .WithMany(t => t.Files)
                       .UsingEntity(j => j.ToTable("FileTags")
                           .HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                           j => j.HasOne(typeof(FileRecord)).WithMany().HasForeignKey("FileId").OnDelete(DeleteBehavior.Cascade));

                entity.HasIndex(f => f.Name);
                entity.HasIndex(f => new { f.Path, f.Name });
            });

            modelBuilder.Entity<Tag>().Ignore(t => t.IsSelected);
        }
    }
}
