using System.Data;
using MySqlConnector;
using SchoolApi.Models;

namespace SchoolApi.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration config)
    {
        var host = config["DB_HOST"] ?? "localhost";
        var port = config["DB_PORT"] ?? "3306";
        var name = config["DB_NAME"] ?? "school_db";
        var user = config["DB_USER"] ?? "school_user";
        var pass = config["DB_PASSWORD"] ?? "school_pass";

        _connectionString = $"Server={host};Port={port};Database={name};User={user};Password={pass};";
    }

    private MySqlConnection CreateConnection() => new(_connectionString);

    // ── Login ──────────────────────────────────
    public async Task<UserInfo?> GetUserByEmail(string email)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("CALL sp_get_user_by_email(@email)", conn);
        cmd.Parameters.AddWithValue("@email", email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var firstName = reader.GetString("first_name");
            var lastName = reader.GetString("last_name");
            return new UserInfo
            {
                Id = reader.GetInt32("id"),
                Email = reader.GetString("email"),
                PasswordHash = reader.GetString("password_hash"),
                Role = reader.GetString("role"),
                IsActive = reader.GetBoolean("is_active"),
                Name = $"{firstName} {lastName}".Trim()
            };
        }
        return null;
    }

    // ── Search Students ────────────────────────
    public async Task<List<StudentRow>> SearchStudents(string term, string? status, int limit, int offset)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(
            "CALL sp_search_students(@term, @status, @limit, @offset)", conn);
        cmd.Parameters.AddWithValue("@term", term);
        cmd.Parameters.AddWithValue("@status", status ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var results = new List<StudentRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new StudentRow
            {
                Id = reader.GetInt32("id"),
                FirstName = reader.GetString("first_name"),
                LastNameFather = reader.GetString("last_name_father"),
                LastNameMother = reader.IsDBNull(reader.GetOrdinal("last_name_mother")) ? "" : reader.GetString("last_name_mother"),
                GradeId = reader.GetInt32("grade_id"),
                GradeName = reader.GetString("grade_name"),
                Status = reader.GetString("status")
            });
        }
        return results;
    }

    // ── Get Grades ──────────────────────────────
    public async Task<List<GradeRow>> GetGrades()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("SELECT id, name FROM grades ORDER BY id", conn);

        var results = new List<GradeRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new GradeRow
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name")
            });
        }
        return results;
    }

    // ── Get Scores ─────────────────────────────
    public async Task<List<ScoreRow>> GetScores(int studentId, int gradeId, int year, int month)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(
            "CALL sp_get_scores(@studentId, @gradeId, @year, @month)", conn);
        cmd.Parameters.AddWithValue("@studentId", studentId);
        cmd.Parameters.AddWithValue("@gradeId", gradeId);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);

        var results = new List<ScoreRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ScoreRow
            {
                SubjectId = reader.GetInt32("subject_id"),
                SubjectName = reader.GetString("subject_name"),
                Score = reader.IsDBNull("score") ? null : reader.GetDecimal("score"),
                IsRecorded = reader.GetBoolean("is_recorded"),
                RecordedAt = reader.IsDBNull("recorded_at") ? null : reader.GetDateTime("recorded_at")
            });
        }
        return results;
    }

    // ── Record Score ───────────────────────────
    public async Task<RecordScoreResult> RecordScore(RecordScoreRequest req)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(
            "CALL sp_record_score(@studentId, @subjectId, @gradeId, @year, @month, @score)", conn);
        cmd.Parameters.AddWithValue("@studentId", req.StudentId);
        cmd.Parameters.AddWithValue("@subjectId", req.SubjectId);
        cmd.Parameters.AddWithValue("@gradeId", req.GradeId);
        cmd.Parameters.AddWithValue("@year", req.Year);
        cmd.Parameters.AddWithValue("@month", req.Month);
        cmd.Parameters.AddWithValue("@score", req.Score);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RecordScoreResult
            {
                StudentId = reader.GetInt32("student_id"),
                SubjectId = reader.GetInt32("subject_id"),
                Score = reader.GetDecimal("score"),
                Operation = reader.GetString("operation")
            };
        }

        throw new InvalidOperationException("No result from sp_record_score");
    }
}
