using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using System.Security.Claims;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RvmController : ControllerBase
{
    private readonly Database _db;

    public RvmController(Database db)
    {
        _db = db;
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
            // 1. Record the scan
            var scanCmd = conn.CreateCommand();
            scanCmd.CommandText = """
                INSERT INTO rvm_scans (rvm_id, user_id, product_barcode, scanned_at, points_awarded, material_type, brand)
                VALUES ($rvmId, $userId, $barcode, $now, 2, $material, $brand)
            """;
            scanCmd.Parameters.AddWithValue("$rvmId", request.RvmId);
            scanCmd.Parameters.AddWithValue("$userId", userId);
            scanCmd.Parameters.AddWithValue("$barcode", request.Barcode ?? (object)DBNull.Value);
            scanCmd.Parameters.AddWithValue("$material", request.MaterialType ?? (object)DBNull.Value);
            scanCmd.Parameters.AddWithValue("$brand", request.Brand ?? (object)DBNull.Value);
            scanCmd.ExecuteNonQuery();

            // 2. Award points to user
            var userCmd = conn.CreateCommand();
            userCmd.CommandText = "UPDATE users SET total_points = total_points + 2 WHERE id = $userId";
            userCmd.Parameters.AddWithValue("$userId", userId);
            userCmd.ExecuteNonQuery();

            // 3. Log reward history
            var historyCmd = conn.CreateCommand();
            historyCmd.CommandText = """
                INSERT INTO user_rewards (user_id, type, points, description, created_at)
                VALUES ($userId, 'earn', 2, 'RVM Bottle Scan', $now)
            """;
            historyCmd.Parameters.AddWithValue("$userId", userId);
            historyCmd.Parameters.AddWithValue("$now", now);
            historyCmd.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { message = "Scan successful! +2 points awarded.", points = 2 });
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
    }
}
