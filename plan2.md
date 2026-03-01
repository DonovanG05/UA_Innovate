# Post-Demo Implementation Progress

## Status Key
✅ Complete | 🔄 In Progress | ⬜ Pending

---

## 1. RVM Impact Slider — Realistic Numbers ✅
- `api/Controllers/RvmController.cs`: `reductionPerRvm` changed 200 → 15 MTCO₂e
- `internal-app/rvm-impact.html`: slider `max="500" step="10"` → `max="100" step="1"`
- Formula description updated (200 → 15 MTCO₂e)
- `gradeOutlook` thresholds updated: 15%→2%, 5%→0.5%

## 2. Internal Tool — User Information Tab ✅
- `internal-app/users.html`: NEW — table with masked email, first name, age, zip (masked), points, scans, fav RVM, tier
- Filter by zip, sort by points/scans/tier
- `api/Controllers/UsersController.cs`: added `GET /api/users` admin endpoint
- Users nav link added to all 4 internal pages

## 3. Registration — Age & Zip Code ✅
- `consumer-app/register.html`: Age + Zip Code fields added to form + fetch body
- `api/Controllers/AuthController.cs`: `RegisterRequest` extended with `int? Age`, `string? ZipCode`
- INSERT SQL updated to include `age, zip_code`
- `api/Data/Database.cs`: schema + ALTER TABLE migration for `age`, `zip_code`

## 4. Bottle Type & Brand Tracking ✅
- `api/Data/Database.cs`: `material_type TEXT, brand TEXT` added to `rvm_scans` + migration
- `api/Controllers/RvmController.cs`: `ScanRequest` extended; INSERT includes material/brand
- `consumer-app/scan.html`: Selection state UI (Plastic/Aluminum + brand buttons) before scan confirm

## 5. CSV Export from Internal Tool ✅
- `api/Controllers/ExportController.cs`: NEW — `/api/export/users`, `/api/export/scans`, `/api/export/emissions`
- `internal-app/users.html`: "Export Users CSV" button
- `internal-app/emissions.html`: "Export Emissions CSV" button
- `internal-app/rvm-impact.html`: "Export Scan Data CSV" button

## 6. RVM Cost Visibility ($60,000/unit) ✅
- `internal-app/rvm-impact.html`: Cost Calculator card added below slider results
- Shows: RVM Count × $60,000, total MTCO₂e reduction, $/MTCO₂e offset
- `RVM_COST = 60000` constant in JavaScript

## 7. API Key / Environment Variable Setup ✅
- `api/env/.env`: created (gitignore manually)
- `api/env/.env.example`: committed template
- `api/Program.cs`: .env loader block added at startup
- `api/appsettings.json`: `GeminiApiKey` cleared to ""
- `api/Services/GeminiService.cs`: env var fallback added

## 8. User Data Security (PBKDF2) ✅
- `api/Controllers/AuthController.cs`:
  - `HashPasswordPbkdf2()` using `Rfc2898DeriveBytes` (100k iterations, SHA256, 16-byte salt)
  - `VerifyPassword()` handles both old SHA256 and new `pbkdf2:<salt>:<hash>` format
  - Register uses PBKDF2; Login/AdminLogin use VerifyPassword
- `api/Controllers/UsersController.cs`: admin endpoint returns masked email + first name only

## 9. AI Front and Center on Main Dashboard ✅
- `internal-app/index.html`:
  - Panel A: AI Suggested Actions (3 cards auto-loaded from worst area)
  - Panel B: Quick AI Chat widget (collapsed by default, session history)
  - `defaultAiAreaId` set from `worstAreas[0]` on dashboard load
  - Chat uses `sessionStorage['aiHistory']` for last 3 turns
  - No new API endpoints needed (reuses `/api/insights` and `/api/insights/query`)

---

## Verification Checklist
- [ ] Register new user with age + zip → confirm saved in SQLite
- [ ] Scan a bottle → confirm material_type, brand in database
- [ ] Internal tool Users tab → masked email + tier display
- [ ] Export CSV from Users tab → correct columns
- [ ] Slider 10 RVMs → ~150 MTCO₂e + $600,000 cost card
- [ ] Add Gemini key to `api/env/.env` → AI Insights and dashboard chat load
- [ ] Admin login still works (SHA256 compat via VerifyPassword)
- [ ] Dashboard shows AI suggestion cards + inline chat responds
