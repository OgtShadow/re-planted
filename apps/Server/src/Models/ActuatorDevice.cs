namespace RePlanted.Server.Models;

public class ActuatorDevice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetParameter { get; set; } = string.Empty;
    public string EffectType { get; set; } = "increase";
    public double EffectStrength { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public List<Plant> Plants { get; set; } = new();
}
