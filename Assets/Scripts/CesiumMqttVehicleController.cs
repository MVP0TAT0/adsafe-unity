using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using CesiumForUnity;
using Unity.Mathematics;

public class CesiumMqttVehicleController : MonoBehaviour
{
    [Header("MQTT Connection")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;

    [Header("Cesium Environment")]
    [Tooltip("Drag the CesiumGeoreference object from your Hierarchy here")]
    public CesiumGeoreference georeference;
    
    [Tooltip("Cesium height is relative to sea level. Adjust this so cars don't spawn underground.")]
    public double roadHeightOffset = 45.0; 

    [System.Serializable]
    public class VehicleMapping
    {
        public string topicIdentifier;
        public CesiumGlobeAnchor globeAnchor;
        public Transform vehicleMesh;

        [HideInInspector] public double3 targetLLH;
        [HideInInspector] public Quaternion targetRotation;
        [HideInInspector] public float currentHeading;
        [HideInInspector] public bool hasData = false;
        [HideInInspector] public bool isInitialized = false;
    }

    [Header("Vehicle Setup")]
    public List<VehicleMapping> vehicles = new List<VehicleMapping>();
    public float lerpSpeed = 12f;

    private MqttClient client;
    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    private readonly object queueLock = new object();
    private bool originSet = false;

    void Start()
    {
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
            if (v.hasData && v.globeAnchor != null)
            {
                if (!v.isInitialized)
                {
                    // Snap instantly on the first frame to avoid flying across the globe
                    v.globeAnchor.longitudeLatitudeHeight = v.targetLLH;
                    v.isInitialized = true;
                }
                else
                {
                    // Lerp smoothly for subsequent frames using double precision
                    double3 currentLLH = v.globeAnchor.longitudeLatitudeHeight;
                    v.globeAnchor.longitudeLatitudeHeight = math.lerp(currentLLH, v.targetLLH, (double)(Time.deltaTime * lerpSpeed));
                }

                if (v.vehicleMesh != null)
                {
                    v.vehicleMesh.localRotation = Quaternion.Lerp(v.vehicleMesh.localRotation, v.targetRotation, Time.deltaTime * lerpSpeed);
                }
            }
        }
    }

    void ProcessMessage(string topic, string json)
    {
        if (!topic.Contains("/cam")) return;

        VehicleMapping vehicle = vehicles.Find(v => !string.IsNullOrEmpty(v.topicIdentifier) && topic.Contains(v.topicIdentifier));
        if (vehicle == null) return;

        try
        {
            CamMessage camData = JsonUtility.FromJson<CamMessage>(json);
            var basic = camData?.camParameters?.basicContainer?.referencePosition;
            if (basic == null) return;

            // 1. Shift the entire Unity World Origin to the very first GPS coordinate received
            if (!originSet && georeference != null)
            {
                georeference.SetOriginLongitudeLatitudeHeight(basic.longitude, basic.latitude, roadHeightOffset);
                originSet = true;
                Debug.Log($"Cesium Origin shifted to Portugal: {basic.latitude}, {basic.longitude}");
            }

            vehicle.currentHeading = camData.camParameters.highFrequencyContainer?.basicVehicleContainerHighFrequency?.heading?.headingValue ?? 0f;
            vehicle.targetLLH = new double3(basic.longitude, basic.latitude, roadHeightOffset); 
            vehicle.targetRotation = Quaternion.Euler(0f, vehicle.currentHeading, 0f);
            vehicle.hasData = true;
        }
        catch { }
    }

    void OnDestroy()
    {
        if (client != null && client.IsConnected) { try { client.Disconnect(); } catch {} }
    }
}