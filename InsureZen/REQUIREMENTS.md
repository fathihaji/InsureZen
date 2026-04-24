| Status | Meaning |
|--------|---------|
| SUBMITTED | Claim ingested, awaiting Maker |
| UNDER_MAKER_REVIEW | Maker picked up and is reviewing |
| PENDING_CHECKER_REVIEW | Maker submitted recommendation, awaiting Checker |
| APPROVED | Checker approved, awaiting forwarding |
| REJECTED | Checker rejected, awaiting forwarding |
| FORWARDED | Sent to insurance company. Terminal state |

---

## 5. Functional Requirements

- FR-01: Accept structured claim data via POST endpoint
- FR-02: New claims must have SUBMITTED status
- FR-03: Validate all required fields on ingestion
- FR-04: Maker can retrieve list of SUBMITTED claims
- FR-05: Maker can pick up a claim — status becomes UNDER_MAKER_REVIEW
- FR-06: Only the assigned Maker can submit a recommendation
- FR-07: After Maker recommendation — status becomes PENDING_CHECKER_REVIEW
- FR-08: Checker can retrieve PENDING_CHECKER_REVIEW claims
- FR-09: Checker can issue final decision — status becomes APPROVED or REJECTED
- FR-10: After Checker decision — claim is automatically forwarded
- FR-11: Claim history endpoint must be paginated and filterable

---

## 6. Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| NFR-01 | Concurrency: Only one Maker can pick up a claim at a time |
| NFR-02 | Auditability: All state transitions must be timestamped |
| NFR-03 | Data Integrity: Invalid state transitions must be rejected |
| NFR-04 | Scalability: Queries must be paginated and indexed |
| NFR-05 | Consistency: API responses must follow a consistent format |

---

## 7. Edge Cases

| # | Edge Case | Handling |
|---|-----------|----------|
| EC-01 | Two Makers pick up same claim simultaneously | SELECT FOR UPDATE lock — second gets 409 Conflict |
| EC-02 | Maker tries to review claim they didn't pick up | 403 Forbidden |
| EC-03 | Checker tries to review claim not ready | 409 Conflict |
| EC-04 | Incident date in the future | 400 Bad Request |
| EC-05 | Checker is same person as Maker | 409 Conflict |

---

## 8. Assumptions

- Authentication is out of scope. User identity passed via X-User-Id header
- Users and insurance companies are pre-seeded
- Forwarding is a stub — logged but no real HTTP call
- Claim amounts stored in single currency
- Page size capped at 100