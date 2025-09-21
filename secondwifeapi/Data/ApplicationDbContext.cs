using Microsoft.EntityFrameworkCore;
using secondwifeapi.Models;

namespace secondwifeapi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseItem> ExpenseItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.DisplayName).HasMaxLength(200);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

                // Create unique index on Username
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            });

            // Configure Group entity
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.AdminUserId).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

                // Configure relationship with admin user
                entity.HasOne(e => e.AdminUser)
                    .WithMany()
                    .HasForeignKey(e => e.AdminUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure GroupMember entity
            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GroupId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.JoinedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

                // Configure relationships
                entity.HasOne(e => e.Group)
                    .WithMany(g => g.GroupMembers)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Create unique index to prevent duplicate memberships
                entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            });

            // Configure Expense entity
            modelBuilder.Entity<Expense>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.ExpenseId);
                
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.GroupId).IsRequired();
                entity.Property(e => e.ExpenseDate).IsRequired();
                entity.Property(e => e.VendorName).HasMaxLength(200);
                entity.Property(e => e.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

                // Configure relationships
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Group)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Create indexes for performance
                entity.HasIndex(e => new { e.UserId, e.ExpenseDate });
                entity.HasIndex(e => e.GroupId);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Configure ExpenseItem entity
            modelBuilder.Entity<ExpenseItem>(entity =>
            {
                entity.HasKey(e => e.ExpenseItemId);
                
                entity.Property(e => e.ExpenseId).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

                // Configure relationship with Expense
                entity.HasOne(e => e.Expense)
                    .WithMany(ex => ex.ExpenseItems)
                    .HasForeignKey(e => e.ExpenseId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for performance
                entity.HasIndex(e => e.ExpenseId);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}