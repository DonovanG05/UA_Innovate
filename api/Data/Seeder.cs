using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using api.Data;
using api.Services;

public static class Seeder
{
    record AreaData(string Name, double Lat, double Lon, double TotalMtco2e, int RvmCount, double EvPct, double RenewablePct);
    record CityData(string Name, string ParentName, double Lat, double Lon, double TotalMtco2e, int RvmCount, double EvPct, double RenewablePct);
    record RvmMachine(string CityName, string LocationName, double Lat, double Lon);

    public static async Task SeedAsync(Database db, IConfiguration config, GeminiService gemini)
    {
        using var conn = db.Connect();
        conn.Open();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM areas";
        if ((long)check.ExecuteScalar()! > 0)
        {
            // DB already seeded — insert mock scans if none
            var scanCheck = conn.CreateCommand();
            scanCheck.CommandText = "SELECT COUNT(*) FROM rvm_scans";
            if ((long)scanCheck.ExecuteScalar()! == 0)
                InsertMockScans(conn);
            ImportMissingDataForExistingUsers(conn);
            SeedRoleAccounts(conn);
            return;
        }

        using var tx = conn.BeginTransaction();

        var countryIds = new Dictionary<string, int>();
        var stateIds   = new Dictionary<string, int>();
        var cityIds    = new Dictionary<string, int>();

        // ── COUNTRIES ────────────────────────────────────────────────────────
        var countries = new AreaData[]
        {
            new("United States", 39.50,  -98.35, 900000, 650, 10.0, 22.0),
            new("Canada",        56.13, -106.35,  80000, 120,  8.0, 58.0),
            new("Mexico",        23.63, -102.55, 200000,  80,  4.0, 12.0)
        };

        foreach (var c in countries)
        {
            var id = InsertArea(conn, c.Name, "country", null, c.Lat, c.Lon);
            countryIds[c.Name] = id;
            InsertEmissions(conn, id, c.TotalMtco2e, 2023);
            InsertInitiatives(conn, id, c.RvmCount, c.EvPct, c.RenewablePct);
        }

        // ── US STATES ────────────────────────────────────────────────────────
        var usStates = new AreaData[]
        {
            new("Texas",              31.97,  -99.90, 72000, 45,  8.0, 22.0),
            new("California",         36.78, -119.42, 67500,120, 28.0, 42.0),
            new("Ohio",               40.41,  -82.91, 45000, 38,  6.0, 12.0),
            new("Pennsylvania",       41.20,  -77.19, 45000, 35,  5.0, 10.0),
            new("Florida",            27.76,  -81.69, 40500, 42,  7.0, 18.0),
            new("Illinois",           40.63,  -89.40, 40500, 36,  9.0, 22.0),
            new("Georgia",            32.16,  -82.90, 36000, 40,  8.0, 15.0),
            new("New York",           42.17,  -74.95, 36000, 55, 12.0, 28.0),
            new("Michigan",           44.31,  -85.60, 31500, 30, 10.0, 18.0),
            new("North Carolina",     35.63,  -79.81, 31500, 28,  5.0, 14.0),
            new("Indiana",            39.85,  -86.26, 27000, 22,  4.0, 12.0),
            new("Tennessee",          35.86,  -86.35, 27000, 25,  5.0, 10.0),
            new("Virginia",           37.77,  -78.17, 22500, 24,  7.0, 18.0),
            new("Wisconsin",          44.27,  -89.62, 22500, 20,  5.0, 22.0),
            new("Alabama",            32.78,  -86.83, 18000, 15,  3.0,  8.0),
            new("Missouri",           38.46,  -92.30, 18000, 18,  4.0, 12.0),
            new("Arizona",            34.05, -111.09, 18000, 22,  6.0, 20.0),
            new("South Carolina",     33.84,  -80.90, 18000, 16,  4.0, 10.0),
            new("Kentucky",           37.67,  -84.87, 16200, 14,  3.0,  8.0),
            new("Colorado",           39.55, -105.78, 16200, 25, 12.0, 28.0),
            new("Minnesota",          46.43,  -93.90, 16200, 20,  8.0, 30.0),
            new("Washington",         47.75, -120.74, 13500, 35, 18.0, 52.0),
            new("Louisiana",          31.16,  -91.87, 13500, 10,  2.0,  6.0),
            new("Mississippi",        32.74,  -89.67, 13500,  8,  2.0,  5.0),
            new("Oklahoma",           35.56,  -96.93, 13500, 12,  3.0, 18.0),
            new("Arkansas",           34.80,  -92.20, 10800, 10,  2.0,  8.0),
            new("Iowa",               42.08,  -93.50, 10800, 12,  4.0, 42.0),
            new("Kansas",             38.53,  -98.39, 10800, 10,  3.0, 38.0),
            new("Nebraska",           41.49,  -99.90,  9000,  8,  3.0, 28.0),
            new("New Mexico",         34.31, -106.02,  9000,  8,  4.0, 22.0),
            new("Nevada",             38.50, -117.07,  7200, 12,  8.0, 18.0),
            new("Utah",               39.32, -111.09,  7200, 10,  5.0, 15.0),
            new("West Virginia",      38.49,  -80.95,  7200,  5,  1.0,  5.0),
            new("Maryland",           39.06,  -76.80,  7200, 18, 10.0, 22.0),
            new("Connecticut",        41.60,  -72.73,  5400, 14, 12.0, 18.0),
            new("Oregon",             43.93, -120.56,  5400, 20, 14.0, 52.0),
            new("Massachusetts",      42.23,  -71.53,  5400, 22, 12.0, 28.0),
            new("New Jersey",         40.22,  -74.76,  5400, 20, 10.0, 18.0),
            new("Idaho",              44.24, -114.48,  4500,  6,  4.0, 42.0),
            new("Maine",              45.25,  -69.45,  3600,  6,  5.0, 35.0),
            new("Montana",            46.88, -110.36,  3600,  4,  2.0, 25.0),
            new("North Dakota",       47.53,  -99.78,  3600,  3,  2.0, 35.0),
            new("South Dakota",       44.44, -100.23,  3600,  4,  2.0, 28.0),
            new("Wyoming",            43.08, -107.29,  2700,  2,  1.0, 12.0),
            new("New Hampshire",      43.45,  -71.56,  2700,  5,  6.0, 20.0),
            new("Delaware",           38.91,  -75.53,  2700,  6,  8.0, 12.0),
            new("Vermont",            44.26,  -72.58,  1800,  4,  8.0, 40.0),
            new("Alaska",             64.20, -153.37,  1800,  2,  1.0, 22.0),
            new("Hawaii",             19.90, -155.56,  1800,  3,  8.0, 32.0),
            new("Rhode Island",       41.70,  -71.55,  1800,  5, 10.0, 18.0)
        };

        int usId = countryIds["United States"];
        foreach (var s in usStates)
        {
            var id = InsertArea(conn, s.Name, "state", usId, s.Lat, s.Lon);
            stateIds[s.Name] = id;
            InsertEmissions(conn, id, s.TotalMtco2e, 2023);
            InsertInitiatives(conn, id, s.RvmCount, s.EvPct, s.RenewablePct);
        }

        // ── CANADIAN PROVINCES ───────────────────────────────────────────────
        var caProvinces = new AreaData[]
        {
            new("Ontario",                      51.25,  -85.32, 28000, 35,  8.0, 22.0),
            new("Quebec",                       52.94,  -73.55, 20000, 30, 10.0, 58.0),
            new("British Columbia",             53.73, -127.65, 12000, 25, 15.0, 85.0),
            new("Alberta",                      53.93, -116.58, 10000, 15,  5.0, 18.0),
            new("Manitoba",                     54.90,  -97.11,  4000,  8,  4.0, 68.0),
            new("Saskatchewan",                 52.94, -106.45,  2500,  5,  2.0, 22.0),
            new("Nova Scotia",                  45.00,  -63.00,  1500,  6,  5.0, 28.0),
            new("New Brunswick",                46.56,  -66.46,  1200,  4,  4.0, 20.0),
            new("Newfoundland and Labrador",    53.14,  -57.66,   500,  2,  2.0, 45.0),
            new("Prince Edward Island",         46.51,  -63.41,   300,  2,  4.0, 28.0)
        };

        int caId = countryIds["Canada"];
        foreach (var p in caProvinces)
        {
            var id = InsertArea(conn, p.Name, "state", caId, p.Lat, p.Lon);
            stateIds[p.Name] = id;
            InsertEmissions(conn, id, p.TotalMtco2e, 2023);
            InsertInitiatives(conn, id, p.RvmCount, p.EvPct, p.RenewablePct);
        }

        // ── MEXICAN STATES ───────────────────────────────────────────────────
        var mxStates = new AreaData[]
        {
            new("Jalisco",    20.66, -103.35, 48000, 20, 4.0, 12.0),
            new("Nuevo Leon", 25.59,  -99.99, 42000, 18, 5.0, 15.0),
            new("CDMX",       19.43,  -99.13, 35000, 30,10.0,  8.0),
            new("Puebla",     19.04,  -98.20, 30000, 12, 3.0,  8.0),
            new("Guanajuato", 21.02, -101.26, 28000, 10, 3.0, 10.0),
            new("Veracruz",   19.18,  -96.14, 17000,  8, 2.0, 12.0)
        };

        int mxId = countryIds["Mexico"];
        foreach (var s in mxStates)
        {
            var id = InsertArea(conn, s.Name, "state", mxId, s.Lat, s.Lon);
            stateIds[s.Name] = id;
            InsertEmissions(conn, id, s.TotalMtco2e, 2023);
            InsertInitiatives(conn, id, s.RvmCount, s.EvPct, s.RenewablePct);
        }

        // ── CITIES ───────────────────────────────────────────────────────────
        var cities = new CityData[]
        {
            // US
            new("New York City",  "New York",        40.71, -74.01,  8500, 40, 15.0, 25.0),
            new("Los Angeles",    "California",      34.05,-118.24, 15000, 70, 30.0, 38.0),
            new("Chicago",        "Illinois",        41.88, -87.63,  9000, 28,  9.0, 20.0),
            new("Houston",        "Texas",           29.76, -95.37, 18000, 30,  6.0, 15.0),
            new("Atlanta",        "Georgia",         33.75, -84.39,  9500, 25, 10.0, 14.0),
            new("Dallas",         "Texas",           32.78, -96.80, 14000, 22,  7.0, 20.0),
            new("Philadelphia",   "Pennsylvania",    39.95, -75.17, 10000, 20,  6.0,  9.0),
            new("Phoenix",        "Arizona",         33.45,-112.07,  5500, 15,  5.0, 22.0),
            new("Denver",         "Colorado",        39.74,-104.98,  4500, 18, 12.0, 28.0),
            new("Seattle",        "Washington",      47.61,-122.33,  4000, 22, 20.0, 50.0),
            new("Miami",          "Florida",         25.77, -80.19,  8500, 22,  6.0, 16.0),
            new("Detroit",        "Michigan",        42.33, -83.05,  7000, 18,  9.0, 15.0),
            new("Minneapolis",    "Minnesota",       44.98, -93.27,  4000, 14,  9.0, 32.0),
            new("Charlotte",      "North Carolina",  35.23, -80.84,  7000, 16,  5.0, 12.0),
            new("Nashville",      "Tennessee",       36.17, -86.78,  6500, 15,  5.0,  9.0),
            new("Columbus",       "Ohio",            39.96, -82.99,  8000, 20,  6.0, 11.0),
            new("Indianapolis",   "Indiana",         39.77, -86.16,  6500, 14,  4.0, 10.0),
            new("San Antonio",    "Texas",           29.42, -98.49, 10000, 18,  5.0, 18.0),
            new("Tampa",          "Florida",         27.95, -82.46,  6000, 16,  6.0, 14.0),
            new("Boston",         "Massachusetts",   42.36, -71.06,  2500, 14, 13.0, 26.0),
            new("Las Vegas",      "Nevada",          36.17,-115.14,  3000,  8,  7.0, 16.0),
            new("Portland",       "Oregon",          45.52,-122.68,  2500, 12, 15.0, 52.0),
            // Canada
            new("Toronto",        "Ontario",          43.65, -79.38, 10000, 22,  9.0, 20.0),
            new("Montreal",       "Quebec",           45.51, -73.55,  8000, 18, 10.0, 55.0),
            new("Vancouver",      "British Columbia", 49.28,-123.12,  5500, 18, 18.0, 82.0),
            new("Calgary",        "Alberta",          51.05,-114.07,  4500, 10,  5.0, 15.0),
            new("Ottawa",         "Ontario",          45.42, -75.70,  3500, 10,  9.0, 22.0),
            // Mexico
            new("Mexico City",    "CDMX",             19.43, -99.13, 28000, 22, 12.0,  7.0),
            new("Guadalajara",    "Jalisco",          20.66,-103.35, 22000, 14,  4.0, 10.0),
            new("Monterrey",      "Nuevo Leon",       25.67,-100.31, 20000, 12,  5.0, 14.0),
            new("Puebla City",    "Puebla",           19.04, -98.20, 14000,  8,  3.0,  8.0),
            new("Leon",           "Guanajuato",       21.12,-101.68, 12000,  6,  3.0,  9.0),
            new("Veracruz City",  "Veracruz",         19.18, -96.14,  8000,  5,  2.0, 11.0)
        };

        foreach (var c in cities)
        {
            if (!stateIds.TryGetValue(c.ParentName, out int parentId))
                throw new Exception($"Parent state not found: {c.ParentName}");
            var id = InsertArea(conn, c.Name, "city", parentId, c.Lat, c.Lon);
            cityIds[c.Name] = id;
            InsertEmissions(conn, id, c.TotalMtco2e, 2023);
            InsertInitiatives(conn, id, c.RvmCount, c.EvPct, c.RenewablePct);
        }

        // ── RVM MACHINES ─────────────────────────────────────────────────────
        var rvms = new RvmMachine[]
        {
            new("Houston",      "Kroger - 2900 Weslayan St",            29.738, -95.403),
            new("Houston",      "HEB - 4821 Washington Ave",            29.769, -95.392),
            new("Houston",      "Walmart Supercenter - Memorial",        29.764, -95.542),
            new("Los Angeles",  "Ralph's - 5400 Wilshire Blvd",         34.063,-118.342),
            new("Los Angeles",  "Whole Foods - 239 N Crescent Dr",      34.077,-118.396),
            new("Los Angeles",  "Target - 7100 Santa Monica Blvd",      34.099,-118.350),
            new("Los Angeles",  "Kroger - LAX Area",                    33.943,-118.408),
            new("New York City","Whole Foods - Columbus Circle",         40.769, -73.984),
            new("New York City","Associated Supermarket - Harlem",       40.811, -73.945),
            new("New York City","Key Food - Brooklyn",                   40.650, -73.950),
            new("New York City","C-Town Supermarkets - Queens",          40.735, -73.875),
            new("Atlanta",      "Publix - Ponce City Market",           33.770, -84.367),
            new("Atlanta",      "Kroger - Midtown Atlanta",             33.783, -84.383),
            new("Atlanta",      "Target - Buckhead",                    33.840, -84.371),
            new("Chicago",      "Jewel-Osco - Michigan Ave",            41.870, -87.626),
            new("Chicago",      "Mariano's - Lincoln Park",             41.922, -87.643),
            new("Chicago",      "Target - The Loop",                    41.881, -87.628),
            new("Seattle",      "QFC - Capitol Hill",                   47.620,-122.318),
            new("Seattle",      "Safeway - South Lake Union",           47.627,-122.337),
            new("Denver",       "King Soopers - LoDo",                  39.754,-104.999),
            new("Denver",       "Whole Foods - Cherry Creek",           39.717,-104.949),
            new("Toronto",      "Loblaws - Maple Leaf Gardens",         43.662, -79.380),
            new("Toronto",      "Metro - Yonge & Eglinton",             43.707, -79.399),
            new("Toronto",      "No Frills - Scarborough",              43.776, -79.257),
            new("Mexico City",  "OXXO - Polanco",                       19.432, -99.193),
            new("Mexico City",  "Walmart - Santa Fe",                   19.362, -99.261),
            new("Mexico City",  "Soriana - Pedregal",                   19.320, -99.202),
            new("Guadalajara",  "Walmart - Zapopan Centro",             20.720,-103.392),
            new("Guadalajara",  "OXXO - Chapalita",                     20.669,-103.401)
        };

        foreach (var rvm in rvms)
        {
            if (!cityIds.TryGetValue(rvm.CityName, out int cityId))
                throw new Exception($"City not found for RVM: {rvm.CityName}");
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO rvm_machines (area_id, location_name, latitude, longitude, active)
                VALUES ($areaId, $name, $lat, $lon, 1)
            """;
            cmd.Parameters.AddWithValue("$areaId", cityId);
            cmd.Parameters.AddWithValue("$name", rvm.LocationName);
            cmd.Parameters.AddWithValue("$lat", rvm.Lat);
            cmd.Parameters.AddWithValue("$lon", rvm.Lon);
            cmd.ExecuteNonQuery();
        }

        InsertMockScans(conn);

        // ── ADMIN USERS ──────────────────────────────────────────────────────
        var adminHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Demo1234!"))).ToLower();
        var adminCmd = conn.CreateCommand();
        adminCmd.CommandText = "INSERT INTO admin_users (email, password_hash, role) VALUES ('admin@coke.com', $hash, 'admin')";
        adminCmd.Parameters.AddWithValue("$hash", adminHash);
        adminCmd.ExecuteNonQuery();
        SeedRoleAccounts(conn);

        // ── GRADES ───────────────────────────────────────────────────────────
        ComputeGrades(conn);

        tx.Commit();

        // ── AI INSIGHTS (Post-commit) ────────────────────────────────────────
        await SeedAiInsights(db, gemini);
    }

    private static async Task SeedAiInsights(Database db, GeminiService gemini)
    {
        using var conn = db.Connect();
        conn.Open();

        // Target: 3 countries + 5 worst states
        var targetCmd = conn.CreateCommand();
        targetCmd.CommandText = """
            SELECT id, name, type FROM areas WHERE type = 'country'
            UNION ALL
            SELECT a.id, a.name, a.type FROM areas a
            JOIN emission_grades eg ON a.id = eg.area_id
            WHERE a.type = 'state'
            ORDER BY eg.raw_score DESC LIMIT 5
        """;

        var targets = new List<(int Id, string Name, string Type)>();
        using (var reader = targetCmd.ExecuteReader())
        {
            while (reader.Read()) targets.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        foreach (var t in targets)
        {
            // Gather context for Gemini
            var ctxCmd = conn.CreateCommand();
            ctxCmd.CommandText = """
                SELECT 
                    eg.grade, eg.raw_score,
                    (SELECT amount_mtco2e FROM emissions_data WHERE area_id = $id AND category = 'trucking') as trucking,
                    (SELECT amount_mtco2e FROM emissions_data WHERE area_id = $id AND category = 'factory') as factory,
                    (SELECT amount_mtco2e FROM emissions_data WHERE area_id = $id AND category = 'energy') as energy,
                    (SELECT amount_mtco2e FROM emissions_data WHERE area_id = $id AND category IN ('refrigeration', 'packaging')) as other,
                    (SELECT value FROM carbon_initiatives WHERE area_id = $id AND initiative_type = 'rvm') as rvm_count,
                    (SELECT value FROM carbon_initiatives WHERE area_id = $id AND initiative_type = 'ev_fleet') as ev_pct,
                    (SELECT value FROM carbon_initiatives WHERE area_id = $id AND initiative_type = 'renewable_energy') as re_pct
                FROM emission_grades eg WHERE eg.area_id = $id
            """;
            ctxCmd.Parameters.AddWithValue("$id", t.Id);

            using var reader = ctxCmd.ExecuteReader();
            if (reader.Read())
            {
                var grade = reader.GetString(0);
                var total = reader.GetDouble(1);
                var trucking = reader.GetDouble(2);
                var factory = reader.GetDouble(3);
                var energy = reader.GetDouble(4);
                var other = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);
                var rvm = (int)reader.GetDouble(6);
                var ev = reader.GetDouble(7);
                var re = reader.GetDouble(8);

                var bottleData = GetBottleSummary(t.Id, conn);

                var insight = await gemini.GenerateInsight(t.Name, t.Type, grade, total, 
                    (trucking/total)*100, (factory/total)*100, (energy/total)*100, (other/total)*100, 
                    rvm, ev, re, bottleData);

                var insCmd = conn.CreateCommand();
                insCmd.CommandText = "INSERT INTO ai_insights (area_id, insight_text, suggestion_type, generated_at) VALUES ($id, $text, 'strategic', $now)";
                insCmd.Parameters.AddWithValue("$id", t.Id);
                insCmd.Parameters.AddWithValue("$text", insight);
                insCmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                insCmd.ExecuteNonQuery();
            }
        }
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static string GetBottleSummary(int areaId, SqliteConnection conn)
    {
        var scanCmd = conn.CreateCommand();
        scanCmd.CommandText = """
            SELECT COUNT(*), 
                   SUM(CASE WHEN material_type = 'plastic' THEN 1 ELSE 0 END) as plastic,
                   SUM(CASE WHEN material_type = 'aluminum' THEN 1 ELSE 0 END) as aluminum
            FROM rvm_scans s
            JOIN rvm_machines m ON s.rvm_id = m.id
            WHERE m.area_id = $areaId
        """;
        scanCmd.Parameters.AddWithValue("$areaId", areaId);
        
        using var reader = scanCmd.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) == 0) return "No recycling data available.";
        
        var total = reader.GetInt32(0);
        var plastic = reader.GetInt32(1);
        var aluminum = reader.GetInt32(2);
        reader.Close();

        var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = """
            SELECT brand, COUNT(*) as cnt
            FROM rvm_scans s
            JOIN rvm_machines m ON s.rvm_id = m.id
            WHERE m.area_id = $areaId AND brand IS NOT NULL
            GROUP BY brand ORDER BY cnt DESC LIMIT 3
        """;
        brandCmd.Parameters.AddWithValue("$areaId", areaId);
        
        var brands = new List<string>();
        using var brandReader = brandCmd.ExecuteReader();
        while (brandReader.Read())
        {
            brands.Add($"{brandReader.GetString(0)} ({brandReader.GetInt32(1)})");
        }

        var brandSummary = brands.Count > 0 ? "Top brands: " + string.Join(", ", brands) : "No brand data.";
        return $"Total scans: {total} (Plastic: {plastic}, Aluminum: {aluminum}). {brandSummary}";
    }

    private static void InsertMockScans(SqliteConnection conn)
    {
        var rvmIdList = new List<int>();
        using (var rvmReader = conn.CreateCommand())
        {
            rvmReader.CommandText = "SELECT id FROM rvm_machines";
            using var rr = rvmReader.ExecuteReader();
            while (rr.Read()) rvmIdList.Add(rr.GetInt32(0));
        }
        if (rvmIdList.Count == 0) return;
        var brands = new[] { "Sprite", "Coke", "Diet Coke", "Coke Zero" };
        var materials = new[] { "plastic", "aluminum" };
        var rng = new Random(42);
        var baseDate = DateTime.UtcNow.AddMonths(-6);
        for (int i = 0; i < 200; i++)
        {
            int rvmId = rvmIdList[rng.Next(rvmIdList.Count)];
            string brand = brands[rng.Next(brands.Length)];
            string material = materials[rng.Next(materials.Length)];
            var scannedAt = baseDate.AddDays(rng.Next(180)).AddMinutes(rng.Next(1440)).ToString("yyyy-MM-dd HH:mm:ss");
            var scanCmd = conn.CreateCommand();
            scanCmd.CommandText = """
                INSERT INTO rvm_scans (rvm_id, user_id, product_barcode, scanned_at, points_awarded, material_type, brand)
                VALUES ($rvmId, NULL, $barcode, $scannedAt, 2, $material, $brand)
            """;
            scanCmd.Parameters.AddWithValue("$rvmId", rvmId);
            scanCmd.Parameters.AddWithValue("$barcode", $"MOCK{rng.Next(100000, 999999)}");
            scanCmd.Parameters.AddWithValue("$scannedAt", scannedAt);
            scanCmd.Parameters.AddWithValue("$material", material);
            scanCmd.Parameters.AddWithValue("$brand", brand);
            scanCmd.ExecuteNonQuery();
        }
    }

    private static void ImportMissingDataForExistingUsers(SqliteConnection conn)
    {
        var userIds = new List<int>();
        var userNames = new List<string>();
        using (var uCmd = conn.CreateCommand())
        {
            uCmd.CommandText = "SELECT id, name FROM users";
            using var ur = uCmd.ExecuteReader();
            while (ur.Read())
            {
                userIds.Add(ur.GetInt32(0));
                userNames.Add(ur.GetString(1));
            }
        }
        if (userIds.Count == 0) return;

        var rvmIds = new List<int>();
        using (var rCmd = conn.CreateCommand())
        {
            rCmd.CommandText = "SELECT id FROM rvm_machines";
            using var rr = rCmd.ExecuteReader();
            while (rr.Read()) rvmIds.Add(rr.GetInt32(0));
        }
        if (rvmIds.Count == 0) return;

        var rng = new Random(12345);
        var brands = new[] { "Sprite", "Coke", "Diet Coke", "Coke Zero" };
        var materials = new[] { "plastic", "aluminum" };
        var zips = new[] { "30301", "90210", "35203", "60601", "77001", "10001", "33101", "75201", "85001", "19101" };
        var baseDate = DateTime.UtcNow.AddMonths(-6);

        foreach (var userId in userIds)
        {
            using var tx = conn.BeginTransaction();
            try
            {
                var updCmd = conn.CreateCommand();
                updCmd.CommandText = "SELECT qr_identifier, zip_code FROM users WHERE id = $id";
                updCmd.Parameters.AddWithValue("$id", userId);
                string? qr = null;
                string? zip = null;
                using (var r = updCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        qr = r.IsDBNull(0) ? null : r.GetString(0);
                        zip = r.IsDBNull(1) ? null : r.GetString(1);
                    }
                }
                if (string.IsNullOrWhiteSpace(qr))
                {
                    var setQr = conn.CreateCommand();
                    setQr.CommandText = "UPDATE users SET qr_identifier = $v WHERE id = $id";
                    setQr.Parameters.AddWithValue("$v", "USER-" + userId + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
                    setQr.Parameters.AddWithValue("$id", userId);
                    setQr.ExecuteNonQuery();
                }
                if (string.IsNullOrWhiteSpace(zip))
                {
                    var setZip = conn.CreateCommand();
                    setZip.CommandText = "UPDATE users SET zip_code = $v WHERE id = $id";
                    setZip.Parameters.AddWithValue("$v", zips[rng.Next(zips.Length)]);
                    setZip.Parameters.AddWithValue("$id", userId);
                    setZip.ExecuteNonQuery();
                }

                var countScans = conn.CreateCommand();
                countScans.CommandText = "SELECT COUNT(*) FROM rvm_scans WHERE user_id = $uid";
                countScans.Parameters.AddWithValue("$uid", userId);
                var existingScans = (long)countScans.ExecuteScalar()!;
                int toAdd = existingScans == 0 ? rng.Next(10, 41) : 0;
                int pointsAdded = 0;
                for (int i = 0; i < toAdd; i++)
                {
                    int rvmId = rvmIds[rng.Next(rvmIds.Count)];
                    int pts = rng.Next(2) == 0 ? 1 : 2;
                    var scannedAt = baseDate.AddDays(rng.Next(180)).AddMinutes(rng.Next(1440)).ToString("yyyy-MM-dd HH:mm:ss");
                    var insScan = conn.CreateCommand();
                    insScan.CommandText = """
                        INSERT INTO rvm_scans (rvm_id, user_id, product_barcode, scanned_at, points_awarded, material_type, brand)
                        VALUES ($rvmId, $userId, $barcode, $scannedAt, $pts, $material, $brand)
                    """;
                    insScan.Parameters.AddWithValue("$rvmId", rvmId);
                    insScan.Parameters.AddWithValue("$userId", userId);
                    insScan.Parameters.AddWithValue("$barcode", "IMP" + rng.Next(100000, 999999));
                    insScan.Parameters.AddWithValue("$scannedAt", scannedAt);
                    insScan.Parameters.AddWithValue("$pts", pts);
                    insScan.Parameters.AddWithValue("$material", materials[rng.Next(materials.Length)]);
                    insScan.Parameters.AddWithValue("$brand", brands[rng.Next(brands.Length)]);
                    insScan.ExecuteNonQuery();

                    var insReward = conn.CreateCommand();
                    insReward.CommandText = """
                        INSERT INTO user_rewards (user_id, type, points, description, created_at)
                        VALUES ($userId, 'earn', $pts, 'RVM Bottle Scan', $scannedAt)
                    """;
                    insReward.Parameters.AddWithValue("$userId", userId);
                    insReward.Parameters.AddWithValue("$pts", pts);
                    insReward.Parameters.AddWithValue("$scannedAt", scannedAt);
                    insReward.ExecuteNonQuery();
                    pointsAdded += pts;
                }
                if (pointsAdded > 0)
                {
                    var updPoints = conn.CreateCommand();
                    updPoints.CommandText = "UPDATE users SET total_points = total_points + $pts WHERE id = $id";
                    updPoints.Parameters.AddWithValue("$pts", pointsAdded);
                    updPoints.Parameters.AddWithValue("$id", userId);
                    updPoints.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        var jykiaIdx = userNames.FindIndex(n => n.Contains("Jykia", StringComparison.OrdinalIgnoreCase));
        if (jykiaIdx >= 0)
        {
            int jykiaId = userIds[jykiaIdx];
            var alreadyBonus = conn.CreateCommand();
            alreadyBonus.CommandText = "SELECT COUNT(*) FROM user_rewards WHERE user_id = $id AND description = 'Bonus points' AND points = 100";
            alreadyBonus.Parameters.AddWithValue("$id", jykiaId);
            if ((long)alreadyBonus.ExecuteScalar()! == 0)
            {
                var add100 = conn.CreateCommand();
                add100.CommandText = "UPDATE users SET total_points = total_points + 100 WHERE id = $id";
                add100.Parameters.AddWithValue("$id", jykiaId);
                add100.ExecuteNonQuery();
                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                var insBonus = conn.CreateCommand();
                insBonus.CommandText = """
                    INSERT INTO user_rewards (user_id, type, points, description, created_at)
                    VALUES ($userId, 'earn', 100, 'Bonus points', $now)
                """;
                insBonus.Parameters.AddWithValue("$userId", jykiaId);
                insBonus.Parameters.AddWithValue("$now", now);
                insBonus.ExecuteNonQuery();
            }
        }
    }

    private static int InsertArea(SqliteConnection conn, string name, string type, int? parentId, double lat, double lon)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO areas (name, type, parent_id, latitude, longitude)
            VALUES ($name, $type, $parentId, $lat, $lon)
        """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$parentId", parentId.HasValue ? (object)parentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$lat", lat);
        cmd.Parameters.AddWithValue("$lon", lon);
        cmd.ExecuteNonQuery();

        var idCmd = conn.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return (int)(long)idCmd.ExecuteScalar()!;
    }

    private static void InsertEmissions(SqliteConnection conn, int areaId, double total, int year)
    {
        var categories = new (string Cat, double Pct)[]
        {
            ("trucking",      0.35),
            ("factory",       0.30),
            ("energy",        0.25),
            ("refrigeration", 0.07),
            ("packaging",     0.03)
        };

        foreach (var (cat, pct) in categories)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO emissions_data (area_id, category, amount_mtco2e, year)
                VALUES ($areaId, $cat, $amount, $year)
            """;
            cmd.Parameters.AddWithValue("$areaId", areaId);
            cmd.Parameters.AddWithValue("$cat", cat);
            cmd.Parameters.AddWithValue("$amount", Math.Round(total * pct, 1));
            cmd.Parameters.AddWithValue("$year", year);
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertInitiatives(SqliteConnection conn, int areaId, int rvmCount, double evPct, double renewablePct)
    {
        var items = new (string Type, double Value)[]
        {
            ("rvm",              rvmCount),
            ("ev_fleet",         evPct),
            ("renewable_energy", renewablePct)
        };

        foreach (var (type, value) in items)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO carbon_initiatives (area_id, initiative_type, value, effective_date)
                VALUES ($areaId, $type, $value, '2023-01-01')
            """;
            cmd.Parameters.AddWithValue("$areaId", areaId);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedRoleAccounts(SqliteConnection conn)
    {
        var accounts = new (string Email, string Password, string Role)[]
        {
            ("marketing@coke.com",  "Marketing2026!",  "marketing"),
            ("sustain@coke.com",    "Sustain2026!",    "sustainability"),
        };
        foreach (var (email, password, role) in accounts)
        {
            var existsCmd = conn.CreateCommand();
            existsCmd.CommandText = "SELECT COUNT(*) FROM admin_users WHERE email = $email";
            existsCmd.Parameters.AddWithValue("$email", email);
            if ((long)existsCmd.ExecuteScalar()! > 0) continue;

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO admin_users (email, password_hash, role) VALUES ($email, $hash, $role)";
            insertCmd.Parameters.AddWithValue("$email", email);
            insertCmd.Parameters.AddWithValue("$hash", hash);
            insertCmd.Parameters.AddWithValue("$role", role);
            insertCmd.ExecuteNonQuery();
        }
    }

    public static void RecomputeGrades(SqliteConnection conn) => ComputeGrades(conn);

    private static void ComputeGrades(SqliteConnection conn)
    {
        var scoreCmd = conn.CreateCommand();
        scoreCmd.CommandText = """
            SELECT
                a.id,
                a.type,
                COALESCE((SELECT SUM(amount_mtco2e) FROM emissions_data WHERE area_id = a.id AND year = 2023), 0) AS raw_score,
                COALESCE((
                    SELECT SUM(CASE initiative_type
                        WHEN 'rvm'              THEN value * 200
                        WHEN 'ev_fleet'         THEN value * 500
                        WHEN 'renewable_energy' THEN value * 300
                        ELSE 0 END)
                    FROM carbon_initiatives WHERE area_id = a.id
                ), 0) AS initiative_deduction
            FROM areas a
        """;

        var rows = new List<(int Id, string Type, double Raw, double Deduction, double Final)>();
        using (var reader = scoreCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var raw = reader.GetDouble(2);
                var ded = reader.GetDouble(3);
                rows.Add((reader.GetInt32(0), reader.GetString(1), raw, ded, Math.Max(0, raw - ded)));
            }
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        foreach (var type in new[] { "country", "state", "city" })
        {
            var group = rows.Where(r => r.Type == type).OrderBy(r => r.Final).ToList();
            for (int i = 0; i < group.Count; i++)
            {
                var pct = (double)i / group.Count;
                var grade = pct < 0.33 ? "A" : pct < 0.66 ? "B" : "C";
                var row = group[i];

                var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO emission_grades (area_id, raw_score, initiative_deduction, final_score, grade, computed_at)
                    VALUES ($areaId, $raw, $ded, $final, $grade, $now)
                """;
                cmd.Parameters.AddWithValue("$areaId", row.Id);
                cmd.Parameters.AddWithValue("$raw", row.Raw);
                cmd.Parameters.AddWithValue("$ded", row.Deduction);
                cmd.Parameters.AddWithValue("$final", row.Final);
                cmd.Parameters.AddWithValue("$grade", grade);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
