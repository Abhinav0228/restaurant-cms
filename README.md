# Restaurant CMS

A multi-tenant restaurant and cafe platform: each restaurant gets a staff portal, live table management, QR ordering at the table, a kitchen display, reservations, offers and its own guest website. A platform console onboards restaurants and manages their plans.

- **Frontend:** Angular 22 (standalone components, signals, zoneless), Vitest, Playwright
- **Backend:** .NET 10 (LTS), ASP.NET Core Minimal APIs, Clean Architecture
- **Data:** PostgreSQL 16 with EF Core 10 (Npgsql)
- **Background jobs:** Hangfire, stored in PostgreSQL
- **Realtime:** SignalR

How the code is organised, and why, is in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). The original plan and domain model are in [`docs/PLAN.md`](docs/PLAN.md).

## Quick start

### Option A: frontend only, no backend (mock API)

```bash
cd frontend
npm install
npm run start:mock        # http://localhost:4200
```

Sign in from `/login` with one of the demo buttons (Admin, Waiter, Kitchen or Platform; password `ChangeMe!123`).

The mock hosts two restaurants: **Ember & Oak** (`/r/ember-oak`) and **Blue Door Café** (`/r/blue-door`, owner `owner@bluedoor.local`). To see table ordering live, open `/r/ember-oak/t/T2` in one tab and `/admin/floor` in another. The mock keeps its data in localStorage and syncs between tabs. Use **Reset demo** in the sidebar to restore the starting data.

### Option B: full stack with Docker

```bash
docker compose up --build
```

| What | URL |
|---|---|
| Web app | http://localhost:8080 |
| API docs (Scalar) | http://localhost:5080/scalar/v1 |
| Hangfire dashboard | http://localhost:5080/hangfire?access_token=… |
| Health | http://localhost:5080/health |

The Hangfire dashboard opens without sign-in only for loopback requests in Development (`dotnet run`). Behind Docker, sign in as `platform@restaurant.local`, then open `/hangfire?access_token=<token>` once; the token is kept in a cookie after that.

### Option C: run each part yourself

```bash
# 1. PostgreSQL (any local install, or just the db service)
docker compose up db -d

# 2. API on http://localhost:5080
cd backend
dotnet restore
dotnet run --project src/RestaurantCms.Api

# 3. Web on http://localhost:4200 (calls the API at :5080)
cd frontend
npm install
npm start
```

In Development (or with `Seed:Demo=true`) the API seeds a platform admin and a demo restaurant, **Ember & Oak** (`/r/ember-oak`), created through the same onboarding use case a real signup uses:

| Email | Role |
|---|---|
| `platform@restaurant.local` | Platform admin (opens `/platform`) |
| `admin@restaurant.local` | Ember & Oak admin |
| `waiter@restaurant.local` | Ember & Oak waiter |
| `kitchen@restaurant.local` | Ember & Oak kitchen |

All of them use the password from `Seed:Password` (`ChangeMe!123` by default). Change it before going live.

## Migrations

The API calls `EnsureCreated` until the first migration exists, so it runs straight away. On Windows, `setup.cmd` creates the first migration for you. To do it by hand:

```bash
cd backend
dotnet tool restore
dotnet ef migrations add Initial -p src/RestaurantCms.Infrastructure -s src/RestaurantCms.Api -o Persistence/Migrations
```

Once a migration exists, startup runs `Migrate()` instead.

## Tests

```bash
# Backend: domain, use cases, architecture rules, tenant isolation (xUnit v3)
cd backend && dotnet test        # the isolation tests need Docker running; they are skipped without it

# Frontend unit tests (Vitest)
cd frontend && npm run test:ci

# End-to-end tests (Playwright, desktop + mobile, against the mock API)
cd frontend
npx playwright install chromium   # once
npm run e2e
```

| Suite | Tests | Covers |
|---|---|---|
| `RestaurantCms.Domain.Tests` | 61 | serving windows, modifiers, order totals and status flow, delay detection, table lifecycle, reservations, offers, time zones, tenant slugs, plans, tenant invariants, domain events |
| `RestaurantCms.Application.Tests` | 12 | use cases through the real pipeline: validation, QR ordering, realtime events, paging, settling, plan limits, onboarding |
| `RestaurantCms.Api.Tests` | 6 + 3 | layer rules, handler registration, tenant filter on every table; HTTP tenant isolation against PostgreSQL (Docker) |
| Frontend unit (Vitest) | 33 | cart, formatting, ApiError, TenantStore, slugs, mock API rules including tenancy |
| Frontend e2e (Playwright) | 13 × 2 viewports | QR order → floor plan → kitchen → bill; sold-out items; required options; takeaway; menu editing; roles; bookings; onboarding a restaurant; per-restaurant guest sites; old QR links |

CI runs all of these on every push (`.github/workflows/ci.yml`).

## Project layout

```
backend/
  src/RestaurantCms.Domain          aggregates and business rules (no dependencies)
  src/RestaurantCms.Application     Features/<feature>/ — one class per use case (request, validator, handler)
  src/RestaurantCms.Infrastructure  EF Core + PostgreSQL (one config and repository per aggregate), Hangfire, SignalR, JWT
  src/RestaurantCms.Api             Features/<feature>/ endpoint modules, tenant resolution, policies, composition
  tests/                            domain, application, architecture + integration
frontend/src/app/
  core/               config, HTTP client, auth, tenant, realtime, theme, mock API
  shared/             models (one file per domain), UI components, cart, pipes
  layout/             staff portal shell
  features/<feature>/ data-access/ (API + store), pages/, ui/, <feature>.routes.ts
frontend/e2e/         Playwright tests
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the rules behind this layout.

## How table ordering works

1. Each table has a code (e.g. `T2`) and a QR code that points to the restaurant's own address, e.g. `/r/ember-oak/t/T2`. Print the cards from **Settings → Tables & QR codes**.
2. A guest's first order, or a waiter seating the table, opens a **table session**. Every later order at that table joins the same session.
3. The floor plan shows each table's guests, time seated and running bill. Tables with orders that need attention (new, ready to serve, or running late) are ringed with a count.
4. Opening a table shows every order placed during the visit, with buttons to move each one on, and the running bill.
5. **Settle bill** is allowed once every order is served or cancelled. It completes the orders, closes the session and marks the table **Needs clearing**. **Table cleared** sets it back to free.

## Background jobs (Hangfire)

| Job | When | What it does |
|---|---|---|
| Reservation reminder | 2 h before a confirmed booking | Sends the reminder (logged in dev; plug in SMS/email via `INotificationSender`) and holds the table |
| No-show check | Booking time + 20 min | Marks a no-show and frees the held table |
| Delayed orders | Every minute | Flags orders past prep time + buffer and alerts staff screens |
| Offer scheduler | Every 5 min | Turns offers on and off at their start and end |
| Daily sales snapshot | Hourly | Refreshes today's KPIs for the dashboard's 7-day chart |
| Stale session cleanup | Hourly | Closes sessions opened over 6 h ago with no orders |

Recurring jobs run once per active restaurant (`Infrastructure/Jobs/TenantJobRunner.cs`). The job logic itself lives in `Application/Features/BackgroundJobs/RestaurantJobs.cs` and knows nothing about Hangfire or tenants.

## Roles

| Role | Can use |
|---|---|
| Platform admin | The platform console only: add restaurants, change plans, suspend and reactivate |
| Admin | Everything in their restaurant, including settings and staff |
| Manager | Everything except restaurant settings and staff |
| Waiter | Today, floor, orders, kitchen, bookings; can mark items sold out |
| Kitchen | Kitchen display and orders |

## Design

The colours follow one rule from the brief: **two neutrals that get along, and a single accent kept for whatever needs clicking or attention.** The restaurant picks one of three palettes in **Settings → Brand colours**. The choice is saved on the server, so guest pages use it too, and each palette has a light and a dark mode.

| Palette | Neutrals | Accent |
|---|---|---|
| Navy (default) | Navy `#0F1B3D` + White `#FFFFFF` | Hot pink `#FF3D8B` (navy text on it) |
| Charcoal | Charcoal `#1E1E1C` + Sand `#D8CBB3` | Electric blue `#2F6BFF` |
| Cream | Cream `#F3EEE3` + Ink `#111111` | Burnt orange `#D9541E` |

Table and order states use fill and pattern (outline, solid, dashed, hatched), so the accent stays meaningful. All text on the accent meets WCAG AA contrast. The typeface is Bricolage Grotesque throughout.

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:Postgres` | Database connection |
| `Jwt:Key` | Token signing key (32+ characters; use user-secrets or an environment variable) |
| `Cors:Origins` | Allowed web origins when the frontend is served separately |
| `Hangfire:RunServer` | Set `false` on API instances that should not process jobs |
| `Seed:Demo` | Seed the platform admin and demo restaurant outside Development |
| `Seed:Password` | Password for the seeded demo accounts |

## Upgrading to .NET 11

.NET 11 (STS) is scheduled for release on 10 November 2026. To move to it once it ships, change `TargetFramework` in `backend/Directory.Build.props` to `net11.0` and update the `10.0.*` package versions to `11.0.*`.
