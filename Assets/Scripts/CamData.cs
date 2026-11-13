using System;
using System.Collections.Generic;

// --- CAM Data Structures ---
[Serializable]
public class CamMessage { public CamParameters camParameters; }
[Serializable]
public class CamParameters {
    public BasicContainer basicContainer;
    public HighFrequencyContainer highFrequencyContainer;
}
[Serializable]
public class HighFrequencyContainer { public BasicVehicleContainerHighFrequency basicVehicleContainerHighFrequency; }
[Serializable]
public class BasicVehicleContainerHighFrequency {
    public Heading heading;
    public Speed speed;
}
[Serializable]
public class Heading { public float headingValue; }
[Serializable]
public class Speed { public float speedValue; }

// --- MCM Data Structures ---
[Serializable]
public class McmRoot {
    public McmFields fields;
    public BasicContainer basicContainer;
    public McmContainer mcmContainer;
    
    // In case the trajectory is stored at the root of the JSON
    public List<TrajectoryPoint> trajectory; 

    public BasicContainer GetBasicContainer() {
        return fields?.payload?.basicContainer ?? basicContainer;
    }

    // Safely hunts down the trajectory array no matter where the JSON hides it
    public List<TrajectoryPoint> GetTrajectory() {
        if (trajectory != null && trajectory.Count > 0) 
            return trajectory;
        
        if (fields?.payload?.mcmContainer?.vehicleManoeuvreContainer?.trajectory != null) 
            return fields.payload.mcmContainer.vehicleManoeuvreContainer.trajectory;
            
        if (mcmContainer?.vehicleManoeuvreContainer?.trajectory != null) 
            return mcmContainer.vehicleManoeuvreContainer.trajectory;
            
        return null;
    }
}
[Serializable]
public class McmFields { public McmPayload payload; }
[Serializable]
public class McmPayload {
    public BasicContainer basicContainer;
    public McmContainer mcmContainer;
}
[Serializable]
public class BasicContainer {
    public ReferencePosition referencePosition; // For CAM
    public ReferencePosition position;          // For MCM
    public int manoeuvreId;                     // The Overtake Trigger
    public int executionStatus;
}
[Serializable]
public class ReferencePosition {
    public double latitude;
    public double longitude;
}
[Serializable]
public class McmContainer { public VehicleManoeuvreContainer vehicleManoeuvreContainer; }
[Serializable]
public class VehicleManoeuvreContainer { 
    // In case the trajectory is nested deeply following strict ETSI standards
    public List<TrajectoryPoint> trajectory; 
}

// --- Shared Data Structures ---
[Serializable]
public class TrajectoryPoint {
    public double latitude;
    public double longitude;
}