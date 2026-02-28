# Coca-Cola Sustainability & Loyalty Platform — Build Plan

## Context for Handoff
If picking this up mid-build: this is a 24-hour hackathon project. Two separate web apps share one ASP.NET Core API and one SQLite database. Read this file top to bottom before touching any code. Check off completed items as you go and update the "Current State" section.

---

## Current State
- [x] **Block 1** — API scaffold + schema
- [x] **Block 2** — Mock data seed
- [x] **Block 3** — Auth endpoints
- [x] **Block 4** — Internal dashboard (overview, emissions, charts, map)
- [x] **Block 5** — Internal dashboard (RVM impact, AI insights page)
- [x] **Block 6** — Gemini AI integration
- [x] **Block 7** — Consumer app (register, login, home, history)
- [x] **Block 8** — Consumer app (scan, rewards, redemption)
- [x] **Block 9** — Polish, QR generation, end-to-end demo test

**Last completed block:** Block 9 — Polish, QR generation, end-to-end demo test
**Next action:** **None. Project Complete!**

## IMPORTANT FOR HANDOFF — Files written, still need to be verified end-to-end
- API runs on port 5009 (set in api/Properties/launchSettings.json)
- Admin login: admin@coke.com / Demo1234!
- Start API: cd UA_Innovate/api && dotnet run
- Open internal app: open internal-app/login.html in browser (use Live Server on port 5500)
- All API endpoints require Bearer token in Authorization header (admin role)
- database.db is auto-created and seeded on first dotnet run

---

## Rules (from CLAUDES_RULES.md — never violate these)
- Vanilla HTML/JS (ES6+), no React/Vue/Angular
- Bootstrap 5.3 from CDN only
- ASP.NET Core Web API, C#, .NET 8
- SQLite with raw SQL — no ORM
- Multi-page app: one `.html` file per tab/section
- Minimum code — no over-engineering
- 2-space indent HTML/CSS/JS, 4-space for C#

---

## Project Structure
```
UA_Innovate/
├── api/                          ← Shared ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── AreasController.cs
│   │   ├── EmissionsController.cs
│   │   ├── RvmController.cs
│   │   ├── UsersController.cs
│   │   ├── RewardsController.cs
│   │   └── InsightsController.cs
│   ├── Data/
│   │   ├── Database.cs           ← SQLite connection + schema init
│   │   └── Seeder.cs             ← Seeds all mock data on startup
│   ├── Services/
│   │   └── GeminiService.cs      ← Gemini free API wrapper
│   ├── appsettings.json
│   ├── Program.cs
│   └── database.db               ← Auto-created on first run
│
├── internal-app/                 ← Admin-only internal dashboard
│   ├── login.html
│   ├── index.html                ← KPI overview
│   ├── emissions.html            ← Emissions by area + A/B/C grades
│   ├── rvm-impact.html           ← RVM projection toggle
│   ├── ai-insights.html          ← Gemini recommendations
│   └── app.js                    ← Shared JS (auth check, fetch helpers)
│
└── consumer-app/                 ← Public consumer loyalty app
    ├── login.html
    ├── register.html
    ├── index.html                ← Points balance + quick actions
    ├── scan.html                 ← QR link handler (RVM demo)
    ├── rewards.html              ← Milestones + coupon redemption
    ├── history.html              ← Scan + points history
    └── app.js                    ← Shared JS (auth check, fetch helpers)
```

---

## Tech Stack
| Layer | Choice |
|-------|--------|
| Frontend | Vanilla HTML/JS, Bootstrap 5.3 CDN |
| Charts | Chart.js CDN |
| Map | Leaflet.js CDN |
| QR Generation | QRCode.js CDN (admin side) |
| Backend | ASP.NET Core Web API (.NET 8), C# |
| Database | SQLite (raw SQL, no ORM), file at `api/database.db` |
| AI | Google Gemini free API (`gemini-1.5-flash`) |
| Auth | JWT bearer tokens (both apps) |

---

## Database Schema

### `areas`
```sql
CREATE TABLE areas (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  type TEXT NOT NULL CHECK(type IN ('country','state','city')),
  parent_id INTEGER REFERENCES areas(id),
  latitude REAL,
  longitude REAL
);
```

### `emissions_data`
```sql
CREATE TABLE emissions_data (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  area_id INTEGER NOT NULL REFERENCES areas(id),
  category TEXT NOT NULL CHECK(category IN ('trucking','factory','energy','refrigeration','packaging')),
  amount_mtco2e REAL NOT NULL,
  year INTEGER NOT NULL,
  month INTEGER
);
```

### `carbon_initiatives`
```sql
CREATE TABLE carbon_initiatives (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  area_id INTEGER NOT NULL REFERENCES areas(id),
  initiative_type TEXT NOT NULL CHECK(initiative_type IN ('rvm','ev_fleet','renewable_energy')),
  value REAL NOT NULL,
  effective_date TEXT
);
```

### `emission_grades`
```sql
CREATE TABLE emission_grades (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  area_id INTEGER NOT NULL REFERENCES areas(id),
  raw_score REAL NOT NULL,
  initiative_deduction REAL NOT NULL,
  final_score REAL NOT NULL,
  grade TEXT NOT NULL CHECK(grade IN ('A','B','C')),
  computed_at TEXT NOT NULL
);
```

### `rvm_machines`
```sql
CREATE TABLE rvm_machines (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  area_id INTEGER NOT NULL REFERENCES areas(id),
  location_name TEXT NOT NULL,
  latitude REAL NOT NULL,
  longitude REAL NOT NULL,
  active INTEGER NOT NULL DEFAULT 1
);
```

### `rvm_scans`
```sql
CREATE TABLE rvm_scans (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  rvm_id INTEGER NOT NULL REFERENCES rvm_machines(id),
  user_id INTEGER REFERENCES users(id),
  product_barcode TEXT,
  scanned_at TEXT NOT NULL,
  points_awarded INTEGER NOT NULL DEFAULT 2
);
```

### `users`
```sql
CREATE TABLE users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  email TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL,
  total_points INTEGER NOT NULL DEFAULT 0
);
```

### `user_rewards`
```sql
CREATE TABLE user_rewards (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id),
  type TEXT NOT NULL CHECK(type IN ('earn','redeem')),
  points INTEGER NOT NULL,
  description TEXT,
  created_at TEXT NOT NULL
);
```

### `coupons`
```sql
CREATE TABLE coupons (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id),
  code TEXT NOT NULL UNIQUE,
  discount_description TEXT NOT NULL,
  issued_at TEXT NOT NULL,
  redeemed_at TEXT
);
```

### `admin_users`
```sql
CREATE TABLE admin_users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  email TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL
);
```

### `ai_insights`
```sql
CREATE TABLE ai_insights (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  area_id INTEGER NOT NULL REFERENCES areas(id),
  insight_text TEXT NOT NULL,
  suggestion_type TEXT,
  generated_at TEXT NOT NULL
);
```

---

## Grading Formula

```
Base Score = (
  trucking_mtco2e  * 0.35 +
  factory_mtco2e   * 0.30 +
  energy_mtco2e    * 0.25 +
  other_mtco2e     * 0.10
) -- summed for the area across all months in the target year

Initiative Deduction = (
  rvm_count         * 0.5  +
  ev_fleet_pct      * 2.0  +
  renewable_pct     * 2.0
)

Final Score = Base Score − Initiative Deduction

Grade (within peer group — cities vs cities, states vs states, countries vs countries):
  A = bottom third by final score (lowest emissions = best)
  B = middle third
  C = top third (highest emissions = worst)
```

Grades are recomputed at seed time and stored in `emission_grades`. Recompute endpoint: `POST /api/emissions/recompute-grades` (admin only).

---

## API Endpoints

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| POST | `/api/auth/admin/login` | None | Returns admin JWT |
| POST | `/api/auth/register` | None | Create consumer account |
| POST | `/api/auth/login` | None | Consumer login, returns JWT |
| GET | `/api/areas?type=country\|state\|city` | None | List areas |
| GET | `/api/emissions?areaId=&year=` | Admin | Emissions breakdown |
| GET | `/api/emissions/grades?type=` | Admin | All graded areas |
| POST | `/api/emissions/recompute-grades` | Admin | Rerun grading formula |
| GET | `/api/rvm?areaId=` | None | RVM machines |
| POST | `/api/rvm/scan` | User | Log bottle scan (+2 pts) |
| GET | `/api/rvm/impact?areaId=&rvmCount=` | Admin | Project emissions reduction |
| GET | `/api/users/me` | User | Profile + points |
| GET | `/api/users/history` | User | Scan + reward history |
| POST | `/api/users/redeem` | User | Spend 100 pts → coupon |
| GET | `/api/insights?areaId=` | Admin | Pre-generated AI text |
| POST | `/api/insights/query` | Admin | Live Gemini query |

---

## QR Code Demo Flow
1. Admin dashboard (`rvm-impact.html`) has a "Generate Demo QR" button per RVM machine
2. Clicking it generates a QR code (via QRCode.js) encoding the URL:
   `http://localhost:5501/consumer-app/scan.html?rvm_id=<ID>&barcode=COKE-355ML`
3. Demo participant scans QR with phone → consumer app opens in their browser
4. If not logged in, redirected to `login.html`, then back to `scan.html` with params preserved
5. `scan.html` reads URL params, calls `POST /api/rvm/scan`, awards 2 points, shows confirmation
6. Internal dashboard reflects new scan on next load

---

## Points & Rewards
- 1 bottle scanned = **2 points**
- **100 points** = 1 coupon (free Coke or discount at local retailer)
- Coupon code is auto-generated (UUID-based), stored in `coupons` table
- No expiry for hackathon demo

---

## Gemini AI Integration
- Model: `gemini-1.5-flash` (free tier)
- API key stored in `appsettings.json` under `"GeminiApiKey"`
- Called at seed time for: all 3 countries + 5 worst-graded US states
- Prompt template per area:
  > "You are a sustainability advisor for The Coca-Cola Company. Area: {name} ({type}). Grade: {grade}. Total emissions: {total} MTCO2e. Breakdown: trucking {t}%, factory {f}%, energy {e}%, other {o}%. Current RVMs deployed: {rvm_count}. EV fleet: {ev_pct}%. Renewable energy: {re_pct}%. Provide exactly 3 specific, actionable recommendations to reduce carbon footprint. At least one must address RVM expansion and one must address electric vehicle fleet adoption. Be concise and practical."
- Response stored in `ai_insights.insight_text`
- Live query endpoint (`POST /api/insights/query`) passes area context + user question to Gemini and streams response back

---

## Mock Data Scope
- **Countries:** US, Canada, Mexico
- **US States:** All 50
- **Canadian Provinces:** ON, QC, BC, AB, MB, SK, NS, NB, NL, PE (10)
- **Mexican States:** Jalisco, Nuevo León, CDMX, Puebla, Guanajuato, Veracruz (6)
- **Cities:** ~5 per country (~40 total), major metro areas
- **Emissions:** Based on Coca-Cola 2023 sustainability report (~3.5M MTCO2e globally, ~35% North America)
  - US: ~900,000 MTCO2e total, distributed by state (proportional to bottling plant density + population)
  - Canada: ~80,000 MTCO2e
  - Mexico: ~200,000 MTCO2e
- **RVM machines:** ~25 seeded across major cities
- **Carbon initiatives:** Vary by state to create realistic A/B/C spread

---

## CDN Links (copy-paste ready)
```html
<!-- Bootstrap 5.3 -->
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>

<!-- Chart.js -->
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>

<!-- Leaflet.js -->
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

<!-- QRCode.js (for QR generation) -->
<script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"></script>
```

---

## Build Blocks — Detailed Checklist

### Block 1 — API Scaffold + Schema (Hours 0–2) ✅ COMPLETE
- [x] Run `dotnet new webapi -n api` inside `UA_Innovate/`
- [x] Remove template WeatherForecast files
- [x] Add `Microsoft.Data.Sqlite` + `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet packages
- [x] Create `Data/Database.cs` — connection helper + `InitializeSchema()` that runs all CREATE TABLE IF NOT EXISTS statements
- [x] Create `Program.cs` — register services, call schema init and seeder on startup, configure CORS (allow all for dev), add JWT auth middleware
- [x] Create `appsettings.json` — connection string, JWT secret, Gemini API key placeholder
- [x] Verify API starts on http://localhost:5009 and database.db is created with all tables
- [x] Create stub `Data/Seeder.cs` (filled out in Block 2)
- [x] Create `internal-app/` and `consumer-app/` frontend folders

### Block 2 — Mock Data Seed (Hours 2–4) ✅ COMPLETE
- [x] Create `Data/Seeder.cs` — checks if data already seeded (count areas), skips if so
- [x] Seed `areas` — 3 countries, 66 states/provinces (50 US + 10 CA + 6 MX), 33 cities
- [x] Seed `emissions_data` — 5 categories per area (trucking 35%, factory 30%, energy 25%, refrigeration 7%, packaging 3%), year 2023 → 510 rows
- [x] Seed `carbon_initiatives` — RVMs, EV fleet %, renewable % per area → 306 rows
- [x] Seed `rvm_machines` — 29 individual machines across 10 major cities with real coordinates
- [x] Seed `admin_users` — admin@coke.com / Demo1234! (SHA-256 hashed)
- [x] Run grading formula → 102 grades (A/B/C split evenly: 22/22/22 for states)
- [x] Grade formula: raw=total MTCO2e, deduction=rvm*200+ev_pct*500+renewable_pct*300, final=max(0,raw-deduction), grades by percentile within type
- [x] Verified: Texas=C(52400), California=A(16900), Washington=A(0), PA=C(32500)

### Block 3 — Auth Endpoints (Hours 3–6) ✅ COMPLETE
- [x] `POST /api/auth/admin/login` — validates admin_users table, returns JWT
- [x] `POST /api/auth/register` — creates user with SHA-256 hashed password, returns JWT
- [x] `POST /api/auth/login` — validates users table, returns JWT
- [x] JWT: 8-hour expiry, claims: sub, email, name, ClaimTypes.Role, ClaimTypes.NameIdentifier
- [x] All status codes correct: 200 success, 401 bad creds, 409 duplicate email, 400 missing fields
- [x] Verified JWT payload contains role=admin/user claim for [Authorize(Roles="admin")] to work

### Block 4 — Internal Dashboard: Core Pages (Hours 6–10) ✅ COMPLETE
- [x] `api/Controllers/DashboardController.cs` — GET /api/dashboard/summary (KPIs, worst areas)
- [x] `api/Controllers/EmissionsController.cs` — GET /api/emissions/grades?type=, POST /api/emissions/recompute-grades
- [x] `internal-app/app.js` — auth check, apiFetch() with Bearer token, logout(), gradeColor(), gradeBadge(), fmt(), setActivePage()
- [x] `internal-app/login.html` — Bootstrap form, POST to admin login, stores token in localStorage
- [x] `internal-app/index.html` — 6 KPI cards (total emissions, grade A/B/C counts, active RVMs, bottles recycled), worst 5 areas table, grading system explanation card
- [x] `internal-app/emissions.html` — Countries/States/Cities toggle, Leaflet map with color-coded circle markers + popups, Chart.js bar chart top 20, full breakdown table with all columns
- [x] All pages share consistent navbar (dark #1a1a2e background, Coca-Cola red accent)
- [x] Build succeeds, 0 errors

### Block 5 — Internal Dashboard: RVM Impact + AI Page (Hours 10–13)
- [ ] `internal-app/rvm-impact.html`
  - [ ] Dropdown to select area
  - [ ] Slider: "Add X RVMs" (0–500)
  - [ ] Display: current emissions vs. projected after RVMs, % reduction
  - [ ] "Generate Demo QR" button — QRCode.js renders QR for selected RVM machine
  - [ ] Table of existing RVM machines in area with scan counts
- [ ] `internal-app/ai-insights.html`
  - [ ] List of areas with pre-generated insights (from `ai_insights` table)
  - [ ] Each insight in a card: area name, grade badge, insight text
  - [ ] Text input + "Ask Gemini" button for live queries (calls `POST /api/insights/query`)

### Block 6 — Gemini AI Integration (Hours 13–16)
- [ ] Create `Services/GeminiService.cs`
  - [ ] `GenerateInsight(area, emissionsData, initiatives)` — builds prompt, calls Gemini API, returns text
  - [ ] `QueryWithContext(area, userQuestion)` — live query with area context prepended
- [ ] Create `InsightsController.cs`
  - [ ] `GET /api/insights?areaId=` — return stored insights
  - [ ] `POST /api/insights/query` — live Gemini call, return response text
- [ ] Wire Gemini calls into `Seeder.cs` — generate insights for 3 countries + 5 worst states on first seed
- [ ] Add Gemini API key to `appsettings.json` (placeholder — user provides real key)

### Block 7 — Consumer App: Auth + Home + History (Hours 16–19)
- [ ] `consumer-app/app.js` — auth check, `apiFetch()` wrapper, redirect to login if no user token
- [ ] `consumer-app/login.html` — Bootstrap form, POST to user login, store token
- [ ] `consumer-app/register.html` — name/email/password form, POST to register
- [ ] `consumer-app/index.html`
  - [ ] Welcome banner with user's name
  - [ ] Points balance (large, prominent)
  - [ ] Progress bar toward 100-point milestone
  - [ ] Quick links: Scan a Bottle, View Rewards, History
  - [ ] Recent activity (last 3 scans)
- [ ] `consumer-app/history.html` — table of all scans and redemptions with dates and points

### Block 8 — Consumer App: Scan + Rewards (Hours 19–22)
- [ ] `consumer-app/scan.html`
  - [ ] On page load: read `?rvm_id=` and `?barcode=` from URL params
  - [ ] If params present: auto-submit scan to `POST /api/rvm/scan`, show result
  - [ ] If no params: show instructions ("Scan a QR code at a Coca-Cola RVM to earn points")
  - [ ] Success state: animated +2 points, updated balance, confetti or highlight
- [ ] `consumer-app/rewards.html`
  - [ ] Milestone cards: 20 pts, 50 pts, 100 pts with lock/unlock state
  - [ ] "Redeem 100 Points" button — calls `POST /api/users/redeem`, shows coupon code
  - [ ] Issued coupons list with codes and status

### Block 9 — Polish + QR Demo + End-to-End Test (Hours 22–24)
- [ ] Shared nav bar on all internal pages (Dashboard, Emissions, RVM Impact, AI Insights, Logout)
- [ ] Shared nav bar on all consumer pages (Home, Scan, Rewards, History, Logout)
- [ ] Coca-Cola branding: red (#F40009) primary color overrides via minimal custom CSS
- [ ] Loading spinners on all async fetches
- [ ] Error messages on failed API calls
- [ ] End-to-end demo flow test:
  - [ ] Admin logs in → sees dashboard → views emissions map → generates QR for an RVM
  - [ ] Consumer registers → scans QR → earns points → redeems coupon
  - [ ] Admin refreshes → sees new scan reflected in RVM scan count
- [ ] README with run instructions

---

## Run Instructions (for handoff / judges)

### Start the API
```bash
cd UA_Innovate/api
dotnet run
# API runs on https://localhost:5000
# database.db auto-created and seeded on first run
```

### Serve the frontends
```bash
# Internal app — open in browser directly or use live server on port 5500
# Consumer app — open in browser directly or use live server on port 5501
# Both apps call API at http://localhost:5000
```

### Default credentials
- Admin: `admin@coke.com` / `Demo1234!`
- Consumer: register a new account via consumer app

### Gemini API Key
Set in `UA_Innovate/api/appsettings.json`:
```json
"GeminiApiKey": "YOUR_KEY_HERE"
```
Get free key at: https://aistudio.google.com/app/apikey

---

## Key Decisions & Constraints
- SQLite only (not MySQL) — zero server setup, perfect for 24-hour demo
- Single shared API for both apps (simpler, one database)
- Gemini API called at seed time to avoid rate limits during live demo
- QR code flow uses URL params (no camera scanning needed) — more reliable for demo
- All data is North America only for this demo (US, Canada, Mexico)
- No email verification, no password complexity enforcement — this is a demo
