using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,marketing,sustainability")]
public class InsightsController : ControllerBase
{
    private readonly Database _db;
    private readonly GeminiService _gemini;

    public InsightsController(Database db, GeminiService gemini)
    {
        _db = db;
        _gemini = gemini;
    }

    [HttpGet]
    public IActionResult GetInsights([FromQuery] int? areaId)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT i.id, i.insight_text, i.suggestion_type, i.generated_at,
                   a.name as area_name, eg.grade
            FROM ai_insights i
            JOIN areas a ON i.area_id = a.id
            JOIN emission_grades eg ON a.id = eg.area_id
            WHERE ($areaId IS NULL OR i.area_id = $areaId)
            ORDER BY i.generated_at DESC
        """;
        cmd.Parameters.AddWithValue("$areaId", areaId.HasValue ? (object)areaId.Value : DBNull.Value);

        var insights = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            insights.Add(new
            {
                id = reader.GetInt32(0),
                insightText = reader.GetString(1),
                suggestionType = reader.IsDBNull(2) ? null : reader.GetString(2),
                generatedAt = reader.GetString(3),
                areaName = reader.GetString(4),
                grade = reader.GetString(5)
            });
        }

        return Ok(insights);
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest request)
    {
        using var conn = _db.Connect();
        conn.Open();

        // Get area context including sustainability initiatives
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.name, eg.grade, eg.raw_score,
                   COALESCE((SELECT CAST(value AS INTEGER) FROM carbon_initiatives WHERE area_id = a.id AND initiative_type = 'rvm'), 0) as rvm_count,
                   COALESCE((SELECT value FROM carbon_initiatives WHERE area_id = a.id AND initiative_type = 'ev_fleet'), 0) as ev_pct,
                   COALESCE((SELECT value FROM carbon_initiatives WHERE area_id = a.id AND initiative_type = 'renewable_energy'), 0) as re_pct
            FROM areas a
            JOIN emission_grades eg ON a.id = eg.area_id
            WHERE a.id = $areaId
        """;
        cmd.Parameters.AddWithValue("$areaId", request.AreaId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { message = "Area not found or not graded yet." });

        var areaName = reader.GetString(0);
        var grade = reader.GetString(1);
        var totalEmissions = reader.GetDouble(2);
        var rvmCount = reader.GetInt32(3);
        var evPct = reader.GetDouble(4);
        var rePct = reader.GetDouble(5);
        reader.Close();

        // Get bottle context
        var bottleData = GetBottleSummary(request.AreaId, conn);

        var result = await _gemini.QueryWithContext(areaName, grade, totalEmissions, rvmCount, evPct, rePct, bottleData, request.Question);
        return Ok(new { answer = result });
    }

    private string GetBottleSummary(int areaId, SqliteConnection conn)
    {
        // Query scans from this area OR any child/grandchild areas (city within state/country)
        var scanCmd = conn.CreateCommand();
        scanCmd.CommandText = """
            SELECT COUNT(*),
                   SUM(CASE WHEN material_type = 'plastic' THEN 1 ELSE 0 END) as plastic,
                   SUM(CASE WHEN material_type = 'aluminum' THEN 1 ELSE 0 END) as aluminum
            FROM rvm_scans s
            JOIN rvm_machines m ON s.rvm_id = m.id
            JOIN areas a ON m.area_id = a.id
            WHERE a.id = $areaId
               OR a.parent_id = $areaId
               OR a.parent_id IN (SELECT id FROM areas WHERE parent_id = $areaId)
        """;
        scanCmd.Parameters.AddWithValue("$areaId", areaId);

        using var reader = scanCmd.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) == 0) return "No recycling data available for this area yet.";

        var total = reader.GetInt32(0);
        var plastic = reader.GetInt32(1);
        var aluminum = reader.GetInt32(2);
        reader.Close();

        // Top brands
        var brandCmd = conn.CreateCommand();
        brandCmd.CommandText = """
            SELECT brand, COUNT(*) as cnt
            FROM rvm_scans s
            JOIN rvm_machines m ON s.rvm_id = m.id
            JOIN areas a ON m.area_id = a.id
            WHERE (a.id = $areaId
               OR a.parent_id = $areaId
               OR a.parent_id IN (SELECT id FROM areas WHERE parent_id = $areaId))
              AND brand IS NOT NULL
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

    public class QueryRequest
    {
        public int AreaId { get; set; }
        public string Question { get; set; } = string.Empty;
    }
}
