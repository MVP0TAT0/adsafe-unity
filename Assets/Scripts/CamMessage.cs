// File name: CamMessage.cs

using System;

namespace V2X.Protocol {

[Serializable]
public class CamMessage
{
    public Fields fields;
    // Note: If you ever need the root one, you can keep: public int stationID;
}

[Serializable]
public class Fields
{
    public Header header;
    public Cam cam;
}

[Serializable]
public class Header
{
    // This captures the stationId inside fields -> header
    public int stationId; 
    public int protocolVersion;
    public int messageId;
}

[Serializable]
public class Cam
{
    public CamParameters camParameters;
}

[Serializable]
public class CamParameters
{
    public BasicContainer basicContainer;
    public HighFrequencyContainer highFrequencyContainer;
}

[Serializable]
public class BasicContainer
{
    public ReferencePosition referencePosition;
    public int stationType;
}

[Serializable]
public class ReferencePosition
{
    public double latitude;
    public double longitude;
}

[Serializable]
public class HighFrequencyContainer
{
    public BasicVehicleContainerHighFrequency basicVehicleContainerHighFrequency;
}

[Serializable]
public class BasicVehicleContainerHighFrequency
{
    public Heading heading;
    public Speed speed;
}

[Serializable] 
public class Heading 
{ 
    public float headingValue; 
}

[Serializable] 
public class Speed 
{ 
    public float speedValue; 
}

[Serializable]
public class McmMessage
{
    public McmFields fields;
}

[Serializable]
public class McmFields
{
    public McmHeader header;
    public McmPayload payload;
}

[Serializable]
public class McmHeader
{
    public int stationId;
}

[Serializable]
public class McmPayload
{
    public McmBasicContainer basicContainer;
    public McmContainer mcmContainer;
}

[Serializable]
public class McmBasicContainer
{
    public int stationType;
    public int mcmType;
    public int manoeuvreId;
    public int executionStatus;
    public McmPosition position;
}

[Serializable]
public class McmPosition
{
    public double latitude;
    public double longitude;
}

[Serializable]
public class McmContainer
{
    public McmVehicleManoeuvreContainer vehicleManoeuvreContainer;
    public McmResponseContainer responseContainer;
}

[Serializable]
public class McmVehicleManoeuvreContainer
{
    public McmVehicleCurrentStateContainer vehicleCurrentStateContainer;
    public McmSubManoeuvre[] submaneuvres;
    public McmManoeuvreAdvice[] manoeuvreAdvice;
}

[Serializable]
public class McmVehicleCurrentStateContainer
{
    public McmSpeed vehicleSpeed;
    public McmHeading vehicleHeading;
}

[Serializable]
public class McmSpeed
{
    public double speedValue;
}

[Serializable]
public class McmHeading
{
    public double value;
}

[Serializable]
public class McmResponseContainer
{
    public McmSubManoeuvre[] submaneuvres;
}

[Serializable]
public class McmSubManoeuvre
{
    public int submanoeuvreID;
    public McmTrajectory referenceTrajectory;
    public McmTrajectory advisedTrajectory;
}

[Serializable]
public class McmManoeuvreAdvice
{
    public int executantID;
    public McmSubManoeuvre[] submaneuvres;
}

[Serializable]
public class McmTrajectory
{
    public McmPathWaypoint[] wayPoints;
}

[Serializable]
public class McmPathWaypoint
{
    public McmPathPosition pathPosition;
}

[Serializable]
public class McmPathPosition
{
    public double deltaLatitude;
    public double deltaLongitude;
    public double deltaAltitude;
}
}
