using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class VehicleManager : MonoBehaviour
{
    [System.Serializable]
    public class VehicleColorOverride
    {
        public int stationId;
        public Color color = Color.white;
    }

    public LogLevel logLevel = LogLevel.Debug;
    public GameObject vehiclePrefab;
    public Transform vehiclesRoot;
    public List<VehicleColorOverride> vehicleColorOverrides = new List<VehicleColorOverride>();

    public double correctionStrength = 0.02;
    [Min(0f)]
    public float snapDistanceMeters = 100f;
    private Dictionary<int, CesiumGlobeAnchor> vehicles = new Dictionary<int, CesiumGlobeAnchor>();

    private Dictionary<int, double> speed = new Dictionary<int, double>();

    private Dictionary<int, double> heading = new Dictionary<int, double>();

    private Dictionary<int, double> lat = new Dictionary<int, double>();

    private Dictionary<int, double> lon = new Dictionary<int, double>();

    Dictionary<int, double> gpsLat = new Dictionary<int, double>();
    Dictionary<int, double> gpsLon = new Dictionary<int, double>();
    private Dictionary<int, float> groundOffset = new Dictionary<int, float>();

    private struct ManeuverPathUpdate
    {
        public string key;
        public int manoeuvreId;
        public double3[] positions;
        public bool isResponse;
        public bool clear;
    }

    private sealed class ManeuverPath
    {
        public int manoeuvreId;
        public double3[] positions;
        public LineRenderer lineRenderer;
    }

    private readonly object maneuverPathQueueLock = new object();
    private readonly Queue<ManeuverPathUpdate> maneuverPathQueue = new Queue<ManeuverPathUpdate>();
    private readonly Dictionary<string, ManeuverPath> maneuverPaths = new Dictionary<string, ManeuverPath>();
    private CesiumGeoreference cesiumGeoreference;
    private Material proposalPathMaterial;
    private Material responsePathMaterial;
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    public void Awake()
    {
        Log.Vehicle.MinLevel = logLevel;
        cesiumGeoreference = GetComponentInParent<CesiumGeoreference>();
        if (cesiumGeoreference == null)
            cesiumGeoreference = FindFirstObjectByType<CesiumGeoreference>();

        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null)
        {
            Log.Vehicle.LogError("Could not find the Universal Render Pipeline/Unlit shader for MCM paths.");
            return;
        }

        proposalPathMaterial = new Material(lineShader);
        proposalPathMaterial.color = new Color(1f, 0.55f, 0.05f);
        responsePathMaterial = new Material(lineShader);
        responsePathMaterial.color = new Color(0.05f, 0.8f, 1f);
    }

    public Transform GetVehicleVisual(int id)
    {
        if (vehicles == null)
            return null;
        vehicles.TryGetValue(id, out CesiumGlobeAnchor obj);
        if (obj == null)
            return null;
        VehicleReference reference = obj.GetComponent<VehicleReference>();

        Transform visual = reference.vehicleVisual;
        return visual;
    }

    public void UpdateVehicle(int stationId, int stationType, double lat, double lon, double heading, double speed)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon))
        {
            Log.Vehicle.LogWarning(() => $"NaN GPS ignored for {stationId}");
            //Debug.LogWarning($"NaN GPS ignored for {stationId}");
            return;
        }
        Log.Vehicle.LogDebug(() => $"Updating vehicle {stationId} to type: {stationType} lat: {lat}, lon: {lon}, heading: {heading}, speed: {speed}");

        if (!vehicles.ContainsKey(stationId))
        {
            bool originalPrefabState = vehiclePrefab.activeSelf;
            //vehiclePrefab.SetActive(false);
           // GameObject obj = Instantiate(vehiclePrefab, vehiclesRoot);

            GameObject templateVehicle = GameObject.Find("car_template");
GameObject obj = Instantiate(templateVehicle, vehiclesRoot);
obj.name = "car_" + stationId;
ApplyVehicleColorOverride(obj, stationId);
obj.SetActive(true);
 
//GameObject obj = GameObject.Find("car_" + stationId);
            obj.name = "Vehicle_" + stationId;
            obj.SetActive(true);
            

            CesiumGlobeAnchor anchor =
                obj.GetComponent<CesiumGlobeAnchor>();
            if (anchor == null)
            {
                Log.Vehicle.LogError(() => $"NO CESIUM GlOBE ANCHOR on vehicle prefab!");

            }
            else
            {
                Log.Vehicle.LogDebug(() => $"Anchor found on {obj.name}");

            }
            vehicles[stationId] = anchor;
            this.speed[stationId] = 0;
            this.heading[stationId] = 0;
            this.lat[stationId] = lat;
            this.lon[stationId] = lon;
            this.gpsLat[stationId] = lat;
            this.gpsLon[stationId] = lon;
            this.groundOffset[stationId] = 0f;

        }

        CesiumGlobeAnchor v = vehicles[stationId];

        //v.longitudeLatitudeHeight = new double3(lon, lat, 65.6);


        if (double.IsNaN(heading))
        {
            Log.Vehicle.LogWarning(() => $"NaN heading ignored for {stationId}");

            heading = 0;

        }

        this.speed[stationId] = speed;
        this.heading[stationId] = heading;

        double updateDistance = GetDistanceMeters(this.lat[stationId], this.lon[stationId], lat, lon);
        if (updateDistance > snapDistanceMeters)
        {
            Log.Vehicle.LogDebug(() =>
                $"Vehicle {stationId} update is {updateDistance:F1} m away; snapping to the new GPS position.");
            this.lat[stationId] = lat;
            this.lon[stationId] = lon;
        }

        this.gpsLat[stationId] = lat;
        this.gpsLon[stationId] = lon;

        VehicleLabel label = v.gameObject.GetComponent<VehicleLabel>();
        int speedKmh = (int)(speed * 3.6);
        label.text.text = $"ID:{stationId}\nType: {stationType}\nH:{heading:F1}º\nS:{speedKmh} km/h";

        Log.Vehicle.LogDebug(() => $"Vehicle {stationId} position updated to lat: {lat}, lon: {lon}. Heading: {heading}º, Speed: {speed} m/s");
        Log.Vehicle.LogDebug(() => $"Vehicle {stationId} anchor position: {v.longitudeLatitudeHeight}");
    }

    private static double GetDistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusMeters = 6371000.0;
        double latitudeDelta = (latitude2 - latitude1) * Mathf.Deg2Rad;
        double longitudeDelta = (longitude2 - longitude1) * Mathf.Deg2Rad;
        double latitude1Radians = latitude1 * Mathf.Deg2Rad;
        double latitude2Radians = latitude2 * Mathf.Deg2Rad;

        double haversine =
            System.Math.Pow(System.Math.Sin(latitudeDelta / 2.0), 2.0) +
            System.Math.Cos(latitude1Radians) * System.Math.Cos(latitude2Radians) *
            System.Math.Pow(System.Math.Sin(longitudeDelta / 2.0), 2.0);
        haversine = System.Math.Max(0.0, System.Math.Min(1.0, haversine));

        return 2.0 * earthRadiusMeters * System.Math.Asin(System.Math.Sqrt(haversine));
    }

    private void ApplyVehicleColorOverride(GameObject vehicle, int stationId)
    {
        VehicleColorOverride colorOverride = null;
        foreach (VehicleColorOverride entry in vehicleColorOverrides)
        {
            if (entry != null && entry.stationId == stationId)
            {
                colorOverride = entry;
                break;
            }
        }

        if (colorOverride == null)
            return;

        bool bodyFound = false;
        bool colorApplied = false;
        Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (!string.Equals(renderer.gameObject.name, "Body", System.StringComparison.Ordinal))
                continue;

            bodyFound = true;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(BaseColorProperty))
                    continue;

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, i);
                propertyBlock.SetColor(BaseColorProperty, colorOverride.color);
                renderer.SetPropertyBlock(propertyBlock, i);
                colorApplied = true;
            }
        }

        if (!bodyFound)
            Log.Vehicle.LogWarning(() => $"No Body renderer found on vehicle {stationId}; leaving the template color unchanged.");
        else if (!colorApplied)
            Log.Vehicle.LogWarning(() => $"The Body renderer for vehicle {stationId} has no _BaseColor property.");
    }

    private struct CamUpdate
    {
        public int id;
        public int stationType;
        public double lat;
        public double lon;
        public double heading;

        public double speed;
    }

    private readonly Queue<CamUpdate> queue = new();

    public void QueueUpdate(int id, int stationType, double lat, double lon, double heading, double speed)
    {
        Log.Vehicle.LogDebug(() => $"Queueing update for station {id} with station type {stationType}, lat: {lat}, lon: {lon} and heading: {heading}");
        lock (queue)
        {
            queue.Enqueue(new CamUpdate
            {
                id = id,
                stationType = stationType,
                lat = lat,
                lon = lon,
                heading = heading,
                speed = speed
            });
        }
    }

    public void QueueManeuverPath(string key, int manoeuvreId, double3[] positions, bool isResponse)
    {
        if (string.IsNullOrEmpty(key) || positions == null || positions.Length < 2)
            return;

        lock (maneuverPathQueueLock)
        {
            maneuverPathQueue.Enqueue(new ManeuverPathUpdate
            {
                key = key,
                manoeuvreId = manoeuvreId,
                positions = positions,
                isResponse = isResponse
            });
        }
    }

    public void QueueClearManeuverPaths(int manoeuvreId)
    {
        lock (maneuverPathQueueLock)
        {
            maneuverPathQueue.Enqueue(new ManeuverPathUpdate
            {
                manoeuvreId = manoeuvreId,
                clear = true
            });
        }
    }

    private void ProcessManeuverPathQueue()
    {
        lock (maneuverPathQueueLock)
        {
            while (maneuverPathQueue.Count > 0)
            {
                ManeuverPathUpdate update = maneuverPathQueue.Dequeue();
                if (update.clear)
                {
                    ClearManeuverPaths(update.manoeuvreId);
                    continue;
                }

                if (!maneuverPaths.TryGetValue(update.key, out ManeuverPath path))
                {
                    LineRenderer lineRenderer = CreateManeuverPathLine(update.key, update.isResponse);
                    if (lineRenderer == null)
                        continue;

                    path = new ManeuverPath
                    {
                        lineRenderer = lineRenderer
                    };
                    maneuverPaths.Add(update.key, path);
                }

                path.manoeuvreId = update.manoeuvreId;
                path.positions = update.positions;
            }
        }
    }

    private LineRenderer CreateManeuverPathLine(string key, bool isResponse)
    {
        if (cesiumGeoreference == null)
            cesiumGeoreference = FindFirstObjectByType<CesiumGeoreference>();
        if (cesiumGeoreference == null)
        {
            Log.Vehicle.LogError("Cannot display an MCM path without a CesiumGeoreference in the scene.");
            return null;
        }
        if (proposalPathMaterial == null || responsePathMaterial == null)
            return null;

        GameObject pathObject = new GameObject("MCM_" + key);
        pathObject.transform.SetParent(cesiumGeoreference.transform, false);

        LineRenderer lineRenderer = pathObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.sharedMaterial = isResponse ? responsePathMaterial : proposalPathMaterial;
        lineRenderer.startColor = isResponse ? responsePathMaterial.color : proposalPathMaterial.color;
        lineRenderer.endColor = lineRenderer.startColor;
        lineRenderer.widthMultiplier = 0.25f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.alignment = LineAlignment.View;
        return lineRenderer;
    }

    private void ClearManeuverPaths(int manoeuvreId)
    {
        List<string> keysToRemove = new List<string>();
        foreach (KeyValuePair<string, ManeuverPath> pair in maneuverPaths)
        {
            if (pair.Value.manoeuvreId == manoeuvreId)
            {
                Destroy(pair.Value.lineRenderer.gameObject);
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (string key in keysToRemove)
            maneuverPaths.Remove(key);
    }

    private void UpdateManeuverPathRenderers()
    {
        if (cesiumGeoreference == null)
            return;

        foreach (ManeuverPath path in maneuverPaths.Values)
        {
            Vector3[] positions = new Vector3[path.positions.Length];
            for (int i = 0; i < path.positions.Length; i++)
            {
                double3 earthCentered =
                    cesiumGeoreference.ellipsoid.LongitudeLatitudeHeightToCenteredFixed(path.positions[i]);
                double3 unityPosition =
                    cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(earthCentered);
                positions[i] = new Vector3(
                    (float)unityPosition.x,
                    (float)unityPosition.y,
                    (float)unityPosition.z);
            }

            path.lineRenderer.positionCount = positions.Length;
            path.lineRenderer.SetPositions(positions);
        }
    }

    private void OnDestroy()
    {
        foreach (ManeuverPath path in maneuverPaths.Values)
        {
            if (path.lineRenderer != null)
                Destroy(path.lineRenderer.gameObject);
        }
        maneuverPaths.Clear();

        if (proposalPathMaterial != null)
            Destroy(proposalPathMaterial);
        if (responsePathMaterial != null)
            Destroy(responsePathMaterial);
    }

    void LateUpdate()
    {
        lock (queue)
        {
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                UpdateVehicle(u.id, u.stationType, u.lat, u.lon, u.heading, u.speed);
            }
        }
        ProcessManeuverPathQueue();
        UpdateManeuverPathRenderers();

        foreach (var pair in vehicles)
        {
            int id = pair.Key;

            CesiumGlobeAnchor anchor = pair.Value;

            double speedMps = speed[id];

            double headingDeg = heading[id];

            double dt = Time.deltaTime;

            double distance =
                speedMps * dt;

            double headingRad =
                headingDeg * Mathf.Deg2Rad;

            double dLat =
                distance * System.Math.Cos(headingRad) / 111320.0;

            double dLon =
                distance * System.Math.Sin(headingRad) /
                (111320.0 * System.Math.Cos(lat[id] * Mathf.Deg2Rad));

            lat[id] += dLat;
            lon[id] += dLon;

            //Log.Vehicle.LogDebug(() => $"Simulated movement for station {id}: dLat: {dLat}, dLon: {dLon}, new lat: {lat[id]}, new lon: {lon[id]}");

double alpha = 1.0 - System.Math.Pow(1.0 - correctionStrength, dt);
lat[id] += (gpsLat[id] - lat[id]) * alpha;
lon[id] += (gpsLon[id] - lon[id]) * alpha;


            VehicleReference reference = anchor.GetComponent<VehicleReference>();

            Transform visual = reference.vehicleVisual;
            //anchor.longitudeLatitudeHeight = new double3(lon[id], lat[id], 64);
            anchor.SetPositionLongitudeLatitudeHeight(lon[id], lat[id], 64.0);

            //visual.localRotation = Quaternion.Euler(0f, (float)headingDeg, 0f);

            Quaternion targetRot = Quaternion.Euler(0f, (float)headingDeg, 0f);
visual.localRotation = Quaternion.Lerp(visual.localRotation, targetRot, (float)(10.0 * dt));
            //anchor.Sync();
        }

        
    }
}
