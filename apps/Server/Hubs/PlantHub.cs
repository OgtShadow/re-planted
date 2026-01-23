using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs;

// Ta klasa zarządza połączeniami. 
// Na razie może być, pusta, służy jako punkt styku.
public class PlantHub : Hub
{
    // Opcjonalnie: Metoda, którą klient może wywołać, np. aby powiedzieć "Cześć"
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
