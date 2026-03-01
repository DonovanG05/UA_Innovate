using Microsoft.Data.Sqlite;

namespace api.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    public SqliteConnection Connect() => new SqliteConnection(_connectionString);

    public void InitializeSchema()
    {
        using var conn = Connect();
        conn.Open();

        var sql = """
            CREATE TABLE IF NOT EXISTS areas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                type TEXT NOT NULL CHECK(type IN ('country','state','city')),
                parent_id INTEGER REFERENCES areas(id),
                latitude REAL,
                longitude REAL
            );

            CREATE TABLE IF NOT EXISTS emissions_data (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area_id INTEGER NOT NULL REFERENCES areas(id),
                category TEXT NOT NULL CHECK(category IN ('trucking','factory','energy','refrigeration','packaging')),
                amount_mtco2e REAL NOT NULL,
                year INTEGER NOT NULL,
                month INTEGER
            );

            CREATE TABLE IF NOT EXISTS carbon_initiatives (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area_id INTEGER NOT NULL REFERENCES areas(id),
                initiative_type TEXT NOT NULL CHECK(initiative_type IN ('rvm','ev_fleet','renewable_energy')),
                value REAL NOT NULL,
                effective_date TEXT
            );

            CREATE TABLE IF NOT EXISTS emission_grades (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area_id INTEGER NOT NULL REFERENCES areas(id),
                raw_score REAL NOT NULL,
                initiative_deduction REAL NOT NULL,
                final_score REAL NOT NULL,
                grade TEXT NOT NULL CHECK(grade IN ('A','B','C')),
                computed_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS rvm_machines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area_id INTEGER NOT NULL REFERENCES areas(id),
                location_name TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                active INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS rvm_scans (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                rvm_id INTEGER NOT NULL REFERENCES rvm_machines(id),
                user_id INTEGER REFERENCES users(id),
                product_barcode TEXT,
                scanned_at TEXT NOT NULL,
                points_awarded INTEGER NOT NULL DEFAULT 2,
                material_type TEXT,
                brand TEXT
            );

            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                total_points INTEGER NOT NULL DEFAULT 0,
                age INTEGER,
                zip_code TEXT
            );

            CREATE TABLE IF NOT EXISTS user_rewards (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL REFERENCES users(id),
                type TEXT NOT NULL CHECK(type IN ('earn','redeem')),
                points INTEGER NOT NULL,
                description TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS coupons (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL REFERENCES users(id),
                code TEXT NOT NULL UNIQUE,
                discount_description TEXT NOT NULL,
                issued_at TEXT NOT NULL,
                redeemed_at TEXT
            );

            CREATE TABLE IF NOT EXISTS admin_users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ai_insights (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area_id INTEGER NOT NULL REFERENCES areas(id),
                insight_text TEXT NOT NULL,
                suggestion_type TEXT,
                generated_at TEXT NOT NULL
            );
        """;

        // SQLite doesn't support multiple statements in one command, so split and run each
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = trimmed;
            cmd.ExecuteNonQuery();
        }

        // Migrations for existing databases (SQLite ignores errors on duplicate columns)
        RunMigration(conn, "ALTER TABLE users ADD COLUMN age INTEGER");
        RunMigration(conn, "ALTER TABLE users ADD COLUMN zip_code TEXT");
        RunMigration(conn, "ALTER TABLE users ADD COLUMN gender TEXT");
        RunMigration(conn, "ALTER TABLE users ADD COLUMN address TEXT");
        RunMigration(conn, "ALTER TABLE users ADD COLUMN qr_identifier TEXT");
        RunMigration(conn, "CREATE UNIQUE INDEX IF NOT EXISTS idx_users_qr_identifier ON users(qr_identifier)");
        RunMigration(conn, "ALTER TABLE rvm_scans ADD COLUMN material_type TEXT");
        RunMigration(conn, "ALTER TABLE rvm_scans ADD COLUMN brand TEXT");
        RunMigration(conn, "ALTER TABLE admin_users ADD COLUMN role TEXT NOT NULL DEFAULT 'admin'");
    }

    private static void RunMigration(SqliteConnection conn, string sql)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch { /* Column already exists — safe to ignore */ }
    }
}
