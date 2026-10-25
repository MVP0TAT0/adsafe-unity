using UnityEngine;
using System.Collections.Generic;

public class UIProxyGenerator : MonoBehaviour
{
    [Header("Layer Setup")]
    public string uiLayerName = "UIDisplay"; // Repurposing your existing layer

    [Header("Road Proxies (Drag your _UI models here)")]
    public List<GameObject> roadProxyPrefabs;

    [Header("Actor Proxies")]
    public GameObject neutralVehicleProxyPrefab;
    public GameObject overtakingVehicleProxyPrefab;
    public GameObject pedestrianProxyPrefab;

    void Start()
    {
        int layerIndex = LayerMask.NameToLayer(uiLayerName);

        // 1. Generate Road Proxies
        GameObject[] roads = GameObject.FindGameObjectsWithTag("Road");
        foreach (GameObject road in roads)
        {
            string cleanName = road.name;

            // Strip out Unity's duplication numbers, e.g., " (2)" or " (68)"
            int parenthesisIndex = cleanName.IndexOf(" (");
            if (parenthesisIndex > 0)
            {
                cleanName = cleanName.Substring(0, parenthesisIndex);
            }

            // Also strip "(Clone)" just in case they are spawned via script later
            cleanName = cleanName.Replace("(Clone)", "").Trim();
            
            string expectedProxyName = cleanName + "_UI";

            // Search the list for a prefab matching the cleaned _UI name
            GameObject matchedPrefab = roadProxyPrefabs.Find(p => p.name == expectedProxyName);
            
            if (matchedPrefab != null)
            {
                InstantiateProxy(matchedPrefab, road.transform, layerIndex);
            }
            else
            {
                Debug.LogWarning("Missing UI Proxy for road: " + expectedProxyName);
            }
        }

        // 2. Generate Pedestrian Proxies
        GameObject[] pedestrians = GameObject.FindGameObjectsWithTag("Pedestrian");
        foreach (GameObject ped in pedestrians)
        {
            InstantiateProxy(pedestrianProxyPrefab, ped.transform, layerIndex);
        }

        // 3. Generate Neutral Vehicle Proxies
        GameObject[] neutralVehicles = GameObject.FindGameObjectsWithTag("Vehicle");
        foreach (GameObject veh in neutralVehicles)
        {
            InstantiateProxy(neutralVehicleProxyPrefab, veh.transform, layerIndex);
        }

        // 4. Generate Overtaking Vehicle Proxy
        GameObject[] overtakingVehicles = GameObject.FindGameObjectsWithTag("OvertakingVehicle");
        foreach (GameObject overtaking in overtakingVehicles)
        {
            InstantiateProxy(overtakingVehicleProxyPrefab, overtaking.transform, layerIndex);
        }
    }

    // Helper function to spawn the model, parent it, and fix its layer
    void InstantiateProxy(GameObject prefab, Transform parentObj, int layerIndex)
    {
        if (prefab == null) return;

        // Spawn the Blender model as a child of the real-world object
        GameObject proxy = Instantiate(prefab, parentObj);
        
        // Reset transforms so it perfectly overlaps the parent
        proxy.transform.localPosition = Vector3.zero;
        proxy.transform.localRotation = Quaternion.identity;

        // Ensure the proxy and all its sub-meshes are exclusively on the UIDisplay layer
        SetLayerRecursively(proxy, layerIndex);
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}