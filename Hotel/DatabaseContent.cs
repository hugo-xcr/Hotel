using Microsoft.EntityFrameworkCore;

namespace Hotel
{
    public class DatabaseContent : DbContext
    {
        public DbSet<Users> Users { get; set; }
        public DbSet<Job_title> Job_titles { get; set; }
        private static DatabaseContent _context;

        public DatabaseContent() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(@"Host=172.20.7.53;Database=db3996_17;Username=root;Password=root");
            }
        }

        public static DatabaseContent GetContext()
        {
            return _context ??= new DatabaseContent();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<Job_title>()
                .HasKey(j => j.Id);
        }
    }
}