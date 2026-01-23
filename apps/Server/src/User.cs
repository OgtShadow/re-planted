namespace RePlanted.Server.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public List<RePlanted.Server.Plant> Plants { get; set; } = new List<RePlanted.Server.Plant>();
}
