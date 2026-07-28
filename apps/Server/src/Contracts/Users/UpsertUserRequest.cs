using RePlanted.Server.Models;

namespace RePlanted.Server.Contracts.Users;

public class UpsertUserRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }

    public DateTime? CreatedAt { get; set; }
    public Parameters? Plants { get; set; }
}
