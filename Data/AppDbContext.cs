using InsureZen.Models;
using Microsoft.EntityFrameworkCore;

namespace InsureZen.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // These are your database tables
    public DbSet<Claim> Claims { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<InsuranceCompany> InsuranceCompanies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CLAIM table configuration
        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(c => c.Id);

            // This tells PostgreSQL to auto-increment RowVersion on every update
            // This is what prevents two Makers grabbing the same claim
            entity.UseXminAsConcurrencyToken();

            // Maker relationship
            entity.HasOne(c => c.Maker)
                  .WithMany(u => u.MakerClaims)
                  .HasForeignKey(c => c.MakerId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Checker relationship
            entity.HasOne(c => c.Checker)
                  .WithMany(u => u.CheckerClaims)
                  .HasForeignKey(c => c.CheckerId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Insurance company relationship
            entity.HasOne(c => c.InsuranceCompany)
                  .WithMany(ic => ic.Claims)
                  .HasForeignKey(c => c.InsuranceCompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Store enums as strings in DB so it's readable
            entity.Property(c => c.Status)
                  .HasConversion<string>();
            entity.Property(c => c.MakerRecommendation)
                  .HasConversion<string>();
            entity.Property(c => c.CheckerDecision)
                  .HasConversion<string>();

            // Index for fast filtering
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.InsuranceCompanyId);
            entity.HasIndex(c => c.CreatedAt);
        });

        // USER table configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>();
        });

        // INSURANCE COMPANY table configuration
        modelBuilder.Entity<InsuranceCompany>(entity =>
        {
            entity.HasKey(ic => ic.Id);
            entity.HasIndex(ic => ic.Name).IsUnique();
        });

        // Seed some initial data so we can test immediately
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed insurance companies
        var company1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var company2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        modelBuilder.Entity<InsuranceCompany>().HasData(
            new InsuranceCompany
            {
                Id = company1Id,
                Name = "AlphaShield Insurance",
                ContactEmail = "claims@alphashield.com",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new InsuranceCompany
            {
                Id = company2Id,
                Name = "BetaCare Health",
                ContactEmail = "claims@betacare.com",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed users - 2 Makers and 2 Checkers
        var maker1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var maker2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var checker1Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var checker2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = maker1Id,
                Name = "Alice Maker",
                Email = "alice@insurezen.com",
                Role = UserRole.Maker,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = maker2Id,
                Name = "Bob Maker",
                Email = "bob@insurezen.com",
                Role = UserRole.Maker,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = checker1Id,
                Name = "Carol Checker",
                Email = "carol@insurezen.com",
                Role = UserRole.Checker,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = checker2Id,
                Name = "David Checker",
                Email = "david@insurezen.com",
                Role = UserRole.Checker,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}