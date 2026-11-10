using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OvertakingManager : MonoBehaviour
{
    [Header("UI Request Panel")]
    public GameObject notificationPanel; 
    public Image timeoutFillImage;       
    public Button allowButton;
    public Button denyButton;

    [Header("Feedback Panels")]
    public GameObject timeoutNotificationPanel; 
    public GameObject deniedNotificationPanel;  
    public float feedbackDisplayDuration = 3.0f; 

    [Header("Negotiation Settings")]
    public float timeoutDuration = 5.0f; 
    
    [Tooltip("Seconds to wait before showing the notification. Adjust this to sync with the replay.py dataset.")]
    public float delayBeforeRequest = 10.0f; 

    [Header("Camera Animation")]
    public Camera targetCamera;
    public float zoomOutFOV = 80f;
    [Tooltip("How high the camera should move on the Y axis to look over the dashboard/seat.")]
    public float zoomOutLocalY = 3f; 
    [Tooltip("How far back the camera should pull on the Z axis. Default is usually around -9.42.")]
    public float zoomOutLocalZ = -18f; 
    [Tooltip("How much the camera should tilt up or down on the X axis when zoomed out.")]
    public float zoomOutLocalRotationX = 10f; 
    public float zoomTransitionSpeed = 5f;

    private float defaultFOV;
    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;
    private Coroutine activeZoomCoroutine;

    private float timer;
    private bool isRequestActive = false;
    private Coroutine activeFeedbackCoroutine;

    void Start()
    {
        if (notificationPanel == null) Debug.LogWarning("Notification Panel is not assigned!");
        if (timeoutFillImage == null) Debug.LogWarning("Timeout Fill Image is not assigned!");
        
        if (targetCamera == null) 
        {
            Debug.LogWarning("Target Camera is not assigned for zooming!");
        }
        else 
        {
            // Remember the starting FOV, position, and rotation so we can always return to normal
            defaultFOV = targetCamera.fieldOfView; 
            defaultLocalPos = targetCamera.transform.localPosition;
            defaultLocalRot = targetCamera.transform.localRotation;
        }

        if (allowButton != null) allowButton.onClick.AddListener(OnAllowClicked);
        if (denyButton != null) denyButton.onClick.AddListener(OnDenyClicked);

        if (timeoutNotificationPanel != null) timeoutNotificationPanel.SetActive(false);
        if (deniedNotificationPanel != null) deniedNotificationPanel.SetActive(false);
        if (notificationPanel != null) notificationPanel.SetActive(false); // Start hidden

        // Start the delayed trigger to sync with the dataset
        if (delayBeforeRequest > 0)
        {
            StartCoroutine(DelayedStartRoutine());
        }
        else
        {
            StartOvertakingRequest();
        }
    }

    private IEnumerator DelayedStartRoutine()
    {
        yield return new WaitForSeconds(delayBeforeRequest);
        StartOvertakingRequest();
    }

    void Update()
    {
        // --- Debug Reset ---
        if (Input.GetKeyDown(KeyCode.R))
        {
            StopAllCoroutines(); 
            StartOvertakingRequest();
            return; 
        }

        if (isRequestActive)
        {
            timer -= Time.deltaTime;

            if (timeoutFillImage != null)
            {
                float timePercentage = 1 - (timer / timeoutDuration);
                timeoutFillImage.fillAmount = timePercentage;
            }

            if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.LeftArrow)) OnAllowClicked();
            else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) OnDenyClicked();

            if (timer <= 0) OnTimeout();
        }
    }

    public void StartOvertakingRequest()
    {
        if (activeFeedbackCoroutine != null) StopCoroutine(activeFeedbackCoroutine);
        if (timeoutNotificationPanel != null) timeoutNotificationPanel.SetActive(false);
        if (deniedNotificationPanel != null) deniedNotificationPanel.SetActive(false);

        isRequestActive = true;
        timer = timeoutDuration;
        
        if (timeoutFillImage != null) timeoutFillImage.fillAmount = 0f;
        if (notificationPanel != null) notificationPanel.SetActive(true);
        
        // Trigger Zoom Out, Move, and Rotate
        if (targetCamera != null)
        {
            Vector3 targetPos = new Vector3(defaultLocalPos.x, zoomOutLocalY, zoomOutLocalZ);
            // Create a new target rotation keeping the original Y and Z, but applying the new X tilt
            Quaternion targetRot = Quaternion.Euler(zoomOutLocalRotationX, defaultLocalRot.eulerAngles.y, defaultLocalRot.eulerAngles.z);
            TriggerCameraTransition(zoomOutFOV, targetPos, targetRot);
        }
        
        Debug.Log("Overtaking Request Started.");
    }

    public void OnAllowClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking ALLOWED. Camera stays zoomed out.");
        EndRequest(false);
    }

    public void OnDenyClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking DENIED. Camera zooms back in.");
        EndRequest(true);
        ShowFeedback(deniedNotificationPanel);
    }

    private void OnTimeout()
    {
        Debug.Log("Request TIMED OUT. Camera zooms back in.");
        EndRequest(true);
        ShowFeedback(timeoutNotificationPanel);
    }

    private void EndRequest(bool resetCamera)
    {
        isRequestActive = false;
        if (notificationPanel != null) notificationPanel.SetActive(false); 
        
        if (resetCamera)
        {
            TriggerCameraTransition(defaultFOV, defaultLocalPos, defaultLocalRot);
        }
    }

    private void ShowFeedback(GameObject panel)
    {
        if (panel == null) return;
        if (activeFeedbackCoroutine != null) StopCoroutine(activeFeedbackCoroutine);
        activeFeedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(panel));
    }

    private IEnumerator ShowFeedbackRoutine(GameObject panelToDisplay)
    {
        panelToDisplay.SetActive(true);
        yield return new WaitForSeconds(feedbackDisplayDuration);
        panelToDisplay.SetActive(false);
        activeFeedbackCoroutine = null;
    }

    private void TriggerCameraTransition(float targetFOV, Vector3 targetPos, Quaternion targetRot)
    {
        if (targetCamera == null) return;
        if (activeZoomCoroutine != null) StopCoroutine(activeZoomCoroutine);
        activeZoomCoroutine = StartCoroutine(SmoothCameraRoutine(targetFOV, targetPos, targetRot));
    }

    private IEnumerator SmoothCameraRoutine(float targetFOV, Vector3 targetPos, Quaternion targetRot)
    {
        // Added Quaternion.Angle to ensure the loop continues until the rotation finishes
        while (Mathf.Abs(targetCamera.fieldOfView - targetFOV) > 0.1f || 
               Vector3.Distance(targetCamera.transform.localPosition, targetPos) > 0.01f ||
               Quaternion.Angle(targetCamera.transform.localRotation, targetRot) > 0.1f)
        {
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * zoomTransitionSpeed);
            targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, targetPos, Time.deltaTime * zoomTransitionSpeed);
            targetCamera.transform.localRotation = Quaternion.Lerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * zoomTransitionSpeed);
            yield return null;
        }
        
        targetCamera.fieldOfView = targetFOV;
        targetCamera.transform.localPosition = targetPos;
        targetCamera.transform.localRotation = targetRot;
        activeZoomCoroutine = null;
    }
}