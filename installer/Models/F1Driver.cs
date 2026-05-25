namespace F1SimHubLive.Installer.Models;

public sealed class F1Driver
{
    public int Number { get; set; }
    public string Code { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Team { get; set; } = "";

    public string DisplayName => $"#{Number,-3} {FirstName} {LastName} ({Team})";
}
