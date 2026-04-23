namespace InsureZen.Models;

public class Claim
{
    public Guid Id { get; set; }

    // Which insurance company this claim belongs to
    public Guid InsuranceCompanyId { get; set; }
    public InsuranceCompany InsuranceCompany { get; set; } = null!;

    // Patient info (sent by upstream service)
    public string PatientName { get; set; } = string.Empty;
    public DateTime PatientDob { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;

    // Current stage of the claim
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    // Maker section - nullable because not filled yet when claim arrives
    public Guid? MakerId { get; set; }
    public User? Maker { get; set; }
    public ClaimDecision? MakerRecommendation { get; set; }
    public string? MakerNotes { get; set; }
    public DateTime? MakerReviewedAt { get; set; }

    // Checker section - nullable for same reason
    public Guid? CheckerId { get; set; }
    public User? Checker { get; set; }
    public ClaimDecision? CheckerDecision { get; set; }
    public string? CheckerNotes { get; set; }
    public DateTime? CheckerReviewedAt { get; set; }

    // Forwarding
    public DateTime? ForwardedAt { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency token - PostgreSQL uses this to detect simultaneous updates
    public uint RowVersion { get; set; }
}