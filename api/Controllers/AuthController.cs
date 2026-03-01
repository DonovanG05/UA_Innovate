using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using api.Data;

namespace api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly Database _db;
    private readonly IConfiguration _config;

    public AuthController(Database db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public record LoginRequest(string Email, string Password);
    public record RegisterRequest(string Email, string Password, string Name, int? Age = null, string? ZipCode = null, string? Gender = null);
    public record AuthResponse(string Token, string Role, string Name, int UserId);

    // POST /api/auth/admin/login
    [HttpPost("admin/login")]
    public IActionResult AdminLogin([FromBody] LoginRequest req)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, password_hash, role FROM admin_users WHERE email = $email";
        cmd.Parameters.AddWithValue("$email", req.Email.Trim().ToLower());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Unauthorized(new { error = "Invalid credentials" });

        var id = reader.GetInt32(0);
        var storedHash = reader.GetString(1);
        var role = reader.GetString(2);

        if (!VerifyPassword(req.Password, storedHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var displayName = role switch {
            "marketing"      => "Marketing",
            "sustainability" => "Sustainability",
            _                => "Admin"
        };

        var token = GenerateToken(id, req.Email, displayName, role);
        return Ok(new AuthResponse(token, role, displayName, id));
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Email, password, and name are required" });

        using var conn = _db.Connect();
        conn.Open();

        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM users WHERE email = $email";
        checkCmd.Parameters.AddWithValue("$email", req.Email.Trim().ToLower());
        if ((long)checkCmd.ExecuteScalar()! > 0)
            return Conflict(new { error = "Email already registered" });

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var qrId = Guid.NewGuid().ToString("N");
        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO users (email, password_hash, name, created_at, total_points, age, zip_code, gender, qr_identifier)
            VALUES ($email, $hash, $name, $now, 0, $age, $zip, $gender, $qrId)
        """;
        insertCmd.Parameters.AddWithValue("$email", req.Email.Trim().ToLower());
        insertCmd.Parameters.AddWithValue("$hash", HashPasswordPbkdf2(req.Password));
        insertCmd.Parameters.AddWithValue("$name", req.Name.Trim());
        insertCmd.Parameters.AddWithValue("$now", now);
        insertCmd.Parameters.AddWithValue("$age", req.Age.HasValue ? (object)req.Age.Value : DBNull.Value);
        insertCmd.Parameters.AddWithValue("$zip", req.ZipCode != null ? (object)req.ZipCode.Trim() : DBNull.Value);
        insertCmd.Parameters.AddWithValue("$gender", req.Gender != null ? (object)req.Gender.Trim() : DBNull.Value);
        insertCmd.Parameters.AddWithValue("$qrId", qrId);
        insertCmd.ExecuteNonQuery();

        var idCmd = conn.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        var id = (int)(long)idCmd.ExecuteScalar()!;

        var token = GenerateToken(id, req.Email, req.Name.Trim(), "user");
        return Ok(new AuthResponse(token, "user", req.Name.Trim(), id));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        using var conn = _db.Connect();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, password_hash FROM users WHERE email = $email";
        cmd.Parameters.AddWithValue("$email", req.Email.Trim().ToLower());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Unauthorized(new { error = "Invalid credentials" });

        var id = reader.GetInt32(0);
        var name = reader.GetString(1);
        var storedHash = reader.GetString(2);

        if (!VerifyPassword(req.Password, storedHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var token = GenerateToken(id, req.Email, name, "user");
        return Ok(new AuthResponse(token, "user", name, id));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    internal string GenerateToken(int id, string email, string name, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var expiry = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 8;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", name),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, id.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiry),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Legacy SHA256 hash (used for seeded admin accounts)
    internal static string HashPassword(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();

    // New PBKDF2 hash for new registrations
    internal static string HashPasswordPbkdf2(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2:{Convert.ToHexString(salt).ToLower()}:{Convert.ToHexString(hash).ToLower()}";
    }

    internal static bool VerifyPassword(string password, string storedHash)
    {
        if (storedHash.StartsWith("pbkdf2:"))
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 3) return false;
            var salt = Convert.FromHexString(parts[1]);
            var expectedHash = Convert.FromHexString(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        // Fallback: legacy SHA256
        return storedHash == HashPassword(password);
    }
}
