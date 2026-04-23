using InsureZen.DTOs;
using InsureZen.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsureZen.Controllers;

[ApiController]
[Route("api/claims")]
public class ClaimsController : ControllerBase
{
    private readonly ClaimService _claimService;

    public ClaimsController(ClaimService claimService)
    {
        _claimService = claimService;
    }

    // POST /api/claims — Ingest a new claim
    [HttpPost]
    public async Task<IActionResult> CreateClaim([FromBody] CreateClaimRequest request)
    {
        try
        {
            var result = await _claimService.CreateClaimAsync(request);
            return CreatedAtAction(nameof(GetClaim), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/claims/{id} — Get single claim
    [HttpGet("{id}")]
    public async Task<IActionResult> GetClaim(Guid id)
    {
        try
        {
            var result = await _claimService.GetClaimByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // GET /api/claims — Paginated history with filters
    [HttpGet]
    public async Task<IActionResult> GetClaims([FromQuery] ClaimQueryParams query)
    {
        var result = await _claimService.GetClaimsAsync(query);
        return Ok(result);
    }

    // POST /api/claims/{id}/pickup-maker — Maker picks up a claim
    [HttpPost("{id}/pickup-maker")]
    public async Task<IActionResult> PickupAsMaker(Guid id)
    {
        // We get the user identity from header (assumption A-01 from requirements)
        var userId = GetUserIdFromHeader();
        if (userId == null)
            return BadRequest(new { error = "X-User-Id header is required." });

        try
        {
            var result = await _claimService.PickupClaimAsMakerAsync(id, userId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // POST /api/claims/{id}/maker-review — Maker submits recommendation
    [HttpPost("{id}/maker-review")]
    public async Task<IActionResult> MakerReview(Guid id, [FromBody] MakerReviewRequest request)
    {
        var userId = GetUserIdFromHeader();
        if (userId == null)
            return BadRequest(new { error = "X-User-Id header is required." });

        try
        {
            var result = await _claimService.SubmitMakerReviewAsync(id, userId.Value, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/claims/{id}/pickup-checker — Checker picks up a claim
    [HttpPost("{id}/pickup-checker")]
    public async Task<IActionResult> PickupAsChecker(Guid id)
    {
        var userId = GetUserIdFromHeader();
        if (userId == null)
            return BadRequest(new { error = "X-User-Id header is required." });

        try
        {
            var result = await _claimService.PickupClaimAsCheckerAsync(id, userId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // POST /api/claims/{id}/checker-decision — Checker issues final decision
    [HttpPost("{id}/checker-decision")]
    public async Task<IActionResult> CheckerDecision(Guid id, [FromBody] CheckerDecisionRequest request)
    {
        var userId = GetUserIdFromHeader();
        if (userId == null)
            return BadRequest(new { error = "X-User-Id header is required." });

        try
        {
            var result = await _claimService.SubmitCheckerDecisionAsync(id, userId.Value, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Helper — reads X-User-Id from request header
    private Guid? GetUserIdFromHeader()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var value) &&
            Guid.TryParse(value, out var userId))
            return userId;
        return null;
    }
}