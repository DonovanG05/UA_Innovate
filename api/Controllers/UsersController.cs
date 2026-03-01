using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using api.Data;
using System.Security.Claims;
using System.Linq;
using System.Text.Json.Serialization;

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
    [Authorize(Roles = "admin,marketing")]
    public IActionResult GetAllUsers([FromQuery] string? sortBy, [FromQuery] string? zipFilter)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.email, u.name, u.age, u.zip_code, u.gender, u.total_points,
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
            var gender = reader.IsDBNull(5) ? null : reader.GetString(5);
            var points = reader.GetInt32(6);
            var scans = reader.GetInt32(7);
            var favLoc = reader.IsDBNull(8) ? "N/A" : reader.GetString(8);

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
                gender,
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

    [HttpGet("leaderboard")]
    public IActionResult GetLeaderboard()
    {
        using var conn = _db.Connect();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.name, COALESCE(COUNT(s.id), 0) AS bottle_count
            FROM users u
            LEFT JOIN rvm_scans s ON s.user_id = u.id
            GROUP BY u.id, u.name
            ORDER BY bottle_count DESC, u.name ASC
            LIMIT 50
            """;
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        var rank = 1;
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var bottleCount = reader.GetInt32(1);
            var firstName = name.Split(' ')[0];
            list.Add(new { rank = rank++, firstName, scanCount = bottleCount });
        }
        return Ok(list);
    }

    [HttpGet("me")]
    public IActionResult GetMe()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, name, total_points, created_at, address, qr_identifier, gender, zip_code FROM users WHERE id = $userId";
        cmd.Parameters.AddWithValue("$userId", userId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { message = "User not found" });

        var id = reader.GetInt32(0);
        var email = reader.GetString(1);
        var name = reader.GetString(2);
        var totalPoints = reader.GetInt32(3);
        var createdAt = reader.GetString(4);
        var address = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : null;
        var qrIdentifier = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetString(6) : null;
        var gender = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null;
        var zipCode = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : null;
        reader.Close();

        if (string.IsNullOrEmpty(qrIdentifier))
        {
            qrIdentifier = Guid.NewGuid().ToString("N");
            var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE users SET qr_identifier = $qrId WHERE id = $userId";
            updateCmd.Parameters.AddWithValue("$qrId", qrIdentifier);
            updateCmd.Parameters.AddWithValue("$userId", userId);
            updateCmd.ExecuteNonQuery();
        }

        return Ok(new
        {
            id,
            email,
            name,
            totalPoints,
            createdAt,
            address,
            qrIdentifier,
            gender,
            zipCode
        });
    }

    [HttpPatch("me")]
    public IActionResult UpdateMe([FromBody] UpdateMeRequest request)
    {
        if (request == null) return BadRequest(new { message = "Body required." });
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        using var conn = _db.Connect();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET address = $address, gender = $gender, zip_code = $zipCode WHERE id = $userId";
        cmd.Parameters.AddWithValue("$address", (object?)request.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$gender", (object?)request.Gender ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$zipCode", (object?)request.ZipCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$userId", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Profile saved.", address = request.Address, gender = request.Gender, zipCode = request.ZipCode });
    }

    public class UpdateMeRequest
    {
        [JsonPropertyName("address")]
        public string? Address { get; set; }
        [JsonPropertyName("gender")]
        public string? Gender { get; set; }
        [JsonPropertyName("zipCode")]
        public string? ZipCode { get; set; }
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
    public IActionResult Redeem([FromBody] RedeemRequest? request = null)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = _db.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT total_points FROM users WHERE id = $userId";
            checkCmd.Parameters.AddWithValue("$userId", userId);
            var balance = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);

            int pointsToDeduct;
            string description;
            string code;

            if (request?.RewardId != null && request.Points > 0)
            {
                var allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["keychain"] = 6, ["polarbear"] = 8, ["mug"] = 12, ["mousepad"] = 15,
                    ["tote"] = 24, ["dietcokecap"] = 28, ["tshirt"] = 35, ["sweatshirt"] = 40,
                    ["pocketwatch"] = 45, ["cooler"] = 120
                };
                if (!allowed.TryGetValue(request.RewardId, out var required) || required != request.Points)
                    return BadRequest(new { message = "Invalid reward or points." });
                if (balance < required)
                    return BadRequest(new { message = $"Insufficient points. {required} points required for this reward." });
                pointsToDeduct = required;
                description = request.RewardId switch
                {
                    "keychain" => "Key chain",
                    "polarbear" => "Polar Bear",
                    "mug" => "Coca-Cola Mug",
                    "mousepad" => "Mouse pad",
                    "tote" => "Tote bag",
                    "dietcokecap" => "Diet Coke Cap",
                    "tshirt" => "Coca-Cola T-shirt",
                    "sweatshirt" => "Sweatshirt",
                    "pocketwatch" => "Pocket Watch",
                    "cooler" => "Coca-Cola Cooler",
                    _ => request.RewardId
                };
                code = "MERCH-" + request.RewardId.ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            }
            else
            {
                pointsToDeduct = 100;
                if (balance < 100) return BadRequest(new { message = "Insufficient points. 100 points required for a coupon." });
                description = "Free 355mL Coca-Cola Product";
                code = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            }

            var couponCmd = conn.CreateCommand();
            couponCmd.CommandText = """
                INSERT INTO coupons (user_id, code, discount_description, issued_at)
                VALUES ($userId, $code, $desc, $now)
            """;
            couponCmd.Parameters.AddWithValue("$userId", userId);
            couponCmd.Parameters.AddWithValue("$code", code);
            couponCmd.Parameters.AddWithValue("$desc", description);
            couponCmd.Parameters.AddWithValue("$now", now);
            couponCmd.ExecuteNonQuery();

            var deductCmd = conn.CreateCommand();
            deductCmd.CommandText = "UPDATE users SET total_points = total_points - $points WHERE id = $userId";
            deductCmd.Parameters.AddWithValue("$userId", userId);
            deductCmd.Parameters.AddWithValue("$points", pointsToDeduct);
            deductCmd.ExecuteNonQuery();

            var historyCmd = conn.CreateCommand();
            historyCmd.CommandText = """
                INSERT INTO user_rewards (user_id, type, points, description, created_at)
                VALUES ($userId, 'redeem', $points, 'Redeemed: ' || $desc, $now)
            """;
            historyCmd.Parameters.AddWithValue("$userId", userId);
            historyCmd.Parameters.AddWithValue("$points", pointsToDeduct);
            historyCmd.Parameters.AddWithValue("$desc", description);
            historyCmd.Parameters.AddWithValue("$now", now);
            historyCmd.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { message = "Reward redeemed successfully!", code, description });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return BadRequest(new { message = "Redemption failed.", error = ex.Message });
        }
    }

    public class RedeemRequest
    {
        public string? RewardId { get; set; }
        public int Points { get; set; }
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
