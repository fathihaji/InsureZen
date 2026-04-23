namespace InsureZen.Models;

// The stages a claim goes through
public enum ClaimStatus
{
    Submitted,
    UnderMakerReview,
    PendingCheckerReview,
    Approved,
    Rejected,
    Forwarded
}

// Maker's recommendation OR Checker's final decision
public enum ClaimDecision
{
    Approve,
    Reject
}

// Who is this user?
public enum UserRole
{
    Maker,
    Checker
}