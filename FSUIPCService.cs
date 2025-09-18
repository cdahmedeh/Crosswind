using FSUIPC;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

public record FSUIPCTelemetry : Telemetry;

/// For pulling in flight data from FSUIPC.
///
/// There's not that much to say here. FSUIPC is incredibly straight-forward. You just read values from offests that
/// FUSIPC offers and keep processing them for every update.
///
/// The real challenge is just finding the conversion factors to convert them into units that can be worked with. Often,
/// they have to be converted from a binary format by dividing with an integer of specific bit size. And then convert
/// the unit that FSUIPC provides to ones that are normally used in aviation. For example, converting meters per seconds
/// to knots for ground speed.
///
/// Offsets are easily found online, I used https://www.projectmagenta.com/all-fsuipc-offsets/
///
/// Finally, FSUIPC is quite universal, and works on every version of Microsoft Flight Simulator all the way back to
/// version 98. Every version of Prepar3D. And X-Plane 9 to 11.
///
/// Uses the FSUIPC Client DLL for .NET by Paul Henty. http://fsuipc.paulhenty.com/#licence
///
public class FSUIPCService(ILogger<FSUIPCService> logger) : ITelemetryService<FSUIPCTelemetry>
{
    // FSUIPC Group for association with this application instance and refreshing offset values.
    private const string GroupName = "LegacySimBridge";

    // FSUIPC Offset Numerical Conversion
    private const int   MaxInteger7Bit  = 1 << 7;
    private const int   MaxInteger8Bit  = 1 << 8;
    private const int   MaxInteger9Bit  = 1 << 9;
    private const int   MaxInteger16Bit = 1 << 16;
    private const long  MaxInteger32Bit = 1L << 32;
    
    // Unit Conversion Factors
    private const double MetersPerSecondToKnots = 1.943844;
    private const double MetersToFeet = 3.28084;
    private const double PascalsToInchMercury = 33.8638866667;
    private const double VerticalSpeedFactor = 60.0;
    private const double HeadingFullCircle = 360.0;
    private const double PitchFactor = 0.1;
    private const double BankFactor = -1;
    private const double TurnRateFactor = 3.0;
    private const double GForceConversionFactor = 625.0;

    // FSUIPC Offsets
    private readonly Offset<uint> _offsetSquawkCode          = new Offset<uint>(GroupName,        0x0354);
    
    private readonly Offset<FsLongitude> _offsetLongitude    = new Offset<FsLongitude>(GroupName, 0x0568, 8);
    private readonly Offset<FsLatitude> _offsetLatitude      = new Offset<FsLatitude>(GroupName,  0x0560, 8);
    
    private readonly Offset<uint> _offsetIndicatedAirSpeed   = new Offset<uint>(GroupName,        0x02BC);
    private readonly Offset<uint> _offsetTrueAirSpeed        = new Offset<uint>(GroupName,        0x02B8);
    private readonly Offset<uint> _offsetGroundSpeed         = new Offset<uint>(GroupName,        0x02B4);
    private readonly Offset<int> _offsetVerticalSpeed        = new Offset<int>(GroupName,         0x02C8);
    
    private readonly Offset<long> _offsetAltitude            = new Offset<long>(GroupName,        0x0570);
    private readonly Offset<long> _offsetRadioAltitude       = new Offset<long>(GroupName,        0x31E4);
    private readonly Offset<uint> _offsetAltimeterPressure   = new Offset<uint>(GroupName,        0x0330);
    
    private readonly Offset<uint> _offsetHeading             = new Offset<uint>(GroupName,        0x0580);
    private readonly Offset<short> _offsetMagneticVariation  = new Offset<short>(GroupName,       0x02A0);
    private readonly Offset<short> _offsetTurnRate           = new Offset<short>(GroupName,       0x037C);
    
    private readonly Offset<int> _offsetPitch                = new Offset<int>(GroupName,         0x057B);
    private readonly Offset<int> _offsetBank                 = new Offset<int>(GroupName,         0x057C);
    private readonly Offset<short> _offsetGForce             = new Offset<short>(GroupName,       0x11BA);
    
    private readonly Offset<ushort> _offsetPauseIndicator    = new Offset<ushort>(GroupName,      0x0264);
    private readonly Offset<ushort> _offsetSlewMode          = new Offset<ushort>(GroupName,      0x05DC);
    
    public bool Connect()
    {
        try
        {
            logger.LogInformation("Connecting to FSUIPC...");
            FSUIPCConnection.Open();
            logger.LogInformation("Connected successfully to FSUIPC!");
            FSUIPCVersion connectionVersion = FSUIPCConnection.FSUIPCVersion;
            logger.LogInformation($"Detected FSUIPC version: {connectionVersion}");
            FsVersion connectionFlightSim = FSUIPCConnection.FlightSimVersionConnected;
            logger.LogInformation($"Detected Flight Simulator: {connectionFlightSim}");
            return true;
        }
        catch (FSUIPCException ex)
        {
            logger.LogError(ex, "Failed to connect to FSUIPC!");
            return false;
        }
    }

    public bool Disconnect()
    {
        try
        {
            logger.LogInformation("Closing connection to FSUIPC...");
            FSUIPCConnection.Close();
            logger.LogInformation("Disconnected successfully from FSUIPC!");
            return true;
        }
        catch (FSUIPCException ex)
        {
            logger.LogError(ex, "Failed to disconnect from FSUIPC!");
            return false;            
        }
    }

    public bool Refresh()
    {
        try
        {
            logger.LogTrace("Processing and refreshing FSUIPC offsets...");
            FSUIPCConnection.Process(GroupName);
            logger.LogTrace("FSUIPC offset values processed.");
            return true;
        }
        catch (FSUIPCException ex)
        {
            logger.LogError(ex, "Failed to process FSUIPC offsets!");
            return false;
        }

    }

    public FSUIPCTelemetry GetTelemetry()
    {
        FSUIPCTelemetry telemetry = new FSUIPCTelemetry();
        
        telemetry.SquawkCode =            _offsetSquawkCode.Value.ToString("X4");
        
        telemetry.Longitude =             _offsetLongitude.Value.DecimalDegrees;
        telemetry.Latitude =              _offsetLatitude.Value.DecimalDegrees;
        
        telemetry.IndicatedAirSpeed =    (int) ( ( _offsetIndicatedAirSpeed.Value    /   (double) MaxInteger7Bit   )                                         );
        telemetry.TrueAirSpeed =         (int) ( ( _offsetTrueAirSpeed.Value         /   (double) MaxInteger7Bit   )                                         );
        telemetry.GroundSpeed =          (int) ( ( _offsetGroundSpeed.Value          /   (double) MaxInteger16Bit  )    * MetersPerSecondToKnots             );
        telemetry.VerticalSpeed =        (int) ( ( _offsetVerticalSpeed.Value        /   (double) MaxInteger7Bit   )    * MetersToFeet * VerticalSpeedFactor );
       
        telemetry.IndicatedAltitude =    (int) ( ( _offsetAltitude.Value             /   (double) MaxInteger32Bit  )    * MetersToFeet                       );
        telemetry.RadioAltitude =        (int) ( ( _offsetRadioAltitude.Value        /   (double) MaxInteger16Bit  )    * MetersToFeet                       );
        telemetry.AltimeterPressure =          ( ( _offsetAltimeterPressure.Value    /   (double) MaxInteger8Bit   )    * PascalsToInchMercury               );
        
        telemetry.TrueHeading =          (int) ( ( _offsetHeading.Value              /   (double) MaxInteger32Bit  )    * HeadingFullCircle                  ); 
        
        int magneticVariation =          (int) ( ( _offsetMagneticVariation.Value    /   (double) MaxInteger16Bit  )    * HeadingFullCircle                  );
        telemetry.IndicatedHeading =               telemetry.TrueHeading                                                - magneticVariation                   ;
       
        telemetry.Pitch =                (int) ( ( _offsetPitch.Value                /   (double) MaxInteger32Bit  )    * HeadingFullCircle * PitchFactor    );
        telemetry.Bank =                 (int) ( ( _offsetBank.Value                 /   (double) MaxInteger32Bit  )    * HeadingFullCircle * BankFactor     );

        telemetry.TurnRate =             (int) ( ( _offsetTurnRate.Value             /   (double) MaxInteger9Bit   )    * TurnRateFactor                     );

        telemetry.GForce =                     (   _offsetGForce.Value                                                  / GForceConversionFactor             );
        
        telemetry.Paused =                         _offsetPauseIndicator.Value                                          == 1                                  ;
        telemetry.SlewMode =                       _offsetSlewMode.Value                                                == 1                                  ; 
       
        return telemetry;
    }
}