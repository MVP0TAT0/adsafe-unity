using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MqttVehicleController : MonoBehaviour
{
    [Header("MQTT Connection")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;

    [System.Serializable]
    public class VehicleMapping
    {
        public string topicIdentifier;
        public Transform vehicleTransform;
        
        // Linked to our new script instead of the raw LineRenderer
        public ManeuverPathVisualizer pathVisualizer; 

        [HideInInspector] public Vector3 targetPosition;
        [HideInInspector] public Quaternion targetRotation;
        [HideInInspector] public float currentHeading;
        [HideInInspector] public bool hasData = false;
        [HideInInspector] public float initialY;
    }

    [Header("Vehicle Setup")]
    public List<VehicleMapping> vehicles = new List<VehicleMapping>();
    public float lerpSpeed = 12f;

    [Header("Procedural Overtake Path Settings")]
    [Tooltip("Used if the dataset does not provide exact GPS trajectory points.")]
    public float laneWidth = 3.5f; 
    public float maneuverLength = 20f; 

    private MqttClient client;
    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    private readonly object queueLock = new object();

    private bool originSet = false;
    private double originLat = 0;
    private double originLon = 0;

    void Start()
    {
        foreach (var v in vehicles)
        {
            if (v.vehicleTransform != null) v.initialY = v.vehicleTransform.localPosition.y;
            if (v.pathVisualizer != null) v.pathVisualizer.ClearPath();
        }

        try
        {
            client = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString());
            client.Subscribe(new string[] { "#" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }
        catch (Exception ex) { Debug.LogError($"MQTT Error: {ex.Message}"); }
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string topic = e.Topic;
        string payload = Encoding.UTF8.GetString(e.Message);
        lock (queueLock) { mainThreadQueue.Enqueue(() => ProcessMessage(topic, payload)); }
    }

    void Update()
    {
        lock (queueLock) { while (mainThreadQueue.Count > 0) mainThreadQueue.Dequeue().Invoke(); }

        foreach (var v in vehicles)
        {
            if (v.vehicleTransform != null && v.hasData)
            {
                // Apply movement relative to the SimulationAnchor (Local Space)
                Vector3 destination = new Vector3(v.targetPosition.x, v.initialY, v.targetPosition.z);
                v.vehicleTransform.localPosition = Vector3.Lerp(v.vehicleTransform.localPosition, destination, Time.deltaTime * lerpSpeed);
                v.vehicleTransform.localRotation = Quaternion.Lerp(v.vehicleTransform.localRotation, v.targetRotation, Time.deltaTime * lerpSpeed);
            }
        }
    }

    void ProcessMessage(string topic, string json)
    {
        if (!topic.Contains("/cam") && !topic.Contains("/mcm")) return;

        VehicleMapping vehicle = vehicles.Find(v => !string.IsNullOrEmpty(v.topicIdentifier) && topic.Contains(v.topicIdentifier));
        if (vehicle == null) return;

        if (topic.Contains("/cam")) ProcessCamMessage(vehicle, json);
        else if (topic.Contains("/mcm")) ProcessMcmMessage(vehicle, json);
    }

    void ProcessCamMessage(VehicleMapping vehicle, string json)
    {
        try
        {
            CamMessage camData = JsonUtility.FromJson<CamMessage>(json);
            var basic = camData?.camParameters?.basicContainer?.referencePosition;
            if (basic == null) return;

            vehicle.currentHeading = camData.camParameters.highFrequencyContainer?.basicVehicleContainerHighFrequency?.heading?.headingValue ?? 0f;
            vehicle.targetPosition = ConvertGpsToUnityPosition(basic.latitude, basic.longitude);
            vehicle.targetRotation = Quaternion.Euler(0f, vehicle.currentHeading, 0f);
            vehicle.hasData = true;
        }
        catch { }
    }

    void ProcessMcmMessage(VehicleMapping vehicle, string json)
    {
        try
        {
            McmRoot mcmData = JsonUtility.FromJson<McmRoot>(json);
            BasicContainer basic = mcmData?.GetBasicContainer();
            if (basic == null) return;

            if (basic.manoeuvreId == 1 && vehicle.pathVisualizer != null)
            {
                // Try to get real data first
                List<TrajectoryPoint> extractedTrajectory = mcmData.GetTrajectory();
                List<Vector3> pathPoints;

                if (extractedTrajectory != null && extractedTrajectory.Count > 0)
                {
                    // If real future points exist, use them
                    pathPoints = ExtractTrajectoryFromMcm(extractedTrajectory, vehicle);
                }
                else
                {
                    // If no future points exist in the JSON, fake the curve mathematically!
                    pathPoints = GenerateProceduralOvertakeCurve(vehicle);
                }

                vehicle.pathVisualizer.DrawPath(pathPoints);
            }
            else if (basic.manoeuvreId == 0 && vehicle.pathVisualizer != null)
            {
                vehicle.pathVisualizer.ClearPath();
            }
        }
        catch { }
    }

    List<Vector3> ExtractTrajectoryFromMcm(List<TrajectoryPoint> extractedTrajectory, VehicleMapping vehicle)
    {
        List<Vector3> unityPoints = new List<Vector3>();
        foreach (var point in extractedTrajectory)
        {
            Vector3 localPos = ConvertGpsToUnityPosition(point.latitude, point.longitude);
            
            Vector3 worldPos = vehicle.vehicleTransform.parent != null 
                ? vehicle.vehicleTransform.parent.TransformPoint(localPos) 
                : localPos;

            unityPoints.Add(worldPos);
        }
        return unityPoints;
    }

    // This mathematically generates the smooth curve based on your global settings
    List<Vector3> GenerateProceduralOvertakeCurve(VehicleMapping vehicle)
    {
        List<Vector3> unityPoints = new List<Vector3>();
        int segments = 30; // How smooth the curve is

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            // SmoothStep math for a buttery S-curve lane change
            float smoothT = t * t * (3f - 2f * t);

            float forwardDistance = t * maneuverLength;
            float leftDistance = -smoothT * laneWidth; 

            // 1. Create the point relative to the car (X is left/right, Z is forward)
            Vector3 localCurvePoint = new Vector3(leftDistance, 0f, forwardDistance);

            // 2. Convert it into World Space based on exactly where the car is facing right now
            Vector3 worldPos = vehicle.vehicleTransform.TransformPoint(localCurvePoint);
            
            unityPoints.Add(worldPos);
        }

        return unityPoints;
    }

    Vector3 ConvertGpsToUnityPosition(double lat, double lon)
    {
        if (!originSet) { originLat = lat; originLon = lon; originSet = true; }
        float z = (float)(lat - originLat) * 111320f;
        float x = (float)(lon - originLon) * (111320f * Mathf.Cos((float)originLat * Mathf.Deg2Rad));
        return new Vector3(x, 0f, z);
    }
}