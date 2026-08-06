using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;
using RePlanted.Server.Contracts.Users;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RePlanted.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users");

        users.MapPost("/login", async (LoginUserRequest request, AppDbContext db, IConfiguration configuration) =>
        {
            var login = request.Login?.Trim();
            if (string.IsNullOrWhiteSpace(login))
            {
                return Results.BadRequest(new { Response = "Login jest wymagany." });
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login || u.Username == login);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var requiresPassword = !string.IsNullOrWhiteSpace(user.PasswordHash);
            if (requiresPassword && user.PasswordHash != (request.Password ?? string.Empty))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = GenerateJwtToken(user, configuration)
            });
        })
            .WithSummary("Login user")
            .WithDescription("Authenticates user by username/email and password.")
            .Accepts<LoginUserRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        users.MapGet("", [Authorize] async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!TryGetUserId(principal, out var callerUserId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == callerUserId);
            return user is not null ? Results.Ok(new List<User> { user }) : Results.NotFound();
        })
            .WithSummary("Get all users")
            .WithDescription("Returns currently authenticated user.")
            .Produces<List<User>>(StatusCodes.Status200OK);

        users.MapGet("/{id:int}", [Authorize] async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!TryGetUserId(principal, out var callerUserId) || callerUserId != id)
            {
                return Results.Forbid();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        })
            .WithSummary("Get user by ID")
            .WithDescription("Returns a single user when it exists.")
            .Produces<User>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        users.MapPost("", async (UpsertUserRequest request, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
            var email = request.Email?.Trim();
            var password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.BadRequest(new { Response = "Email jest wymagany." });
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return Results.BadRequest(new { Response = "Hasło jest wymagane." });
            }

            var username = string.IsNullOrWhiteSpace(request.Username)
                ? email.Split('@')[0]
                : request.Username.Trim();

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = password,
                CreatedAt = request.CreatedAt ?? DateTime.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            await ActuatorDeviceEndpoints.EnsureEspMockDeviceAsync(db, user.Id);

            await hubContext.Clients.All.SendAsync("UsersUpdated");

            return Results.Ok(new { Response = $"Added user: {user.Username}" });
        })
            .WithSummary("Create user")
            .WithDescription("Creates a new user and broadcasts UsersUpdated to SignalR clients.")
            .Accepts<UpsertUserRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        users.MapPut("/{id:int}", [Authorize] async (int id, User updatedUser, ClaimsPrincipal principal, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
            if (!TryGetUserId(principal, out var callerUserId) || callerUserId != id)
            {
                return Results.Forbid();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("UsersUpdated");

            return Results.Ok(new { Response = $"Updated user: {user.Username}" });
        })
            .WithSummary("Update user")
            .WithDescription("Updates an existing user and broadcasts UsersUpdated to SignalR clients.")
            .Accepts<User>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        users.MapDelete("/{id:int}", [Authorize] async (int id, ClaimsPrincipal principal, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
            if (!TryGetUserId(principal, out var callerUserId) || callerUserId != id)
            {
                return Results.Forbid();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("UsersUpdated");

            return Results.Ok(new { Response = $"Deleted user: {user.Username}", Id = user.Id });
        })
            .WithSummary("Delete user")
            .WithDescription("Deletes an existing user and broadcasts UsersUpdated to SignalR clients.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out int userId)
    {
        userId = 0;
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out userId);
    }

    private static string GenerateJwtToken(User user, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key configuration");
        var issuer = configuration["Jwt:Issuer"] ?? "re-planted-server";
        var audience = configuration["Jwt:Audience"] ?? "re-planted-client";
        var expiresMinutes = int.TryParse(configuration["Jwt:ExpiresMinutes"], out var parsedMinutes)
            ? parsedMinutes
            : 120;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}