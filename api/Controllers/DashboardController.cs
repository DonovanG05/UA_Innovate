using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using api.Data;

namespace api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "admin,marketing,sustainability")]
public class DashboardController : ControllerBase
{
    private readonly Database _db;
    public DashboardController(Database db) => _db = db;

    // GET /api/dashboard/summary
    [HttpGet("summary")]
    public IActionResult Summary()
    {
        using var conn = _db.Connect();
        conn.Open();

        // Total emissions from country-level areas (year 2023). SQLite SUM returns real, not long.
        var totalCmd = conn.CreateCommand();
        totalCmd.CommandText = """
            SELECT CAST(COALESCE(SUM(ed.amount_mtco2e), 0) AS REAL)
            FROM emissions_data ed
            JOIN areas a ON a.id = ed.area_id
            WHERE a.type = 'country' AND ed.year = 2023
        """;
        var totalObj = totalCmd.ExecuteScalar();
        var totalEmissions = (totalObj == null || totalObj == DBNull.Value) ? 0d : Convert.ToDouble(totalObj);

        // Grade counts for states
        var gradeCmd = conn.CreateCommand();
        gradeCmd.CommandText = """
            SELECT eg.grade, COUNT(*) as cnt
            FROM emission_grades eg
            JOIN areas a ON a.id = eg.area_id
            WHERE a.type = 'state'
            GROUP BY eg.grade
        """;
        var grades = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0 };
        using (var r = gradeCmd.ExecuteReader())
            while (r.Read()) grades[r.GetString(0)] = (int)r.GetInt64(1);

        // Total RVM scans ever
        var scansCmd = conn.CreateCommand();
        scansCmd.CommandText = "SELECT COUNT(*) FROM rvm_scans";
        var totalScans = (long)scansCmd.ExecuteScalar()!;

        // Top 5 worst states (highest final_score = worst)
        var worstCmd = conn.CreateCommand();
        worstCmd.CommandText = """
            SELECT a.id, a.name, eg.grade, CAST(eg.raw_score AS REAL), CAST(eg.final_score AS REAL)
            FROM emission_grades eg
            JOIN areas a ON a.id = eg.area_id
            WHERE a.type = 'state'
            ORDER BY eg.final_score DESC
            LIMIT 5
        """;
        var worst = new List<object>();
        using (var r = worstCmd.ExecuteReader())
            while (r.Read())
                worst.Add(new { id = r.GetInt32(0), name = r.GetString(1), grade = r.GetString(2), rawScore = r.GetDouble(3), finalScore = r.GetDouble(4) });

        // Active RVM machine count
        var rvmCmd = conn.CreateCommand();
        rvmCmd.CommandText = "SELECT COUNT(*) FROM rvm_machines WHERE active = 1";
        var rvmCount = (long)rvmCmd.ExecuteScalar()!;

        return Ok(new
        {
            totalEmissionsMtco2e = totalEmissions,
            gradeA = grades["A"],
            gradeB = grades["B"],
            gradeC = grades["C"],
            totalRvmScans = totalScans,
            activeRvms = rvmCount,
            worstAreas = worst
        });
    }
}
