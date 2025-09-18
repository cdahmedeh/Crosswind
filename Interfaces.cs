namespace LegacySimBridge;

public interface ITelemetryService<out T> where T : Telemetry
{
    bool Connect();
    bool Disconnect();
    bool Refresh();
    T GetTelemetry();
}

public interface ITelemetryReceiver
{
    bool Start();
    bool Stop();
}

public interface IFSUIPCReceiver
{
    bool Send(FSUIPCTelemetry telemetry);
}

public record Telemetry
{
    // Information
    public string SquawkCode { get; set; }

    // Position
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    
    // Speeds
    public int IndicatedAirSpeed { get; set; }
    public int TrueAirSpeed { get; set; }
    public int GroundSpeed { get; set; }
    public int VerticalSpeed { get; set; }
    
    // Altitudes
    public int IndicatedAltitude { get; set; }
    public int RadioAltitude { get; set; }
    public double AltimeterPressure { get; set; }
    
    // Headings
    public int IndicatedHeading { get; set; }
    public int TrueHeading { get; set; }
    public int TurnRate { get; set; }
    
    // Attitudes
    public double Pitch { get; set; }
    public double Bank { get; set; }
    public double GForce { get; set; }
    
    // Statuses
    public bool Paused { get; set; }
    public bool SlewMode { get; set; }
}