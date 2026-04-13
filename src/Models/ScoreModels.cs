namespace SchoolApi.Models;

// GET /scores query parameters
public class GetScoresRequest
{
    public int StudentId { get; set; }
    public int GradeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}

// Single score row returned by sp_get_scores
public class ScoreRow
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public decimal? Score { get; set; }
    public bool IsRecorded { get; set; }
    public DateTime? RecordedAt { get; set; }
}

// POST /scores request body
public class RecordScoreRequest
{
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int GradeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Score { get; set; }
}

// Response from sp_record_score
public class RecordScoreResult
{
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public decimal Score { get; set; }
    public string Operation { get; set; } = "";
}

// Student search result
public class StudentRow
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastNameFather { get; set; } = "";
    public string LastNameMother { get; set; } = "";
    public int GradeId { get; set; }
    public string GradeName { get; set; } = "";
    public string Status { get; set; } = "";
}

// Grade
public class GradeRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

// Login
public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public UserInfo User { get; set; } = new();
}

public class UserInfo
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; }
}
