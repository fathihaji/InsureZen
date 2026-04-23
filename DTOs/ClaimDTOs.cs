namespace InsureZen.DTOs;

// What the upstream service sends when submitting a new claim
public class CreateClaimRequest
{
    public Guid InsuranceCompanyId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime PatientDob { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

// What Maker sends when submitting their recommendation
public class MakerReviewRequest
{
    public string Recommendation { get; set; } = string.Empty; // "Approve" or "Reject"
    public string? Notes { get; set; }
}

// What Checker sends when issuing final decision
public class CheckerDecisionRequest
{
    public string Decision { get; set; } = string.Empty; // "Approve" or "Reject"
    public string? Notes { get; set; }
}

// Query parameters for the history/list endpoint
public class ClaimQueryParams
{
    public string? Status { get; set; }
    public Guid? InsuranceCompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// What we send back for a single claim
public class ClaimResponse
{
    public Guid Id { get; set; }
    public string InsuranceCompanyName { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime PatientDob { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MakerName { get; set; }
    public string? MakerRecommendation { get; set; }
    public string? MakerNotes { get; set; }
    public DateTime? MakerReviewedAt { get; set; }
    public string? CheckerName { get; set; }
    public string? CheckerDecision { get; set; }
    public string? CheckerNotes { get; set; }
    public DateTime? CheckerReviewedAt { get; set; }
    public DateTime? ForwardedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Paginated list response for the history endpoint
public class PagedResponse<T>
{
    public List<T> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}