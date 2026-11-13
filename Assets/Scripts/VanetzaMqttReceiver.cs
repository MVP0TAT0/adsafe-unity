using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using V2X.Protocol;

// Use prefixed type aliases to avoid collisions with un-namespaced classes in global namespace
using V2XCamMessage = V2X.Protocol.CamMessage;
using V2XMcmMessage = V2X.Protocol.McmMessage;
using V2XMcmContainer = V2X.Protocol.McmContainer;

public class VanetzaMqttReceiver : MonoBehaviour
{
    public VehicleManager vehicleManager;

    public LogLevel logLevel = LogLevel.Debug;
    public string mqttTopic = "+/vanetza/out/cam";
    public string mcmTopic = "+/vanetza/out/mcm";
    public string mcmInTopic = "+/vanetza/in/mcm";

    public string address = "127.0.0.1";
    public int port = 1883;

    private IMqttClient client;
    private bool isQuitting = false;
    private readonly object proposalOriginsLock = new object();
    private readonly Dictionary<string, McmPosition> proposalOrigins = new Dictionary<string, McmPosition>();

    async void Start()
    {
        Log.Mqtt.MinLevel = logLevel;
        Log.Mqtt.LogInfo(() => $"Starting MQTT client and connecting to broker at {address}:{port}");

        var factory = new MqttFactory();
        client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e =>
        {
            Log.Mqtt.LogDebug(() => $"Received message on topic: {e.ApplicationMessage.Topic}");
            string json = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            string[] jsonLines = json.Split('\n');
            string preview = string.Join("\n", jsonLines, 0, Mathf.Min(jsonLines.Length, 10));
            Log.Mqtt.LogDebug(() => $"JSON Message Preview (First 10 lines):\n{preview}");

            if (e.ApplicationMessage.Topic.EndsWith("/mcm", System.StringComparison.Ordinal))
            {
                bool isOutgoing = e.ApplicationMessage.Topic.EndsWith("/in/mcm", System.StringComparison.Ordinal);
                HandleMcmMessage(json, isOutgoing);
                return Task.CompletedTask;
            }

            V2XCamMessage msg = JsonUtility.FromJson<V2XCamMessage>(json);
            Log.Mqtt.LogDebug(() => $"Parsed CamMessage for station {msg}");

            int stationId = msg.fields.header.stationId;
            int stationType = msg.fields.cam.camParameters.basicContainer.stationType;
            double lat = msg.fields.cam.camParameters.basicContainer.referencePosition.latitude;
            double lon = msg.fields.cam.camParameters.basicContainer.referencePosition.longitude;

            double heading = msg.fields.cam.camParameters.highFrequencyContainer.basicVehicleContainerHighFrequency.heading.headingValue;
            double speed = msg.fields.cam.camParameters.highFrequencyContainer.basicVehicleContainerHighFrequency.speed.speedValue;
            
            Log.Mqtt.LogDebug(() => $"Received update for station {stationId} with:\n \t lat: {lat}, \n\t lon: {lon} \n\t heading: {heading} \n\t speed: {speed}");

            vehicleManager.QueueUpdate(stationId, stationType, lat, lon, heading, speed);

            return Task.CompletedTask;
        };

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(address, port)
            .Build();

        await client.ConnectAsync(options);
        await client.SubscribeAsync(mqttTopic);
        await client.SubscribeAsync(mcmTopic);
        await client.SubscribeAsync(mcmInTopic);
    }

    private void HandleMcmMessage(string json, bool isOutgoing)
    {
        V2XMcmMessage msg = JsonUtility.FromJson<V2XMcmMessage>(json);
        if (msg == null || msg.fields == null || msg.fields.header == null ||
            msg.fields.payload == null || msg.fields.payload.basicContainer == null ||
            msg.fields.payload.basicContainer.position == null)
        {
            Log.Mqtt.LogWarning("MCM message is missing its header or basic position data.");
            return;
        }

        McmBasicContainer basicContainer = msg.fields.payload.basicContainer;
        McmPosition origin = basicContainer.position;
        int stationId = msg.fields.header.stationId;

        McmVehicleManoeuvreContainer vehicleManoeuvreContainer =
            msg.fields.payload.mcmContainer != null
                ? msg.fields.payload.mcmContainer.vehicleManoeuvreContainer
                : null;
        McmVehicleCurrentStateContainer currentState =
            vehicleManoeuvreContainer != null
                ? vehicleManoeuvreContainer.vehicleCurrentStateContainer
                : null;

        if (!isOutgoing && currentState != null && currentState.vehicleSpeed != null && currentState.vehicleHeading != null)
        {
            vehicleManager.QueueUpdate(
                stationId,
                basicContainer.stationType,
                origin.latitude,
                origin.longitude,
                currentState.vehicleHeading.value,
                currentState.vehicleSpeed.speedValue);
        }

        switch (basicContainer.mcmType)
        {
            case 1:
                CacheProposalOrigin(basicContainer, origin, vehicleManoeuvreContainer);
                QueueProposalPaths(stationId, basicContainer, origin, vehicleManoeuvreContainer);
                break;
            case 2:
                McmPosition proposalOrigin = FindProposalOrigin(basicContainer.manoeuvreId, stationId);
                if (proposalOrigin == null)
                {
                    Log.Mqtt.LogWarning($"No proposal origin found for MCM response {basicContainer.manoeuvreId} from station {stationId}.");
                    break;
                }

                QueueResponsePaths(stationId, basicContainer, proposalOrigin, msg.fields.payload.mcmContainer);
                break;
            case 4:
                vehicleManager.QueueClearManeuverPaths(basicContainer.manoeuvreId);
                ClearProposalOrigins(basicContainer.manoeuvreId);
                break;
        }
    }

    private void CacheProposalOrigin(
        McmBasicContainer basicContainer,
        McmPosition origin,
        McmVehicleManoeuvreContainer vehicleManoeuvreContainer)
    {
        lock (proposalOriginsLock)
        {
            string defaultKey = GetProposalOriginKey(basicContainer.manoeuvreId, -1);
            proposalOrigins[defaultKey] = origin;

            if (vehicleManoeuvreContainer == null || vehicleManoeuvreContainer.manoeuvreAdvice == null)
                return;

            foreach (McmManoeuvreAdvice advice in vehicleManoeuvreContainer.manoeuvreAdvice)
            {
                if (advice != null)
                    proposalOrigins[GetProposalOriginKey(basicContainer.manoeuvreId, advice.executantID)] = origin;
            }
        }
    }

    private McmPosition FindProposalOrigin(int manoeuvreId, int responderStationId)
    {
        lock (proposalOriginsLock)
        {
            if (proposalOrigins.TryGetValue(GetProposalOriginKey(manoeuvreId, responderStationId), out McmPosition origin))
                return origin;

            proposalOrigins.TryGetValue(GetProposalOriginKey(manoeuvreId, -1), out origin);
            return origin;
        }
    }

    private void ClearProposalOrigins(int manoeuvreId)
    {
        string prefix = manoeuvreId + ":";
        lock (proposalOriginsLock)
        {
            List<string> keysToRemove = new List<string>();
            foreach (string key in proposalOrigins.Keys)
            {
                if (key.StartsWith(prefix, System.StringComparison.Ordinal))
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
                proposalOrigins.Remove(key);
        }
    }

    private string GetProposalOriginKey(int manoeuvreId, int participantStationId)
    {
        return $"{manoeuvreId}:{participantStationId}";
    }

    private void QueueProposalPaths(
        int stationId,
        McmBasicContainer basicContainer,
        McmPosition origin,
        McmVehicleManoeuvreContainer vehicleManoeuvreContainer)
    {
        int queuedPathCount = 0;
        if (vehicleManoeuvreContainer != null && vehicleManoeuvreContainer.submaneuvres != null)
        {
            foreach (McmSubManoeuvre submaneouvre in vehicleManoeuvreContainer.submaneuvres)
            {
                if (submaneouvre == null)
                    continue;

                string pathKey =
                    $"proposal:{stationId}:{basicContainer.manoeuvreId}:{submaneouvre.submanoeuvreID}";
                if (QueueTrajectory(pathKey, basicContainer.manoeuvreId, origin, submaneouvre.referenceTrajectory, false))
                    queuedPathCount++;
            }
        }

        if (queuedPathCount == 0 && vehicleManoeuvreContainer != null &&
            vehicleManoeuvreContainer.manoeuvreAdvice != null)
        {
            foreach (McmManoeuvreAdvice advice in vehicleManoeuvreContainer.manoeuvreAdvice)
            {
                if (advice == null || advice.submaneuvres == null)
                    continue;

                foreach (McmSubManoeuvre submaneouvre in advice.submaneuvres)
                {
                    if (submaneouvre == null)
                        continue;

                    string pathKey =
                        $"proposal:{stationId}:{basicContainer.manoeuvreId}:{advice.executantID}:{submaneouvre.submanoeuvreID}";
                    if (QueueTrajectory(pathKey, basicContainer.manoeuvreId, origin, submaneouvre.advisedTrajectory, false))
                        queuedPathCount++;
                }
            }
        }

        if (queuedPathCount == 0)
            Log.Mqtt.LogWarning($"MCM proposal {basicContainer.manoeuvreId} contains no drawable trajectory.");
    }

    private void QueueResponsePaths(
        int stationId,
        McmBasicContainer basicContainer,
        McmPosition origin,
        V2XMcmContainer mcmContainer)
    {
        int queuedPathCount = 0;
        McmSubManoeuvre[] submaneuvres =
            mcmContainer != null && mcmContainer.responseContainer != null
                ? mcmContainer.responseContainer.submaneuvres
                : null;

        if (submaneuvres != null)
        {
            foreach (McmSubManoeuvre submaneouvre in submaneuvres)
            {
                if (submaneouvre == null)
                    continue;

                string pathKey =
                    $"response:{stationId}:{basicContainer.manoeuvreId}:{submaneouvre.submanoeuvreID}";
                if (QueueTrajectory(pathKey, basicContainer.manoeuvreId, origin, submaneouvre.referenceTrajectory, true))
                    queuedPathCount++;
            }
        }

        if (queuedPathCount == 0)
            Log.Mqtt.LogWarning($"MCM response {basicContainer.manoeuvreId} contains no drawable trajectory.");
    }

    private bool QueueTrajectory(
        string pathKey,
        int manoeuvreId,
        McmPosition origin,
        McmTrajectory trajectory,
        bool isResponse)
    {
        if (trajectory == null || trajectory.wayPoints == null || trajectory.wayPoints.Length < 2)
            return false;

        var points = new List<double3>(trajectory.wayPoints.Length);
        foreach (McmPathWaypoint waypoint in trajectory.wayPoints)
        {
            if (waypoint == null || waypoint.pathPosition == null)
                continue;

            points.Add(new double3(
                origin.longitude + waypoint.pathPosition.deltaLongitude,
                origin.latitude + waypoint.pathPosition.deltaLatitude,
                64.0 + waypoint.pathPosition.deltaAltitude));
        }

        if (points.Count < 2)
            return false;

        vehicleManager.QueueManeuverPath(pathKey, manoeuvreId, points.ToArray(), isResponse);
        return true;
    }

    private async void OnApplicationQuit()
    {
        isQuitting = true;

        if (client != null)
        {
            try
            {
                await client.DisconnectAsync();
                client.Dispose();
            }
            catch (System.Exception e)
            {
                Debug.Log(e);
            }
        }
    }

    private async void OnDisable()
    {
        if (isQuitting) return;

        if (client != null)
        {
            try
            {
                await client.DisconnectAsync();
                client.Dispose();
            }
            catch { }
        }
    }
}