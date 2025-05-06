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
        public DbSet<BookCategory> BookCategories { get; set; }
        public DbSet<LibraryBook> LibraryBooks { get; set; }
        public DbSet<PublicationLike> PublicationLikes { get; set; }
        public DbSet<ReaderSettings> ReaderSettings { get; set; }
        public DbSet<Bookmark> BookMarks { get; set; }
        public DbSet<ReadingState> ReadingStates { get; set; }
        public DbSet<ReadingActivity> ReadingActivities { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<ReviewLike> ReviewLikes { get; set; }



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
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReaderSettings>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.Book)
                .WithMany()
                .HasForeignKey(b => b.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReadingState>()
                .HasOne(rs => rs.User)
                .WithMany()
                .HasForeignKey(rs => rs.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReadingState>()
                .HasOne(rs => rs.Book)
                .WithMany()
                .HasForeignKey(rs => rs.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReadingActivity>()
                .HasOne(ra => ra.User)
                .WithMany()
                .HasForeignKey(ra => ra.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReadingActivity>()
                .HasOne(ra => ra.Book)
                .WithMany()
                .HasForeignKey(ra => ra.BookId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Publication)
                .WithMany()
                .HasForeignKey(c => c.FKIdPublication)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewLike>()
                .HasKey(rl => new { rl.FKIdUser, rl.FKIdReview });

            modelBuilder.Entity<ReviewLike>()
                .HasOne(rl => rl.User)
                .WithMany()
                .HasForeignKey(rl => rl.FKIdUser)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReviewLike>()
                .HasOne(rl => rl.Review)
                .WithMany()
                .HasForeignKey(rl => rl.FKIdReview)
                .OnDelete(DeleteBehavior.NoAction);

        }

    }
}
