using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ManeuverPathVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("Data Slicing Controls")]
    [Tooltip("How many points to skip before we start drawing the line.")]
    public int startIndexOffset = 0;
    
    [Tooltip("How many points long the path should be.")]
    public int pointsToRender = 20;

    [Header("Visual Settings")]
    [Tooltip("Width of the path (matches car width)")]
    public float pathWidth = 2.5f;
    
    [Tooltip("Lifts the path slightly so it doesn't glitch through the asphalt")]
    public float groundClearance = 0.2f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        // Force the line to lay completely flat on the ground
        lineRenderer.alignment = LineAlignment.TransformZ;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); 
        lineRenderer.useWorldSpace = true;
    }

    // Call this from your MQTT script whenever you receive the trajectory array
    public void DrawPath(List<Vector3> incomingPoints)
    {
        if (incomingPoints == null || incomingPoints.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // Safely calculate the slice to prevent out-of-bounds errors
        int safeStart = Mathf.Clamp(startIndexOffset, 0, incomingPoints.Count - 1);
        int safeCount = Mathf.Min(pointsToRender, incomingPoints.Count - safeStart);

        lineRenderer.positionCount = safeCount;
        lineRenderer.startWidth = pathWidth;
        lineRenderer.endWidth = pathWidth;

        // Apply the sliced points to the line
        for (int i = 0; i < safeCount; i++)
        {
            Vector3 pt = incomingPoints[safeStart + i];
            pt.y += groundClearance; // Lift slightly above the road
            lineRenderer.SetPosition(i, pt);
        }
    }

    // Optional: Call this to hide the path when a maneuver is Denied
    public void ClearPath()
    {
        lineRenderer.positionCount = 0;
    }
}