using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs;

[Authorize]
public abstract class UserHubAuthorization : Hub
{
    public static string GroupName(int userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.UserIdentifier, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        }

        await base.OnConnectedAsync();
    }
}