using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs;

public class PlantHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
