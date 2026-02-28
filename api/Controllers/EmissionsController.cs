using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using api.Data;

namespace api.Controllers;

[ApiController]
[Route("api/emissions")]
[Authorize(Roles = "admin")]
public class EmissionsController : ControllerBase
{
    private readonly Database _db;
    public EmissionsController(Database db) => _db = db;

    // GET /api/emissions/grades?type=state
    [HttpGet("grades")]
    public IActionResult GetGrades([FromQuery] string type = "state")
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                a.id, a.name, a.type, a.latitude, a.longitude,
                eg.grade,
                CAST(eg.raw_score AS REAL)           AS raw_score,
                CAST(eg.initiative_deduction AS REAL) AS deduction,
                CAST(eg.final_score AS REAL)          AS final_score,
                CAST(COALESCE((SELECT amount_mtco2e FROM emissions_data WHERE area_id=a.id AND year=2023 AND category='trucking'),0) AS REAL)      AS trucking,
                CAST(COALESCE((SELECT amount_mtco2e FROM emissions_data WHERE area_id=a.id AND year=2023 AND category='factory'),0) AS REAL)       AS factory,
                CAST(COALESCE((SELECT amount_mtco2e FROM emissions_data WHERE area_id=a.id AND year=2023 AND category='energy'),0) AS REAL)        AS energy,
                CAST(COALESCE((SELECT amount_mtco2e FROM emissions_data WHERE area_id=a.id AND year=2023 AND category='refrigeration'),0) AS REAL) AS refrigeration,
                CAST(COALESCE((SELECT amount_mtco2e FROM emissions_data WHERE area_id=a.id AND year=2023 AND category='packaging'),0) AS REAL)     AS packaging,
                CAST(COALESCE((SELECT value FROM carbon_initiatives WHERE area_id=a.id AND initiative_type='rvm'),0) AS REAL)              AS rvm_count,
                CAST(COALESCE((SELECT value FROM carbon_initiatives WHERE area_id=a.id AND initiative_type='ev_fleet'),0) AS REAL)         AS ev_pct,
                CAST(COALESCE((SELECT value FROM carbon_initiatives WHERE area_id=a.id AND initiative_type='renewable_energy'),0) AS REAL) AS renewable_pct
            FROM areas a
            JOIN emission_grades eg ON eg.area_id = a.id
            WHERE a.type = $type
            ORDER BY eg.raw_score DESC
        """;
        cmd.Parameters.AddWithValue("$type", type);

        var results = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                id            = reader.GetInt32(0),
                name          = reader.GetString(1),
                type          = reader.GetString(2),
                latitude      = reader.GetDouble(3),
                longitude     = reader.GetDouble(4),
                grade         = reader.GetString(5),
                rawScore      = reader.GetDouble(6),
                deduction     = reader.GetDouble(7),
                finalScore    = reader.GetDouble(8),
                trucking      = reader.GetDouble(9),
                factory       = reader.GetDouble(10),
                energy        = reader.GetDouble(11),
                refrigeration = reader.GetDouble(12),
                packaging     = reader.GetDouble(13),
                rvmCount      = reader.GetDouble(14),
                evPct         = reader.GetDouble(15),
                renewablePct  = reader.GetDouble(16)
            });
        }
        return Ok(results);
    }

    // POST /api/emissions/recompute-grades  (re-runs grading formula)
    [HttpPost("recompute-grades")]
    public IActionResult RecomputeGrades()
    {
        using var conn = _db.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM emission_grades";
        del.ExecuteNonQuery();

        Seeder.RecomputeGrades(conn);
        tx.Commit();
        return Ok(new { message = "Grades recomputed" });
    }
}
