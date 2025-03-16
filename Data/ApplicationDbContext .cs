using Microsoft.EntityFrameworkCore;
using EReaderApp.Models;

namespace EReaderApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Library> Libraries { get; set; }
        public DbSet<Publication> Publications { get; set; }
        public DbSet<Note> Notes { get; set; }


        // Add these missing DbSet properties:
        public DbSet<BookCategory> BookCategories { get; set; }
        public DbSet<LibraryBook> LibraryBooks { get; set; }
        public DbSet<PublicationLike> PublicationLikes { get; set; }
        public DbSet<ReaderSettings> ReaderSettings { get; set; }

        // Optionally, configure the relationships in OnModelCreating
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure composite keys
            modelBuilder.Entity<BookCategory>()
                .HasKey(bc => new { bc.FKIdCategory, bc.FKIdBook });

            modelBuilder.Entity<LibraryBook>()
                .HasKey(lb => new { lb.FKIdLibrary, lb.FKIdBook });

            modelBuilder.Entity<PublicationLike>()
                .HasKey(pl => new { pl.FKIdUser, pl.FKIdPublication });

            // Disable cascade delete for all User-related relationships
            modelBuilder.Entity<Publication>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Library>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PublicationLike>()
                .HasOne(pl => pl.User)
                .WithMany()
                .HasForeignKey(pl => pl.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PublicationLike>()
                .HasOne(pl => pl.Publication)
                .WithMany()
                .HasForeignKey(pl => pl.FKIdPublication)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Note>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReaderSettings>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.NoAction);
        }

    }
}
