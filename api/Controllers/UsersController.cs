using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using System.Security.Claims;
using System.Linq;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly Database _db;

    public UsersController(Database db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public IActionResult GetAllUsers([FromQuery] string? sortBy, [FromQuery] string? zipFilter)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.email, u.name, u.age, u.zip_code, u.total_points,
                   COUNT(s.id) as scan_count,
                   (SELECT m.location_name FROM rvm_scans s2
                    JOIN rvm_machines m ON s2.rvm_id = m.id
                    WHERE s2.user_id = u.id
                    GROUP BY m.location_name ORDER BY COUNT(*) DESC LIMIT 1) as fav_location
            FROM users u
            LEFT JOIN rvm_scans s ON s.user_id = u.id
            GROUP BY u.id
        """;

        var users = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var email = reader.GetString(1);
            var name = reader.GetString(2);
            var age = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
            var zip = reader.IsDBNull(4) ? null : reader.GetString(4);
            var points = reader.GetInt32(5);
            var scans = reader.GetInt32(6);
            var favLoc = reader.IsDBNull(7) ? "N/A" : reader.GetString(7);

            // Mask email: j***@example.com
            var atIdx = email.IndexOf('@');
            var maskedEmail = atIdx > 0
                ? email[0] + "***" + email[atIdx..]
                : "***";

            // First name only
            var firstName = name.Split(' ')[0];

            // Mask zip: 3**** (first char only)
            var maskedZip = zip != null && zip.Length > 0 ? zip[0] + new string('*', zip.Length - 1) : null;

            // Tier
            var tier = scans >= 50 ? "Platinum" : scans >= 20 ? "Gold" : scans >= 5 ? "Silver" : "Bronze";

            users.Add(new
            {
                id,
                maskedEmail,
                firstName,
                age,
                maskedZip,
                totalPoints = points,
                scanCount = scans,
                favoriteRvmLocation = favLoc,
                tier
            });
        }

        // Apply zip filter
        if (!string.IsNullOrEmpty(zipFilter))
            users = users.Where(u => ((dynamic)u).maskedZip?.ToString()?.StartsWith(zipFilter[0].ToString()) == true).ToList();

        // Sort
        users = sortBy switch
        {
            "points" => users.OrderByDescending(u => ((dynamic)u).totalPoints).ToList<object>(),
            "scans" => users.OrderByDescending(u => ((dynamic)u).scanCount).ToList<object>(),
            "tier" => users.OrderBy(u => ((dynamic)u).tier switch { "Platinum" => 0, "Gold" => 1, "Silver" => 2, _ => 3 }).ToList<object>(),
            _ => users
        };

        return Ok(users);
    }

    [HttpGet("me")]
    public IActionResult GetMe()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, name, total_points, created_at FROM users WHERE id = $userId";
        cmd.Parameters.AddWithValue("$userId", userId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { message = "User not found" });

        return Ok(new
        {
            id = reader.GetInt32(0),
            email = reader.GetString(1),
            name = reader.GetString(2),
            totalPoints = reader.GetInt32(3),
            createdAt = reader.GetString(4)
        });
    }

    [HttpGet("history")]
    public IActionResult GetHistory()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT type, points, description, created_at
            FROM user_rewards
            WHERE user_id = $userId
            ORDER BY created_at DESC
        """;
        cmd.Parameters.AddWithValue("$userId", userId);

        var history = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            history.Add(new
            {
                type = reader.GetString(0),
                points = reader.GetInt32(1),
                description = reader.GetString(2),
                createdAt = reader.GetString(3)
            });
        }

        return Ok(history);
    }

    [HttpPost("redeem")]
    public IActionResult Redeem()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = _db.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            // 1. Check points
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT total_points FROM users WHERE id = $userId";
            checkCmd.Parameters.AddWithValue("$userId", userId);
            var points = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);

            if (points < 100) return BadRequest(new { message = "Insufficient points. 100 points required for a coupon." });

            // 2. Generate coupon
            var code = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var couponCmd = conn.CreateCommand();
            couponCmd.CommandText = """
                INSERT INTO coupons (user_id, code, discount_description, issued_at)
                VALUES ($userId, $code, 'Free 355mL Coca-Cola Product', $now)
            """;
            couponCmd.Parameters.AddWithValue("$userId", userId);
            couponCmd.Parameters.AddWithValue("$code", code);
            couponCmd.Parameters.AddWithValue("$now", now);
            couponCmd.ExecuteNonQuery();

            // 3. Deduct points
            var deductCmd = conn.CreateCommand();
            deductCmd.CommandText = "UPDATE users SET total_points = total_points - 100 WHERE id = $userId";
            deductCmd.Parameters.AddWithValue("$userId", userId);
            deductCmd.ExecuteNonQuery();

            // 4. Log reward history
            var historyCmd = conn.CreateCommand();
            historyCmd.CommandText = """
                INSERT INTO user_rewards (user_id, type, points, description, created_at)
                VALUES ($userId, 'redeem', 100, 'Redeemed for Coupon: ' || $code, $now)
            """;
            historyCmd.Parameters.AddWithValue("$userId", userId);
            historyCmd.Parameters.AddWithValue("$code", code);
            historyCmd.Parameters.AddWithValue("$now", now);
            historyCmd.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { message = "Reward redeemed successfully!", code, description = "Free 355mL Coca-Cola Product" });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return BadRequest(new { message = "Redemption failed.", error = ex.Message });
        }
    }

    [HttpGet("coupons")]
    public IActionResult GetCoupons()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT code, discount_description, issued_at, redeemed_at FROM coupons WHERE user_id = $userId ORDER BY issued_at DESC";
        cmd.Parameters.AddWithValue("$userId", userId);

        var coupons = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            coupons.Add(new
            {
                code = reader.GetString(0),
                description = reader.GetString(1),
                issuedAt = reader.GetString(2),
                redeemedAt = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return Ok(coupons);
    }
}
