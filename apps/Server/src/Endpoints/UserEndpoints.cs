using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;
using Replanted.Server.Contracts.Users;

namespace RePlanted.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users");

        users.MapGet("", async (AppDbContext db) =>
            await db.Users.ToListAsync())
            .WithSummary("Get all users")
            .WithDescription("Returns all users.")
            .Produces<List<User>>(StatusCodes.Status200OK);

        users.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        })
            .WithSummary("Get user by ID")
            .WithDescription("Returns a single user when it exists.")
            .Produces<User>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        users.MapPost("", async (User user, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("UsersUpdated");

            return Results.Ok(new { Response = $"Added user: {user.Username}" });
        })
            .WithSummary("Create user")
            .WithDescription("Creates a new user and broadcasts UsersUpdated to SignalR clients.")
            .Accepts<User>("application/json")
            .Produces(StatusCodes.Status200OK);

        users.MapPut("/{id:int}", async (int id, User updatedUser, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
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

        users.MapDelete("/{id:int}", async (int id, AppDbContext db, IHubContext<UserHub> hubContext) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("UsersUpdated");
            return Results.Ok(new { Response = $"Deleted user: {user.Username}" });
        })
            .WithSummary("Delete user")
            .WithDescription("Deletes an existing user and broadcasts UsersUpdated to SignalR clients.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}