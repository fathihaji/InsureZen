using InsureZen.Data;
using InsureZen.DTOs;
using InsureZen.Models;
using InsureZen.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsureZen.Tests;

public class ClaimServiceTests
{
    // Helper that creates a fresh in-memory database for each test
    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    // Helper that seeds the minimum data needed
    private async Task SeedAsync(AppDbContext db)
    {
        db.InsuranceCompanies.Add(new InsuranceCompany
        {
            Id = TestData.CompanyId,
            Name = "Test Insurance",
            ContactEmail = "test@insurance.com"
        });
        db.Users.Add(new User
        {
            Id = TestData.MakerId,
            Name = "Test Maker",
            Email = "maker@test.com",
            Role = UserRole.Maker
        });
        db.Users.Add(new User
        {
            Id = TestData.CheckerId,
            Name = "Test Checker",
            Email = "checker@test.com",
            Role = UserRole.Checker
        });
        await db.SaveChangesAsync();
    }

    // ── TEST 1: New claim should have Submitted status ─────────────────
    [Fact]
    public async Task CreateClaim_ShouldHaveSubmittedStatus()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        var result = await service.CreateClaimAsync(new CreateClaimRequest
        {
            InsuranceCompanyId = TestData.CompanyId,
            PatientName = "John Doe",
            PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PolicyNumber = "POL-001",
            ClaimType = "Hospitalisation",
            ClaimAmount = 5000,
            IncidentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Test claim"
        });

        Assert.Equal("Submitted", result.Status);
    }

    // ── TEST 2: Maker pickup should change status to UnderMakerReview ──
    [Fact]
    public async Task PickupAsMaker_ShouldChangeStatusToUnderMakerReview()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        var claim = await service.CreateClaimAsync(new CreateClaimRequest
        {
            InsuranceCompanyId = TestData.CompanyId,
            PatientName = "Jane Doe",
            PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PolicyNumber = "POL-002",
            ClaimType = "Dental",
            ClaimAmount = 1000,
            IncidentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Dental work"
        });

        var result = await service.PickupClaimAsMakerAsync(claim.Id, TestData.MakerId);

        Assert.Equal("UnderMakerReview", result.Status);
        Assert.Equal("Test Maker", result.MakerName);
    }

    // ── TEST 3: Non-maker cannot pick up a claim ───────────────────────
    [Fact]
    public async Task PickupAsMaker_WithCheckerUser_ShouldThrowUnauthorized()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        var claim = await service.CreateClaimAsync(new CreateClaimRequest
        {
            InsuranceCompanyId = TestData.CompanyId,
            PatientName = "Bob Smith",
            PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PolicyNumber = "POL-003",
            ClaimType = "Outpatient",
            ClaimAmount = 500,
            IncidentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Outpatient visit"
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.PickupClaimAsMakerAsync(claim.Id, TestData.CheckerId));
    }

    // ── TEST 4: Maker submits recommendation ──────────────────────────
    [Fact]
    public async Task MakerReview_ShouldChangeToPendingCheckerReview()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        var claim = await service.CreateClaimAsync(new CreateClaimRequest
        {
            InsuranceCompanyId = TestData.CompanyId,
            PatientName = "Alice Brown",
            PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PolicyNumber = "POL-004",
            ClaimType = "Surgery",
            ClaimAmount = 8000,
            IncidentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Surgery"
        });

        await service.PickupClaimAsMakerAsync(claim.Id, TestData.MakerId);

        var result = await service.SubmitMakerReviewAsync(claim.Id, TestData.MakerId,
            new MakerReviewRequest
            {
                Recommendation = "Approve",
                Notes = "Looks good"
            });

        Assert.Equal("PendingCheckerReview", result.Status);
        Assert.Equal("Approve", result.MakerRecommendation);
    }

    // ── TEST 5: Maker cannot review claim they didn't pick up ─────────
    [Fact]
    public async Task MakerReview_ByWrongMaker_ShouldThrowUnauthorized()
    {
        var db = CreateDb();
        await SeedAsync(db);

        var maker2Id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = maker2Id,
            Name = "Other Maker",
            Email = "other@maker.com",
            Role = UserRole.Maker
        });
        await db.SaveChangesAsync();

        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        var claim = await service.CreateClaimAsync(new CreateClaimRequest
        {
            InsuranceCompanyId = TestData.CompanyId,
            PatientName = "Test Patient",
            PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PolicyNumber = "POL-005",
            ClaimType = "General",
            ClaimAmount = 200,
            IncidentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "General checkup"
        });

        await service.PickupClaimAsMakerAsync(claim.Id, TestData.MakerId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SubmitMakerReviewAsync(claim.Id, maker2Id,
                new MakerReviewRequest { Recommendation = "Approve" }));
    }

    // ── TEST 6: Future incident date should be rejected ───────────────
    [Fact]
    public async Task CreateClaim_WithFutureIncidentDate_ShouldThrow()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = new ClaimService(db, NullLogger<ClaimService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateClaimAsync(new CreateClaimRequest
            {
                InsuranceCompanyId = TestData.CompanyId,
                PatientName = "Future Patient",
                PatientDob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PolicyNumber = "POL-006",
                ClaimType = "General",
                ClaimAmount = 100,
                IncidentDate = DateTime.UtcNow.AddDays(10),
                Description = "Future incident"
            }));
    }
}

public static class TestData
{
    public static readonly Guid CompanyId = Guid.NewGuid();
    public static readonly Guid MakerId = Guid.NewGuid();
    public static readonly Guid CheckerId = Guid.NewGuid();
}