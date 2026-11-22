namespace MediaLog.Models;

/// <summary>A person or company credited on a work ("Composer: Koji Kondo").</summary>
public sealed class Credit
{
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
}
