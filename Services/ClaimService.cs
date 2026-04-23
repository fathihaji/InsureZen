using InsureZen.Data;
using InsureZen.DTOs;
using InsureZen.Models;
using Microsoft.EntityFrameworkCore;

namespace InsureZen.Services;

public class ClaimService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ClaimService> _logger;

    public ClaimService(AppDbContext db, ILogger<ClaimService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── INGEST A NEW CLAIM ───────────────────────────────────────────────
    public async Task<ClaimResponse> CreateClaimAsync(CreateClaimRequest request)
    {
        var company = await _db.InsuranceCompanies.FindAsync(request.InsuranceCompanyId);
        if (company == null)
            throw new KeyNotFoundException("Insurance company not found.");

        if (request.IncidentDate.Date > DateTime.UtcNow.Date)
            throw new ArgumentException("Incident date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(request.PatientName))
            throw new ArgumentException("Patient name is required.");
        if (string.IsNullOrWhiteSpace(request.PolicyNumber))
            throw new ArgumentException("Policy number is required.");
        if (request.ClaimAmount <= 0)
            throw new ArgumentException("Claim amount must be greater than zero.");

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            InsuranceCompanyId = request.InsuranceCompanyId,
            PatientName = request.PatientName.Trim(),
            PatientDob = request.PatientDob,
            PolicyNumber = request.PolicyNumber.Trim(),
            ClaimType = request.ClaimType.Trim(),
            ClaimAmount = request.ClaimAmount,
            IncidentDate = request.IncidentDate,
            Description = request.Description.Trim(),
            Status = ClaimStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        _logger.LogInformation("New claim {ClaimId} ingested for company {CompanyId}",
            claim.Id, claim.InsuranceCompanyId);

        return await GetClaimByIdAsync(claim.Id);
    }

    // ─── GET SINGLE CLAIM ─────────────────────────────────────────────────
    public async Task<ClaimResponse> GetClaimByIdAsync(Guid id)
    {
        var claim = await _db.Claims
            .Include(c => c.InsuranceCompany)
            .Include(c => c.Maker)
            .Include(c => c.Checker)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (claim == null)
            throw new KeyNotFoundException($"Claim {id} not found.");

        return MapToResponse(claim);
    }

    // ─── GET PAGINATED CLAIM HISTORY ──────────────────────────────────────
    public async Task<PagedResponse<ClaimResponse>> GetClaimsAsync(ClaimQueryParams query)
    {
        if (query.PageSize > 100) query.PageSize = 100;
        if (query.Page < 1) query.Page = 1;

        var q = _db.Claims
            .Include(c => c.InsuranceCompany)
            .Include(c => c.Maker)
            .Include(c => c.Checker)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<ClaimStatus>(query.Status, true, out var status))
            q = q.Where(c => c.Status == status);

        if (query.InsuranceCompanyId.HasValue)
            q = q.Where(c => c.InsuranceCompanyId == query.InsuranceCompanyId.Value);

        if (query.FromDate.HasValue)
            q = q.Where(c => c.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(c => c.CreatedAt <= query.ToDate.Value);

        var totalCount = await q.CountAsync();

        var claims = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResponse<ClaimResponse>
        {
            Data = claims.Select(MapToResponse).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    // ─── MAKER: PICK UP A CLAIM ───────────────────────────────────────────
    public async Task<ClaimResponse> PickupClaimAsMakerAsync(Guid claimId, Guid makerId)
    {
        var maker = await _db.Users.FindAsync(makerId);
        if (maker == null)
            throw new KeyNotFoundException("User not found.");
        if (maker.Role != UserRole.Maker)
            throw new UnauthorizedAccessException("Only Makers can pick up claims for review.");

        // Only use transactions when using a real relational database
        if (_db.Database.IsRelational())
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var claim = await _db.Claims
                    .FirstOrDefaultAsync(c => c.Id == claimId);

                if (claim != null)
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"Claims\" WHERE \"Id\" = {0} FOR UPDATE", claimId);

                if (claim == null)
                    throw new KeyNotFoundException("Claim not found.");

                if (claim.Status != ClaimStatus.Submitted)
                    throw new InvalidOperationException(
                        $"Claim cannot be picked up. Current status: {claim.Status}");

                claim.MakerId = makerId;
                claim.Status = ClaimStatus.UnderMakerReview;
                claim.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Claim {ClaimId} picked up by Maker {MakerId}",
                    claimId, makerId);

                return await GetClaimByIdAsync(claimId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            // In-memory path for tests
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
                throw new KeyNotFoundException("Claim not found.");

            if (claim.Status != ClaimStatus.Submitted)
                throw new InvalidOperationException(
                    $"Claim cannot be picked up. Current status: {claim.Status}");

            claim.MakerId = makerId;
            claim.Status = ClaimStatus.UnderMakerReview;
            claim.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Claim {ClaimId} picked up by Maker {MakerId}",
                claimId, makerId);

            return await GetClaimByIdAsync(claimId);
        }
    }

    // ─── MAKER: SUBMIT RECOMMENDATION ────────────────────────────────────
    public async Task<ClaimResponse> SubmitMakerReviewAsync(
        Guid claimId, Guid makerId, MakerReviewRequest request)
    {
        if (!Enum.TryParse<ClaimDecision>(request.Recommendation, true, out var recommendation))
            throw new ArgumentException("Recommendation must be 'Approve' or 'Reject'.");

        var claim = await _db.Claims.FindAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException("Claim not found.");

        if (claim.MakerId != makerId)
            throw new UnauthorizedAccessException(
                "You are not the Maker assigned to this claim.");

        if (claim.Status != ClaimStatus.UnderMakerReview)
            throw new InvalidOperationException(
                $"Claim is not under maker review. Current status: {claim.Status}");

        claim.MakerRecommendation = recommendation;
        claim.MakerNotes = request.Notes?.Trim();
        claim.MakerReviewedAt = DateTime.UtcNow;
        claim.Status = ClaimStatus.PendingCheckerReview;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Claim {ClaimId} reviewed by Maker {MakerId}. Recommendation: {Rec}",
            claimId, makerId, recommendation);

        return await GetClaimByIdAsync(claimId);
    }

    // ─── CHECKER: PICK UP A CLAIM ─────────────────────────────────────────
    public async Task<ClaimResponse> PickupClaimAsCheckerAsync(Guid claimId, Guid checkerId)
    {
        var checker = await _db.Users.FindAsync(checkerId);
        if (checker == null)
            throw new KeyNotFoundException("User not found.");
        if (checker.Role != UserRole.Checker)
            throw new UnauthorizedAccessException("Only Checkers can pick up claims for final review.");

        if (_db.Database.IsRelational())
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var claim = await _db.Claims
                    .FirstOrDefaultAsync(c => c.Id == claimId);

                if (claim != null)
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"Claims\" WHERE \"Id\" = {0} FOR UPDATE", claimId);

                if (claim == null)
                    throw new KeyNotFoundException("Claim not found.");

                if (claim.Status != ClaimStatus.PendingCheckerReview)
                    throw new InvalidOperationException(
                        $"Claim is not ready for checker review. Current status: {claim.Status}");

                if (claim.MakerId == checkerId)
                    throw new InvalidOperationException(
                        "The Checker cannot be the same person as the Maker for this claim.");

                claim.CheckerId = checkerId;
                claim.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Claim {ClaimId} picked up by Checker {CheckerId}",
                    claimId, checkerId);

                return await GetClaimByIdAsync(claimId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
                throw new KeyNotFoundException("Claim not found.");

            if (claim.Status != ClaimStatus.PendingCheckerReview)
                throw new InvalidOperationException(
                    $"Claim is not ready for checker review. Current status: {claim.Status}");

            if (claim.MakerId == checkerId)
                throw new InvalidOperationException(
                    "The Checker cannot be the same person as the Maker for this claim.");

            claim.CheckerId = checkerId;
            claim.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Claim {ClaimId} picked up by Checker {CheckerId}",
                claimId, checkerId);

            return await GetClaimByIdAsync(claimId);
        }
    }

    // ─── CHECKER: ISSUE FINAL DECISION ───────────────────────────────────
    public async Task<ClaimResponse> SubmitCheckerDecisionAsync(
        Guid claimId, Guid checkerId, CheckerDecisionRequest request)
    {
        if (!Enum.TryParse<ClaimDecision>(request.Decision, true, out var decision))
            throw new ArgumentException("Decision must be 'Approve' or 'Reject'.");

        var claim = await _db.Claims.FindAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException("Claim not found.");

        if (claim.CheckerId != checkerId)
            throw new UnauthorizedAccessException(
                "You are not the Checker assigned to this claim.");

        if (claim.Status != ClaimStatus.PendingCheckerReview)
            throw new InvalidOperationException(
                $"Claim is not pending checker review. Current status: {claim.Status}");

        claim.CheckerDecision = decision;
        claim.CheckerNotes = request.Notes?.Trim();
        claim.CheckerReviewedAt = DateTime.UtcNow;
        claim.Status = decision == ClaimDecision.Approve
            ? ClaimStatus.Approved
            : ClaimStatus.Rejected;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Claim {ClaimId} final decision by Checker {CheckerId}: {Decision}",
            claimId, checkerId, decision);

        return await ForwardClaimAsync(claimId);
    }

    // ─── FORWARD CLAIM TO INSURER (STUB) ─────────────────────────────────
    public async Task<ClaimResponse> ForwardClaimAsync(Guid claimId)
    {
        var claim = await _db.Claims
            .Include(c => c.InsuranceCompany)
            .FirstOrDefaultAsync(c => c.Id == claimId);

        if (claim == null)
            throw new KeyNotFoundException("Claim not found.");

        if (claim.Status != ClaimStatus.Approved && claim.Status != ClaimStatus.Rejected)
            throw new InvalidOperationException(
                "Only approved or rejected claims can be forwarded.");

        claim.Status = ClaimStatus.Forwarded;
        claim.ForwardedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[FORWARD STUB] Claim {ClaimId} forwarded to {Company} at {Email}. Decision: {Decision}",
            claimId,
            claim.InsuranceCompany.Name,
            claim.InsuranceCompany.ContactEmail,
            claim.CheckerDecision);

        return await GetClaimByIdAsync(claimId);
    }

    // ─── HELPER: MAP CLAIM TO RESPONSE DTO ───────────────────────────────
    private static ClaimResponse MapToResponse(Claim claim) => new()
    {
        Id = claim.Id,
        InsuranceCompanyName = claim.InsuranceCompany?.Name ?? string.Empty,
        PatientName = claim.PatientName,
        PatientDob = claim.PatientDob,
        PolicyNumber = claim.PolicyNumber,
        ClaimType = claim.ClaimType,
        ClaimAmount = claim.ClaimAmount,
        IncidentDate = claim.IncidentDate,
        Description = claim.Description,
        Status = claim.Status.ToString(),
        MakerName = claim.Maker?.Name,
        MakerRecommendation = claim.MakerRecommendation?.ToString(),
        MakerNotes = claim.MakerNotes,
        MakerReviewedAt = claim.MakerReviewedAt,
        CheckerName = claim.Checker?.Name,
        CheckerDecision = claim.CheckerDecision?.ToString(),
        CheckerNotes = claim.CheckerNotes,
        CheckerReviewedAt = claim.CheckerReviewedAt,
        ForwardedAt = claim.ForwardedAt,
        CreatedAt = claim.CreatedAt,
        UpdatedAt = claim.UpdatedAt
    };
}