using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using System.Security.Claims;
using System.Text.Json;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RvmController : ControllerBase
{
    private readonly Database _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public RvmController(Database db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult GetRvms([FromQuery] int? areaId)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.id, m.location_name, m.latitude, m.longitude, m.active,
                   (SELECT COUNT(*) FROM rvm_scans WHERE rvm_id = m.id) as scan_count
            FROM rvm_machines m
            WHERE ($areaId IS NULL OR m.area_id = $areaId)
        """;
        cmd.Parameters.AddWithValue("$areaId", areaId.HasValue ? (object)areaId.Value : DBNull.Value);

        var rvms = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rvms.Add(new
            {
                id = reader.GetInt32(0),
                locationName = reader.GetString(1),
                latitude = reader.GetDouble(2),
                longitude = reader.GetDouble(3),
                active = reader.GetInt32(4) == 1,
                scanCount = reader.GetInt32(5)
            });
        }

        return Ok(rvms);
    }

    [HttpGet("nearest")]
    [Authorize]
    public async Task<IActionResult> GetNearestRvm()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        string? zipCode = null;
        using (var conn = _db.Connect())
        {
            conn.Open();
            var userCmd = conn.CreateCommand();
            userCmd.CommandText = "SELECT zip_code FROM users WHERE id = $userId";
            userCmd.Parameters.AddWithValue("$userId", userId);
            var z = userCmd.ExecuteScalar();
            if (z != null && z != DBNull.Value) zipCode = z.ToString()?.Trim();
        }

        if (string.IsNullOrEmpty(zipCode))
        {
            using var conn = _db.Connect();
            conn.Open();
            var firstCmd = conn.CreateCommand();
            firstCmd.CommandText = "SELECT id, location_name, latitude, longitude FROM rvm_machines WHERE active = 1 ORDER BY id LIMIT 1";
            using var r = firstCmd.ExecuteReader();
            if (r.Read())
                return Ok(new { locationName = r.GetString(1), distanceMi = (double?)null, message = "Set your zip code in profile to see distance." });
            return Ok(new { message = "No RVMs found. Set your zip code in profile." });
        }

        double userLat, userLon;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var res = await client.GetAsync($"https://api.zippopotam.us/us/{zipCode}");
            if (!res.IsSuccessStatusCode) throw new Exception("Geocode failed");
            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var places = doc.RootElement.GetProperty("places");
            if (places.GetArrayLength() == 0) throw new Exception("No place");
            var place = places[0];
            userLat = place.GetProperty("latitude").GetDouble();
            userLon = place.GetProperty("longitude").GetDouble();
        }
        catch
        {
            using var conn = _db.Connect();
            conn.Open();
            var firstCmd = conn.CreateCommand();
            firstCmd.CommandText = "SELECT id, location_name, latitude, longitude FROM rvm_machines WHERE active = 1 ORDER BY id LIMIT 1";
            using var r = firstCmd.ExecuteReader();
            if (r.Read())
                return Ok(new { locationName = r.GetString(1), distanceMi = (double?)null, message = "Using your zip. Distance unavailable." });
            return Ok(new { message = "No RVMs found." });
        }

        var rvms = new List<(int id, string name, double lat, double lon)>();
        using (var conn = _db.Connect())
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, location_name, latitude, longitude FROM rvm_machines WHERE active = 1";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                rvms.Add((reader.GetInt32(0), reader.GetString(1), reader.GetDouble(2), reader.GetDouble(3)));
        }

        if (rvms.Count == 0)
            return Ok(new { message = "No RVMs found." });

        double minDist = double.MaxValue;
        string? nearestName = null;
        int nearestId = 0;
        foreach (var r in rvms)
        {
            var d = HaversineMi(userLat, userLon, r.lat, r.lon);
            if (d < minDist) { minDist = d; nearestName = r.name; nearestId = r.id; }
        }

        return Ok(new { id = nearestId, locationName = nearestName, distanceMi = Math.Round(minDist, 1), zipCode });
    }

    private static double HaversineMi(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3959; // Earth radius miles
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    [HttpGet("scans/stats")]
    [Authorize(Roles = "admin")]
    public IActionResult GetScanStats()
    {
        using var conn = _db.Connect();
        conn.Open();

        // Totals
        var totalCmd = conn.CreateCommand();
        totalCmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(points_awarded),0) FROM rvm_scans";
        using var tr = totalCmd.ExecuteReader();
        tr.Read();
        var totalScans = tr.GetInt32(0);
        var totalPoints = tr.GetInt32(1);
        tr.Close();

        // By material
        var matCmd = conn.CreateCommand();
        matCmd.CommandText = """
            SELECT COALESCE(material_type,'unknown'), COUNT(*)
            FROM rvm_scans GROUP BY material_type
        """;
        var byMaterial = new Dictionary<string, int>();
        using var mr = matCmd.ExecuteReader();
        while (mr.Read()) byMaterial[mr.GetString(0)] = mr.GetInt32(1);
        mr.Close();

        // By brand
        var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = """
            SELECT COALESCE(brand,'unknown'), COUNT(*)
            FROM rvm_scans GROUP BY brand ORDER BY COUNT(*) DESC
        """;
        var byBrand = new List<object>();
        using var br = brandCmd.ExecuteReader();
        while (br.Read()) byBrand.Add(new { brand = br.GetString(0), count = br.GetInt32(1) });
        br.Close();

        // By RVM location (top 10)
        var locCmd = conn.CreateCommand();
        locCmd.CommandText = """
            SELECT m.location_name, COUNT(*) as cnt
            FROM rvm_scans s JOIN rvm_machines m ON s.rvm_id = m.id
            GROUP BY m.location_name ORDER BY cnt DESC LIMIT 10
        """;
        var byLocation = new List<object>();
        using var lr = locCmd.ExecuteReader();
        while (lr.Read()) byLocation.Add(new { location = lr.GetString(0), count = lr.GetInt32(1) });
        lr.Close();

        // Recent 20 scans
        var recentCmd = conn.CreateCommand();
        recentCmd.CommandText = """
            SELECT s.scanned_at, m.location_name,
                   COALESCE(s.material_type,'—'), COALESCE(s.brand,'—'), s.points_awarded
            FROM rvm_scans s JOIN rvm_machines m ON s.rvm_id = m.id
            ORDER BY s.scanned_at DESC LIMIT 20
        """;
        var recent = new List<object>();
        using var rr = recentCmd.ExecuteReader();
        while (rr.Read())
        {
            recent.Add(new
            {
                scannedAt = rr.GetString(0),
                location = rr.GetString(1),
                material = rr.GetString(2),
                brand = rr.GetString(3),
                points = rr.GetInt32(4)
            });
        }

        return Ok(new { totalScans, totalPoints, byMaterial, byBrand, byLocation, recentScans = recent });
    }

    [HttpGet("impact")]
    [Authorize(Roles = "admin")]
    public IActionResult GetImpact([FromQuery] int areaId, [FromQuery] int rvmCount)
    {
        using var conn = _db.Connect();
        conn.Open();

        // Current emissions for 2023
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SUM(amount_mtco2e) FROM emissions_data WHERE area_id = $areaId AND year = 2023";
        cmd.Parameters.AddWithValue("$areaId", areaId);
        var currentEmissions = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);

        // Realistic formula: 500 items/day × 365 days × ~100g CO2e/item = ~18 MTCO2e/year
        // Rounded conservatively to 15 MTCO2e per RVM per year.
        double reductionPerRvm = 15.0;
        double projectedReduction = rvmCount * reductionPerRvm;
        double projectedEmissions = Math.Max(0, currentEmissions - projectedReduction);
        double percentReduction = currentEmissions > 0 ? (projectedReduction / currentEmissions) * 100 : 0;

        return Ok(new
        {
            areaId,
            currentEmissions,
            projectedEmissions,
            projectedReduction,
            percentReduction = Math.Round(percentReduction, 2)
        });
    }

    [HttpPost("scan")]
    [Authorize]
    public IActionResult Scan([FromBody] ScanRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = _db.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var pointsToAward = request.SizeOz == 20 ? 2 : 1;
            // 1. Record the scan
            var scanCmd = conn.CreateCommand();
            scanCmd.CommandText = """
                INSERT INTO rvm_scans (rvm_id, user_id, product_barcode, scanned_at, points_awarded, material_type, brand)
                VALUES ($rvmId, $userId, $barcode, $now, $points, $material, $brand)
            """;
            scanCmd.Parameters.AddWithValue("$rvmId", request.RvmId);
            scanCmd.Parameters.AddWithValue("$userId", userId);
            scanCmd.Parameters.AddWithValue("$barcode", request.Barcode ?? (object)DBNull.Value);
            scanCmd.Parameters.AddWithValue("$material", request.MaterialType ?? (object)DBNull.Value);
            scanCmd.Parameters.AddWithValue("$brand", request.Brand ?? (object)DBNull.Value);
            scanCmd.Parameters.AddWithValue("$points", pointsToAward);
            scanCmd.Parameters.AddWithValue("$now", now);
            scanCmd.ExecuteNonQuery();

            // 2. Award points to user
            var userCmd = conn.CreateCommand();
            userCmd.CommandText = "UPDATE users SET total_points = total_points + $points WHERE id = $userId";
            userCmd.Parameters.AddWithValue("$userId", userId);
            userCmd.Parameters.AddWithValue("$points", pointsToAward);
            userCmd.ExecuteNonQuery();

            // 3. Log reward history
            var historyCmd = conn.CreateCommand();
            historyCmd.CommandText = """
                INSERT INTO user_rewards (user_id, type, points, description, created_at)
                VALUES ($userId, 'earn', $points, 'RVM Bottle Scan', $now)
            """;
            historyCmd.Parameters.AddWithValue("$userId", userId);
            historyCmd.Parameters.AddWithValue("$points", pointsToAward);
            historyCmd.Parameters.AddWithValue("$now", now);
            historyCmd.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { message = $"Scan successful! +{pointsToAward} point(s) awarded.", points = pointsToAward });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return BadRequest(new { message = "Failed to record scan.", error = ex.Message });
        }
    }

    public class ScanRequest
    {
        public int RvmId { get; set; }
        public string? Barcode { get; set; }
        public string? MaterialType { get; set; }
        public string? Brand { get; set; }
        /// <summary>12 = 1 point, 20 = 2 points. Default 12 if not set.</summary>
        public int SizeOz { get; set; } = 12;
    }
}
