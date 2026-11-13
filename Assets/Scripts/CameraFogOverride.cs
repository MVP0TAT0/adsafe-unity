using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class CameraFogOverride : MonoBehaviour
{
    [Header("Fog Settings for this Camera")]
    public bool overrideFog = true;
    public bool fogEnabled = true;
    public Color fogColor = Color.gray;
    public float fogDensity = 0.02f;
    
    // Hidden variables to remember the global scene settings
    private bool originalFogState;
    private Color originalFogColor;
    private float originalFogDensity;

    private Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        // Subscribe to URP's rendering events
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    void OnDisable()
    {
        // Unsubscribe when destroyed to prevent memory leaks
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
    }

    private void OnBeginCamera(ScriptableRenderContext context, Camera renderingCamera)
    {
        // The millisecond THIS camera starts drawing the screen...
        if (renderingCamera == cam && overrideFog)
        {
            // 1. Save whatever the global scene fog is currently set to
            originalFogState = RenderSettings.fog;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;

            // 2. Inject this camera's custom fog
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera renderingCamera)
    {
        // The millisecond THIS camera finishes drawing...
        if (renderingCamera == cam && overrideFog)
        {
            // 3. Restore the global fog so other cameras aren't affected
            RenderSettings.fog = originalFogState;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
        }
    }
}