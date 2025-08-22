using FSUIPC;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

public class FSUIPCService
{
    private const string GroupName = "LegacySimBridge";
    
    private readonly ILogger<FSUIPCService> _logger;
    
    private Offset<FsLongitude> offsetLongitude = new Offset<FsLongitude>(GroupName, 0x0568, 8);
    private Offset<FsLatitude> offsetLatitude = new Offset<FsLatitude>(GroupName, 0x0560, 8);
    
    private Offset<uint> offsetIndicatedAirSpeed = new Offset<uint>(GroupName, 0x02BC);
    private Offset<uint> offsetTrueAirSpeed = new Offset<uint>(GroupName, 0x02B8);
    private Offset<uint> offsetGroundSpeed = new Offset<uint>(GroupName, 0x02B4);
    
    private Offset<ushort> offsetSquawkCode = new Offset<ushort>(GroupName, 0x0354);
    
    public FSUIPCService(ILogger<FSUIPCService> logger)
    {
        _logger = logger;
    }
    
    public bool Connect()
    {
        try
        {
            _logger.LogInformation("Connecting to FSUIPC");
            
            FSUIPCConnection.Open();
            FSUIPCConnection.Process(GroupName);
            
            _logger.LogInformation("Connected successfully to FSUIPC");
            
            return true;
        }
        catch (FSUIPCException ex)
        {
            _logger.LogError(ex, "Failed to connect to FSUIPC");
            
            return false;
        }
    }

    public Coordinates GetCoordinates()
    {
        double latitude = offsetLatitude.Value.DecimalDegrees;
        double longitude = offsetLongitude.Value.DecimalDegrees;
    
        return new Coordinates(latitude, longitude);
    }

    public string GetSquawkCode()
    {
        ushort squawkCode = offsetSquawkCode.Value;
        return squawkCode.ToString("X4");
    }

    public Speeds GetSpeeds()
    {
        double indiactedAirSpeed = offsetIndicatedAirSpeed.Value / 128.0;
        double trueAirSpeed = offsetTrueAirSpeed.Value / 128.0;
        double groundSpeed = (offsetGroundSpeed.Value / 65536.0) * 1.943844; 
        
        return new Speeds(indiactedAirSpeed, trueAirSpeed, groundSpeed);
    }
}

public record Coordinates(double Latitude, double Longitude);
public record Speeds(double IndicatedAirSpeed, double TrueAirSpeed, double GroundSpeed);