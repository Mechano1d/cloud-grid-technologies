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
├── .github/                  # CI/CD workflows and PR template
├── backend/                  # R1: .NET 10 backend ecosystem
│   ├── OnlineBanking.slnx    # Solution file
│   ├── Directory.Build.props # Shared compiler & analyzer settings
│   ├── Directory.Packages.props # Centralized NuGet versions
│   ├── src/
│   │   ├── Shared/           # Pure shared contracts, Result, event DTOs (no ASP.NET)
│   │   ├── Monolith/         # Lab 1–3 monolith API (Modules: Clients, Accounts, Transactions)
│   │   └── Services/         # Lab 2+ workers & Lab 3+ microservices
│   └── tests/                # Backend unit & integration tests
├── frontend/                 # R2: Web UI client
├── load-tests/               # R2: k6 / Locust benchmark scripts & results
├── gateway/                  # R4: nginx / API Gateway configuration (Lab 2+)
├── infra/                    # R3 + R4: docker, k8s, terraform, monitoring
├── docs/
│   ├── openapi/              # Shared API contracts (start here for parallel work)
│   ├── architecture/         # Diagrams & ADRs
│   ├── reports/              # Team lab reports
│   └── ai-logs/              # Transparent AI usage logs per role
└── .env.example              # Environment variable contract
```

## Git Workflow

- `main` — stable milestone releases (`lab1`, `lab2`, `lab3`, `lab4`, `project`).
- `development` — team integration branch.
- Work in short-lived feature branches (`feat/backend-*`, `feat/frontend-*`, `infra/*`, `docs/*`) off `development` and merge via Pull Request using **merge commits** (no squash) so each contributor's history is preserved.

## Quick Start (Backend — .NET 10)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/) and PostgreSQL (for full persistence).

```bash
cp .env.example .env
dotnet restore backend/OnlineBanking.slnx
dotnet build backend/OnlineBanking.slnx
dotnet test backend/OnlineBanking.slnx
```

- Shared Lab 1 API contract: `docs/openapi/lab1-monolith.yaml`
