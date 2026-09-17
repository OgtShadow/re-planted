using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs;

public class PlantHub : UserHubAuthorization
{
    public async Task SendMessage(string message)
    {
        if (int.TryParse(Context.UserIdentifier, out var userId))
        {
            await Clients.Group(GroupName(userId)).SendAsync("ReceiveMessage", message);
        }
    }
}
