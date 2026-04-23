# InsureZen – Insurance Claim Processing API

A backend REST API built with ASP.NET Core 8 and PostgreSQL that handles the internal
claim review workflow for InsureZen — a medical insurance processing company.

---

## What This Does

Insurance companies submit claim forms in different formats. InsureZen normalises
them and puts each claim through a two-stage internal review before sending a decision
back to the insurer.

The two stages are:
- **Maker** – an InsureZen employee who reviews the claim and gives a recommendation
- **Checker** – a second employee who reviews the Maker's work and makes the final call

Once the Checker decides, the claim is automatically forwarded to the insurance company.

---

## Tech Stack

- ASP.NET Core 8 (Web API)
- PostgreSQL 16
- Entity Framework Core 8
- xUnit (tests)
- Swagger UI (API docs)

---

## Project Structure
InsureZen/
├── Controllers/          # HTTP endpoints
├── Services/             # Business logic
├── Models/               # Database entities
├── DTOs/                 # Request and response shapes
├── Data/                 # EF Core DbContext and migrations
├── Migrations/           # Auto-generated EF migrations
InsureZen.Tests/
└── ClaimServiceTests.cs  # Unit tests for claim state transitions
---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [PostgreSQL 16](https://www.postgresql.org/download/)

### 1. Clone the repo

```bash
git clone <your-repo-url>
cd InsureZen
```

### 2. Set up the database

Make sure PostgreSQL is running, then update the connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=InsureZenDb;Username=postgres;Password=your_password"
}
```

### 3. Run the API

```bash
dotnet run
```

The app will automatically create the database and apply all migrations on startup.
It will also seed two insurance companies, two Makers, and two Checkers.

### 4. Open Swagger

http://localhost:5202/swagger

### 5. Run the tests

```bash
cd ../InsureZen.Tests
dotnet test
```

---

## Claim Lifecycle

Every claim moves through these states in order — never backwards:

SUBMITTED → UNDER_MAKER_REVIEW → PENDING_CHECKER_REVIEW → APPROVED/REJECTED → FORWARDED
---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/claims | Submit a new claim |
| GET | /api/claims | Get paginated claim history |
| GET | /api/claims/{id} | Get a single claim |
| POST | /api/claims/{id}/pickup-maker | Maker locks a claim for review |
| POST | /api/claims/{id}/maker-review | Maker submits recommendation |
| POST | /api/claims/{id}/pickup-checker | Checker locks a claim for review |
| POST | /api/claims/{id}/checker-decision | Checker issues final decision |

All Maker and Checker endpoints require an `X-User-Id` header (a valid user UUID).

---

## Seeded Test Data

The database is pre-loaded with the following accounts for testing:

| Name | Role | ID |
|------|------|----|
| Alice Maker | Maker | `11111111-1111-1111-1111-111111111111` |
| Bob Maker | Maker | `22222222-2222-2222-2222-222222222222` |
| Carol Checker | Checker | `33333333-3333-3333-3333-333333333333` |
| David Checker | Checker | `44444444-4444-4444-4444-444444444444` |

| Company | ID |
|---------|----|
| AlphaShield Insurance | `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` |
| BetaCare Health | `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` |

---

## Concurrency

Multiple Makers can be working at the same time. To prevent two Makers from
grabbing the same claim simultaneously, I used PostgreSQL row-level locking
(`SELECT FOR UPDATE`) inside a transaction. The second Maker gets a 409 Conflict.

The in-memory database used in tests doesn't support this, so I added an
`IsRelational()` check to skip the lock during testing.

---

## Assumptions

- Authentication is out of scope. User identity is passed via `X-User-Id` header.
- Users and insurance companies are pre-seeded. No registration endpoints needed.
- A Maker and Checker cannot be the same person on the same claim.
- Forwarding to the insurer is a stub — the action is logged but no real HTTP call is made.
- Claim amounts are stored in a single currency with no conversion.
- Incident date cannot be in the future.
- Page size is capped at 100 to prevent large queries.

---

## Requirements Analysis

See [REQUIREMENTS.md](./REQUIREMENTS.md) for the full breakdown of entities,
actors, functional requirements, non-functional requirements, and edge cases.

