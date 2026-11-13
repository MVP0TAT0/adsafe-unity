using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class CesiumPingPongTest : MonoBehaviour
{
    [Header("Cesium Configuration")]
    [SerializeField]
    private CesiumGeoreference cesiumGeoreference;

    [Header("GPS Waypoints (Coimbra University Area)")]
    // Point A: DEI Department Area — Longitude, Latitude, Height (metres)
    private double3 _coordinateA = new double3(-8.6543649, 40.62816398, 65.0);
    // Point B: Same position, higher altitude
    private double3 _coordinateB = new double3(-8.6543649, 40.62816398, 80.0);

    [Header("Movement Settings")]
    [SerializeField] private float speed = 0.5f;
    [SerializeField] private Vector3 cubeScale = new Vector3(5f, 5f, 5f);

    private GameObject _testCube;
    private CesiumGlobeAnchor _globeAnchor;

    void Start()
    {
        if (cesiumGeoreference == null)
        {
            Debug.LogError("[CesiumTest] CesiumGeoreference not assigned in the Inspector.", this);
            return;
        }

        // Create cube and parent it to the Georeference before adding any Cesium components
        _testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _testCube.name = "Dynamic_GPS_Test_Cube";
        _testCube.transform.SetParent(cesiumGeoreference.transform, false);
        _testCube.transform.localScale = cubeScale;

        // Colour the cube and disable occlusion culling
        MeshRenderer meshRenderer = _testCube.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            meshRenderer.material.color = Color.red;
            meshRenderer.allowOcclusionWhenDynamic = false;
        }



        // Expand mesh bounds to suppress frustum culling on fast-moving objects.
        // Use a large finite value — float.MaxValue causes NaN in Unity's culling math.
        MeshFilter meshFilter = _testCube.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            meshFilter.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f);
        }

        // Attach the Globe Anchor. detectTransformChanges = false because we drive
        // position ourselves via SetPositionLongitudeLatitudeHeight; enabling it would
        // create a feedback loop with Cesium's internal transform sync.
        _globeAnchor = _testCube.AddComponent<CesiumGlobeAnchor>();
        _globeAnchor.detectTransformChanges = false;

        _globeAnchor.SetPositionLongitudeLatitudeHeight(
            _coordinateA.x, _coordinateA.y, _coordinateA.z);

        Debug.Log("[CesiumTest] Test cube initialised.");
    }

    // LateUpdate ensures Cesium has finished its own Update transforms before we
    // overwrite the position, avoiding a one-frame positional lag.
    void LateUpdate()
    {
        if (_globeAnchor == null) return;

        // Smoothly oscillate between 0 and 1
        double lerpFactor = (math.sin(Time.time * speed) + 1.0) / 2.0;

        // Interpolate GPS coordinates — order is strictly (Longitude, Latitude, Height)
        double3 currentGps = math.lerp(_coordinateA, _coordinateB, lerpFactor);

        _globeAnchor.SetPositionLongitudeLatitudeHeight(
            currentGps.x, currentGps.y, currentGps.z);

            Debug.Log($"[CesiumTest] Cube position: {_testCube.transform.position}");
    }
}