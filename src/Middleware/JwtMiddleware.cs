using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SchoolApi.Middleware;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _secret;

    public JwtMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _secret = config["JWT_SECRET"] ?? "dev-secret-change-in-production";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for login and health endpoints
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("/login") || path.Contains("/health") || context.Request.Method == "OPTIONS")
        {
            await _next(context);
            return;
        }

        var token = context.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { success = false, error = "Token required" });
            return;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(token, validationParams, out _);

            // Attach user info to context
            context.Items["UserId"] = principal.FindFirst("id")?.Value;
            context.Items["UserRole"] = principal.FindFirst(ClaimTypes.Role)?.Value
                                        ?? principal.FindFirst("role")?.Value;
            context.Items["UserEmail"] = principal.FindFirst(ClaimTypes.Email)?.Value
                                         ?? principal.FindFirst("email")?.Value;

            await _next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { success = false, error = "Invalid or expired token" });
        }
    }
}
