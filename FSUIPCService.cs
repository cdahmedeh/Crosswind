using FSUIPC;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

public class FSUIPCTelemetry : Telemetry
{
    
}
public class FSUIPCService : ITelemetryService<FSUIPCTelemetry>
{
    private const string GroupName = "LegacySimBridge";
    
    private readonly ILogger<FSUIPCService> _logger;
    
    private readonly Offset<FsLongitude> _offsetLongitude = new Offset<FsLongitude>(GroupName, 0x0568, 8);
    private readonly Offset<FsLatitude> _offsetLatitude = new Offset<FsLatitude>(GroupName, 0x0560, 8);
    
    private readonly Offset<uint> _offsetIndicatedAirSpeed = new Offset<uint>(GroupName, 0x02BC);
    private readonly Offset<uint> _offsetTrueAirSpeed = new Offset<uint>(GroupName, 0x02B8);
    private readonly Offset<uint> _offsetGroundSpeed = new Offset<uint>(GroupName, 0x02B4);
    
    private readonly Offset<long> _offsetAltitude = new Offset<long>(GroupName, 0x0570);
    private readonly Offset<long> _offsetRadioAltitude = new Offset<long>(GroupName, 0x31E4);
    private readonly Offset<uint> _offsetAltimeterPressure = new Offset<uint>(GroupName, 0x0330);
    
    private readonly Offset<uint> _offsetHeading = new Offset<uint>(GroupName, 0x0580);
    private readonly Offset<short> _offsetMagneticVariation = new Offset<short>(GroupName, 0x02A0);
    
    private readonly Offset<int> _offsetVerticalSpeed = new Offset<int>(GroupName, 0x02C8);
    
    private readonly Offset<int> _offsetPitch = new Offset<int>(GroupName, 0x057B);
    private readonly Offset<int> _offsetBank = new Offset<int>(GroupName, 0x057C);
    
    private readonly Offset<short> _offsetTurnRate = new Offset<short>(GroupName, 0x037C);
    
    private readonly Offset<short> _offsetGForce = new Offset<short>(GroupName, 0x11BA);
    
    private readonly Offset<ushort> _offsetPauseIndicator = new Offset<ushort>(GroupName, 0x0264);
    private readonly Offset<ushort> _offsetSlewMode = new Offset<ushort>(GroupName, 0x05DC);
    
    private readonly Offset<short> _offsetLocalTimeOffset = new Offset<short>(GroupName, 0x05DC);
    
    private readonly Offset<uint> _offsetSquawkCode = new Offset<uint>(GroupName, 0x0354);
    
    public FSUIPCService(ILogger<FSUIPCService> logger)
    {
        _logger = logger;
    }
    
    public bool Connect()
    {
        try
        {
            _logger.LogInformation("Connecting to FSUIPC...");
            FSUIPCConnection.Open();
            _logger.LogInformation("Connected successfully to FSUIPC!");
            FSUIPCVersion connectionVersion = FSUIPCConnection.FSUIPCVersion;
            _logger.LogInformation($"Detected FSUIPC version: {connectionVersion}");
            FsVersion connectionFlightSim = FSUIPCConnection.FlightSimVersionConnected;
            _logger.LogInformation($"Detected Flight Simulator: {connectionFlightSim}");
            return true;
        }
        catch (FSUIPCException ex)
        {
            _logger.LogError(ex, "Failed to connect to FSUIPC!");
            return false;
        }
    }

    public bool Disconnect()
    {
        try
        {
            _logger.LogInformation("Closing connection to FSUIPC");
            FSUIPCConnection.Close();
            _logger.LogInformation("Disconnected successfully from FSUIPC");
            return true;
        }
        catch (FSUIPCException ex)
        {
            _logger.LogError(ex, "Failed to disconnect from FSUIPC");
            return false;            
        }
    }

    public bool Refresh()
    {
        FSUIPCConnection.Process(GroupName);
        return true;
    }

    public FSUIPCTelemetry GetTelemetry()
    {
        FSUIPCTelemetry telemetry = new FSUIPCTelemetry();
        
        telemetry.SquawkCode = _offsetSquawkCode.Value.ToString("X4");
        
        telemetry.Longitude = _offsetLongitude.Value.DecimalDegrees;
        telemetry.Latitude = _offsetLatitude.Value.DecimalDegrees;
        
        telemetry.IndicatedAirSpeed = (int) (_offsetIndicatedAirSpeed.Value / 128.0);
        telemetry.TrueAirSpeed = (int) (_offsetTrueAirSpeed.Value / 128.0);
        telemetry.GroundSpeed = (int) ((_offsetGroundSpeed.Value / 65536.0) * 1.943844);
        telemetry.VerticalSpeed = (int)(_offsetVerticalSpeed.Value * 60.0 * 3.28084 / 256);
       
        telemetry.IndicatedAltitude = (int) ((_offsetAltitude.Value / 65536.0 / 65536.0) * 3.28084);
        telemetry.RadioAltitude = (int) ((_offsetRadioAltitude.Value / 65536.0) * 3.28084);
        telemetry.AltimeterPressure = (_offsetAltimeterPressure.Value) / (16.0 * 33.8638866667);
        
        telemetry.TrueHeading = (int)(((double)_offsetHeading.Value) * 360 / (65536.0 * 65536.0)); 
        int magneticVariation =  (int)(((double)_offsetMagneticVariation.Value) * 360 / (65536.0)); 
        telemetry.IndicatedHeading = telemetry.TrueHeading - magneticVariation;
       
        telemetry.Pitch = (int)((_offsetPitch.Value * 360.0) / 65536.0 / 65536.0) * -1;
        telemetry.Bank = (int)((_offsetBank.Value * 360.0) / 65536.0 / 65536.0) * -1;

        telemetry.TurnRate = (int)((_offsetTurnRate.Value / 512.0) * 3.0);

        telemetry.GForce = (_offsetGForce.Value) / 625.0;
        
        telemetry.Paused = _offsetPauseIndicator.Value == 1;
        telemetry.SlewMode = _offsetSlewMode.Value == 1; 
        
        return telemetry;
    }
}