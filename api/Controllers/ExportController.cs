using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using api.Data;

namespace api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize(Roles = "admin")]
public class ExportController : ControllerBase
{
    private readonly Database _db;

    public ExportController(Database db)
    {
        _db = db;
    }

    [HttpGet("users")]
    public IActionResult ExportUsers()
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.email, u.name, u.age, u.zip_code, u.total_points,
                   COUNT(s.id) as scan_count, u.created_at
            FROM users u
            LEFT JOIN rvm_scans s ON s.user_id = u.id
            GROUP BY u.id
            ORDER BY u.total_points DESC
        """;

        var sb = new StringBuilder();
        sb.AppendLine("id,email,name,age,zip_code,total_points,scan_count,created_at");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var email = CsvEscape(reader.GetString(1));
            var name = CsvEscape(reader.GetString(2));
            var age = reader.IsDBNull(3) ? "" : reader.GetInt32(3).ToString();
            var zip = reader.IsDBNull(4) ? "" : CsvEscape(reader.GetString(4));
            var points = reader.GetInt32(5);
            var scans = reader.GetInt32(6);
            var created = reader.GetString(7);
            sb.AppendLine($"{id},{email},{name},{age},{zip},{points},{scans},{created}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "users.csv");
    }

    [HttpGet("scans")]
    public IActionResult ExportScans()
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.user_id, m.location_name, s.product_barcode,
                   s.material_type, s.brand, s.points_awarded, s.scanned_at
            FROM rvm_scans s
            JOIN rvm_machines m ON s.rvm_id = m.id
            ORDER BY s.scanned_at DESC
        """;

        var sb = new StringBuilder();
        sb.AppendLine("id,user_id,rvm_location,barcode,material_type,brand,points_awarded,scanned_at");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var userId = reader.IsDBNull(1) ? "" : reader.GetInt32(1).ToString();
            var loc = CsvEscape(reader.GetString(2));
            var barcode = reader.IsDBNull(3) ? "" : CsvEscape(reader.GetString(3));
            var material = reader.IsDBNull(4) ? "" : CsvEscape(reader.GetString(4));
            var brand = reader.IsDBNull(5) ? "" : CsvEscape(reader.GetString(5));
            var pts = reader.GetInt32(6);
            var scannedAt = reader.GetString(7);
            sb.AppendLine($"{id},{userId},{loc},{barcode},{material},{brand},{pts},{scannedAt}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "scans.csv");
    }

    [HttpGet("emissions")]
    public IActionResult ExportEmissions()
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.name, a.type, e.category, e.amount_mtco2e, e.year, e.month
            FROM emissions_data e
            JOIN areas a ON e.area_id = a.id
            ORDER BY a.name, e.year, e.category
        """;

        var sb = new StringBuilder();
        sb.AppendLine("area,type,category,amount_mtco2e,year,month");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var area = CsvEscape(reader.GetString(0));
            var type = reader.GetString(1);
            var cat = reader.GetString(2);
            var amount = reader.GetDouble(3);
            var year = reader.GetInt32(4);
            var month = reader.IsDBNull(5) ? "" : reader.GetInt32(5).ToString();
            sb.AppendLine($"{area},{type},{cat},{amount},{year},{month}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "emissions.csv");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
