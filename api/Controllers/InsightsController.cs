using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
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

        // Get area context
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.name, eg.grade, eg.raw_score
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

        var result = await _gemini.QueryWithContext(areaName, grade, totalEmissions, request.Question);
        return Ok(new { answer = result });
    }

    public class QueryRequest
    {
        public int AreaId { get; set; }
        public string Question { get; set; } = string.Empty;
    }
}
