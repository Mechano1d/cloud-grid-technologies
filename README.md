# Online Banking System — Cloud & GRID Technologies

Unified team monorepo for the **Cloud and GRID Technologies** course (KPI, Group ІТ-51мн).
The project evolves a single **Online Banking System** across all 4 laboratory works and the final course project.

## Domain & Milestones

Core domain: **Clients + Accounts + Transfers**, extended with **Notifications**, **Credits**, **Credit Scoring**, and **Audit**.

| Tag | Milestone | Architecture Scope |
|---|---|---|
| `lab1` | Lab 1 — Cloud Application | `.NET 10` Monolith (Clients, Accounts, Transfers, JWT, PostgreSQL) + Web UI + Cloud deploy |
| `lab2` | Lab 2 — Containerized App | Multi-container Docker Compose (`gateway`, `app`, `postgres`, `rabbitmq`, `notification-worker`, `frontend`) |
| `lab3` | Lab 3 — Monolith vs Microservices | Microservices (`client`, `account`, `transaction`, `notification` services) benchmarked against Monolith |
| `lab4` | Lab 4 — Cloud Resource Management | Kubernetes + HPA, Prometheus + Grafana, GitHub Actions CI/CD, Terraform |
| `project` | Course Project | Full system + `credit-service`, `scoring-service`, `audit-service`, canary release & presentation |

## Team Roles

| Role | Scope | Key Directories |
|---|---|---|
| **R1 — Backend / Domain** | `.NET 10` API, domain logic, JWT auth, PostgreSQL migrations, monolith -> microservices | `backend/` |
| **R2 — Frontend / QA / Load** | Web client, API integration tests, k6/Locust benchmarks | `frontend/`, `load-tests/` |
| **R3 — DevOps / Cloud** | Cloud DB & hosting, Dockerfiles, Compose, K8s/HPA, Terraform, CI/CD | `infra/`, `.github/workflows/` |
| **R4 — Integration / Observability / PM** | RabbitMQ, API Gateway, Prometheus/Grafana, OpenAPI, reports & presentation | `gateway/`, `infra/monitoring/`, `docs/` |

## Repository Structure

```text
.
├── .github/                              # CI/CD workflows and PR template
├── backend/                              # R1: .NET 10 backend ecosystem
│   ├── OnlineBanking.slnx                # Solution file
│   ├── Directory.Build.props             # Shared compiler & analyzer settings
│   ├── Directory.Packages.props          # Centralized NuGet versions
│   ├── src/
│   │   ├── Shared/OnlineBanking.Shared/  # Pure cross-module contracts, Result, Time
│   │   ├── Monolith/                     # Lab 1–3 Modular Monolith
│   │   │   ├── OnlineBanking.Api/
│   │   │   ├── OnlineBanking.Infrastructure/
│   │   │   ├── OnlineBanking.Persistence/
│   │   │   ├── OnlineBanking.Module.Clients/
│   │   │   ├── OnlineBanking.Module.Accounts/
│   │   │   └── OnlineBanking.Module.Transactions/
│   │   └── Services/                     # Lab 2+ workers & Lab 3+ microservices
│   └── tests/
│       ├── OnlineBanking.UnitTests/      # Domain + NetArchTest boundary tests
│       └── OnlineBanking.IntegrationTests/ # WebApplicationFactory + Testcontainers PostgreSQL
├── frontend/                             # R2: Web UI client
├── load-tests/                           # R2: k6 / Locust benchmark scripts & results
├── gateway/                              # R4: nginx / API Gateway configuration (Lab 2+)
├── infra/                                # R3 + R4: docker, k8s, terraform, monitoring
├── docs/
│   ├── openapi/                          # Shared API contracts (start here for parallel work)
│   ├── architecture/                     # Diagrams & ADRs (ADR-001)
│   ├── database/                         # Idempotent PostgreSQL migration SQL scripts
│   ├── reports/                          # Team lab reports
│   └── ai-logs/                          # Transparent AI usage logs per role
└── .env.example                          # Environment variable contract
```

## Quick Start (Backend — .NET 10 + PostgreSQL)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/) and Docker (for local PostgreSQL and Testcontainers integration tests).

1. Start PostgreSQL and configure environment variables:

```bash
cp .env.example .env
docker run -d --name banking-postgres \
  -e POSTGRES_DB=online_banking \
  -e POSTGRES_USER=banking_user \
  -e POSTGRES_PASSWORD=change_me_local_password \
  -p 5432:5432 postgres:17-alpine
```

2. Export database and JWT environment variables (matching `.env`), then run the API (Development applies EF Core migrations and idempotent demo seed automatically):

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=online_banking;Username=banking_user;Password=change_me_local_password"
export Jwt__SecretKey="replace_with_at_least_32_char_secret_key_for_local_dev"
dotnet run --project backend/src/Monolith/OnlineBanking.Api --environment Development --urls http://localhost:8080
```

3. Build and run all unit, architecture, and PostgreSQL integration tests:

```bash
dotnet build backend/OnlineBanking.slnx -c Release
dotnet test backend/OnlineBanking.slnx -c Release
```

- Interactive API docs (Scalar UI in `Development`): `http://localhost:8080/scalar/v1` (raw spec: `http://localhost:8080/openapi/v1.json`)
- Shared Lab 1 OpenAPI contract: `docs/openapi/lab1-monolith.yaml`
- Backend Architecture ADR: `docs/architecture/001-backend-architecture.md`
- Idempotent SQL DDL: `docs/database/initial-migrations.sql`
