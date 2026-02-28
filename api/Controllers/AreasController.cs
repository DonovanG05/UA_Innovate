using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreasController : ControllerBase
{
    private readonly Database _db;
    public AreasController(Database db) => _db = db;

    [HttpGet]
    public IActionResult GetAreas([FromQuery] string? type)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, type FROM areas WHERE ($type IS NULL OR type = $type) ORDER BY name";
        cmd.Parameters.AddWithValue("$type", type ?? (object)DBNull.Value);

        var results = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                id = reader.GetInt32(0),
                name = reader.GetString(1),
                type = reader.GetString(2)
            });
        }
        return Ok(results);
    }
}
