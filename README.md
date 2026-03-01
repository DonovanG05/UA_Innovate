# Coca-Cola Sustainability & Loyalty Platform

This project is a 24-hour hackathon submission featuring a shared ASP.NET Core API and two separate vanilla HTML/JS web applications:
1. **Internal Dashboard**: An administrative portal to monitor emissions data, map high-priority areas, analyze the impact of Reverse Vending Machine (RVM) deployment, and receive Gemini AI-generated strategic insights.
2. **Consumer Loyalty App**: A mobile-first web app where consumers scan QR codes at RVM locations to earn points and redeem them for rewards (like free Coca-Cola products).

## Tech Stack
- **Backend:** ASP.NET Core Web API (.NET 8), C#
- **Database:** SQLite (Raw SQL, No ORMs)
- **AI:** Google Gemini 2.5 Flash (via free API key)
- **Frontend:** Vanilla HTML, CSS, JavaScript (ES6+), Bootstrap 5.3 via CDN, Chart.js, Leaflet.js, QRCode.js

## Project Structure
- `/api/` - The shared ASP.NET Core backend.
- `/internal-app/` - The admin dashboard.
- `/consumer-app/` - The public loyalty application.

---

## Run Instructions

### 1. Configure the API
The backend requires a Gemini API key for AI insights.
1. Create the file `api/env/.env` (this file is gitignored — never commit it).
2. Add your key from [Google AI Studio](https://aistudio.google.com/app/apikey):
   ```
   GEMINI_API_KEY=your_key_here
   ```
*(If left unconfigured, the app still runs but AI responses will be disabled.)*

### 2. Start the Backend API
1. Open a terminal and navigate to the API directory:
   ```bash
   cd UA_Innovate/api
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. The API will start on `http://localhost:5009`. On the first run, it will automatically create `database.db` and seed it with all required mock data.

### 3. Start the Frontends
You can serve the frontends using any standard HTTP server (like VS Code's "Live Server" extension, Python's `http.server`, or `npx serve`).

**Internal App:**
- Serve the `internal-app` directory on `http://localhost:5500`.
- Three demo accounts are pre-seeded with different access levels:

| Role | Email | Password | Access |
|------|-------|----------|--------|
| **Admin** | `admin@coke.com` | `Demo1234!` | Everything |
| **Marketing** | `marketing@coke.com` | `Marketing2026!` | Dashboard, Bottle Data, Users |
| **Sustainability** | `sustain@coke.com` | `Sustain2026!` | Dashboard, Emissions, RVM Impact, Bottle Data |

**Consumer App:**
- Serve the `consumer-app` directory on `http://localhost:5501`.
- Register a new account or log in if you already have one.

---

## Demo Flow

### Role-Based Access
1. Log in as **Marketing** (`marketing@coke.com` / `Marketing2026!`) — notice only Dashboard, Bottle Data, and Users are visible in the nav.
2. Log in as **Sustainability** (`sustain@coke.com` / `Sustain2026!`) — see emissions and RVM impact data but not user PII.
3. Log in as **Admin** (`admin@coke.com` / `Demo1234!`) for full access including CSV exports.

### Core Features
1. **Dashboard:** KPI overview, interactive emissions map, and Live AI Consultant — ask Gemini questions about any area's carbon footprint.
2. **Emissions:** Explore emissions by country, state, or city. Filter and sort by grade, category, or total MTCO₂e.
3. **RVM Impact:** Select a city, adjust the slider to simulate new RVM deployments, and see projected emissions reduction and ROI.
4. **Generate QR:** In the "RVM Impact" tab, click "Generate QR" on any machine to get a consumer scan link.
5. **Consumer Scan:** Open the Consumer App, register an account, then scan the QR (or open the link) to earn points.
6. **Redeem:** Once you reach 100 points in the Consumer App, redeem them for a coupon code.
7. **Bottle Data:** View aggregated recycling stats — total scans, material breakdown, top brands, and top RVM locations.
8. **Users:** Browse loyalty program members with masked PII, sort by points or tier, and export to CSV (admin only).
