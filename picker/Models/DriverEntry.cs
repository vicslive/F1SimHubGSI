namespace F1SimHubLive.Picker.Models;

/// <summary>
/// One row in the picker list — a single driver pulled from MultiViewer's
/// DriverList endpoint, enriched with championship-standings data so the
/// list can be grouped by team (constructors' order) and sorted within a
/// team by driver points.
/// </summary>
public sealed class DriverEntry
{
    public string Number { get; init; } = "";
    public string Tla { get; init; } = "";
    public string LastName { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string TeamName { get; init; } = "";
    /// <summary>Hex without leading '#'. May be empty.</summary>
    public string TeamColour { get; init; } = "";

    /// <summary>Fallback ordering when standings are unavailable.</summary>
    public int RacingNumberSort { get; init; }

    /// <summary>
    /// Constructors' championship position for this driver's team
    /// (1 = championship leader). int.MaxValue when unknown.
    /// </summary>
    public int TeamPosition { get; init; } = int.MaxValue;

    /// <summary>Constructors' championship points for the team.</summary>
    public int TeamPoints { get; init; }

    /// <summary>Drivers' championship points for this driver.</summary>
    public int DriverPoints { get; init; }

    /// <summary>True for the driver currently written in settings.json.
    /// Used by the XAML DataTrigger to highlight the row.</summary>
    public bool IsCurrent { get; init; }

    public string DisplayLastName =>
        string.IsNullOrWhiteSpace(LastName) ? Tla : LastName.ToUpperInvariant();

    public string DriverPointsDisplay =>
        DriverPoints > 0 ? $"{DriverPoints} pts" : "";
}
