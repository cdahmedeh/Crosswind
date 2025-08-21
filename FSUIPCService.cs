using FSUIPC;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

public class FSUIPCService
{
    private readonly ILogger<FSUIPCService> _logger;
    
    private Offset<FsLongitude> offsetLon = new Offset<FsLongitude>("LegacySimBridge", 0x0568, 8);
    private Offset<FsLatitude> offsetLat = new Offset<FsLatitude>("LegacySimBridge", 0x0560, 8);
    
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
            FSUIPCConnection.Process("LegacySimBridge");
            
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
        double latitude = offsetLat.Value.DecimalDegrees;
        double longitude = offsetLon.Value.DecimalDegrees;
    
        return new Coordinates(latitude, longitude);
    }
}

public record Coordinates(double Latitude, double Longitude);