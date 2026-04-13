using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SchoolApi.Middleware;
using SchoolApi.Models;
using SchoolApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda hosting
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Register services
builder.Services.AddSingleton<DatabaseService>();

// Load env vars as configuration (Lambda sets these from SAM template)
builder.Configuration.AddEnvironmentVariables();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // TODO: In production, restrict to specific frontend domain
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();
app.UseMiddleware<JwtMiddleware>();

// ── Health Check ───────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "school-api-dotnet" }));

// ── Login ──────────────────────────────────────
app.MapPost("/login", async (LoginRequest req, DatabaseService db, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(ApiResponse.Fail("Email and password are required"));

    try
    {
        var user = await db.ValidateLogin(req.Email, req.Password);
        if (user == null)
            return Results.Json(ApiResponse.Fail("Invalid credentials"), statusCode: 401);

        var secret = config["JWT_SECRET"] ?? "dev-secret-change-in-production";
        var expiresIn = config["JWT_EXPIRES_IN"] ?? "24h";
        var hours = int.Parse(expiresIn.Replace("h", ""));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("id", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role),
            new Claim("name", user.Name)
        };

        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddHours(hours),
            claims: claims,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = tokenString,
            User = user
        }));
    }
    catch (MySqlConnector.MySqlException ex) when (ex.Message.Contains("45000") || ex.SqlState == "45000")
    {
        return Results.Json(ApiResponse.Fail(ex.Message), statusCode: 401);
    }
});

// ── GET /scores ────────────────────────────────
app.MapGet("/scores", async (int studentId, int gradeId, int year, int month, DatabaseService db) =>
{
    if (studentId <= 0 || gradeId <= 0 || year <= 0 || month < 1 || month > 12)
        return Results.BadRequest(ApiResponse.Fail("studentId, gradeId, year, and month (1-12) are required"));

    try
    {
        var scores = await db.GetScores(studentId, gradeId, year, month);
        return Results.Ok(ApiResponse<List<ScoreRow>>.Ok(scores));
    }
    catch (MySqlConnector.MySqlException ex) when (ex.SqlState == "45000")
    {
        return Results.BadRequest(ApiResponse.Fail(ex.Message));
    }
});

// ── POST /scores ───────────────────────────────
app.MapPost("/scores", async (RecordScoreRequest req, DatabaseService db) =>
{
    if (req.StudentId <= 0 || req.SubjectId <= 0 || req.GradeId <= 0)
        return Results.BadRequest(ApiResponse.Fail("studentId, subjectId, and gradeId are required"));

    if (req.Score < 0 || req.Score > 10)
        return Results.BadRequest(ApiResponse.Fail("Score must be between 0.00 and 10.00"));

    if (req.Month < 1 || req.Month > 12)
        return Results.BadRequest(ApiResponse.Fail("Month must be between 1 and 12"));

    if (req.Year <= 0)
        return Results.BadRequest(ApiResponse.Fail("Year is required"));

    try
    {
        var result = await db.RecordScore(req);
        return Results.Ok(ApiResponse<RecordScoreResult>.Ok(result));
    }
    catch (MySqlConnector.MySqlException ex) when (ex.SqlState == "45000")
    {
        return Results.BadRequest(ApiResponse.Fail(ex.Message));
    }
});

app.Run();
