# Coca-Cola Sustainability & Loyalty Platform

This project is a 24-hour hackathon submission featuring a shared ASP.NET Core API and two separate vanilla HTML/JS web applications:
1. **Internal Dashboard**: An administrative portal to monitor emissions data, map high-priority areas, analyze the impact of Reverse Vending Machine (RVM) deployment, and receive Gemini AI-generated strategic insights.
2. **Consumer Loyalty App**: A mobile-first web app where consumers scan QR codes at RVM locations to earn points and redeem them for rewards (like free Coca-Cola products).

## Tech Stack
- **Backend:** ASP.NET Core Web API (.NET 8), C#
- **Database:** SQLite (Raw SQL, No ORMs)
- **AI:** Google Gemini 1.5 Flash (via free API key)
- **Frontend:** Vanilla HTML, CSS, JavaScript (ES6+), Bootstrap 5.3 via CDN, Chart.js, Leaflet.js, QRCode.js

## Project Structure
- `/api/` - The shared ASP.NET Core backend.
- `/internal-app/` - The admin dashboard.
- `/consumer-app/` - The public loyalty application.

---

## Run Instructions

### 1. Configure the API
By default, the backend needs a Gemini API Key to function correctly for AI insights. 
1. Open `api/appsettings.json`.
2. Locate the `"GeminiApiKey"` field and replace `"YOUR_GEMINI_API_KEY_HERE"` with your actual Gemini API key from [Google AI Studio](https://aistudio.google.com/app/apikey).
*(Note: If left unconfigured, the app will still run and fall back to mock AI responses).*

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
- Log in with:
  - **Email:** `admin@coke.com`
  - **Password:** `Demo1234!`

**Consumer App:**
- Serve the `consumer-app` directory on `http://localhost:5501`.
- Register a new account or log in if you already have one.

---

## Demo Flow
1. **Admin Dashboard:** Log in as admin, explore the KPI dashboard, and view the interactive map of high-emission areas.
2. **RVM Impact:** Go to the "RVM Impact" tab, select a target city, and use the slider to simulate the emissions reduction from deploying new machines.
3. **Generate QR:** In the "RVM Impact" tab, find an existing RVM machine in the list and click "Generate QR".
4. **Consumer Scan:** In the Consumer App, register a new account.
5. **Earn Points:** To simulate scanning the physical RVM, click the generated QR code link (or scan it with a mobile device connected to the same local network). This will open the Consumer App's scan page and automatically award you 2 points!
6. **Redeem:** Go to the "Rewards" tab. Once you reach 100 points, click the button to generate a discount coupon code.
7. **AI Insights:** Head back to the admin dashboard and view the "AI Insights" tab to see pre-generated recommendations based on the region's grade, or ask Gemini custom questions about a specific area's carbon footprint.
