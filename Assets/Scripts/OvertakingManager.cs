using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OvertakingManager : MonoBehaviour
{
    [Header("Central Display UI")]
    public GameObject notificationPanel; 
    public Image timeoutFillImage;       
    public Button allowButton;
    public Button denyButton;
    public GameObject timeoutNotificationPanel; 
    public GameObject deniedNotificationPanel;  

    [Header("Driver Display UI (Behind Wheel)")]
    public GameObject driverNotificationPanel;
    public Image driverTimeoutFillImage;
    public GameObject driverTimeoutNotificationPanel;
    public GameObject driverDeniedNotificationPanel;

    [Header("Overtaking Visuals")]
    [Tooltip("Drag the Arrow Canvas Image here. Make sure it is NOT a child of the Notification Panels.")]
    public GameObject blinkingArrow;
    [Tooltip("How fast the arrow blinks (lower is faster)")]
    public float blinkSpeed = 0.5f;

    [Header("Negotiation Settings")]
    public float timeoutDuration = 5.0f; 
    public float feedbackDisplayDuration = 3.0f; 
    
    [Tooltip("Seconds to wait before showing the notification. Adjust this to sync with the replay.py dataset.")]
    public float delayBeforeRequest = 10.0f; 

    [Header("Camera Animation")]
    public Camera targetCamera;
    public float zoomOutFOV = 80f;
    public float zoomOutLocalY = 3f; 
    public float zoomOutLocalZ = -18f; 
    public float zoomOutLocalRotationX = 10f; 
    public float zoomTransitionSpeed = 5f;

    private float defaultFOV;
    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;
    private Coroutine activeZoomCoroutine;

    private float timer;
    private bool isRequestActive = false;
    private Coroutine activeFeedbackCoroutine;
    private Coroutine activeBlinkCoroutine;

    void Start()
    {
        if (notificationPanel == null) Debug.LogWarning("Central Notification Panel is not assigned!");
        if (timeoutFillImage == null) Debug.LogWarning("Central Timeout Fill Image is not assigned!");
        
        if (targetCamera == null) 
        {
            Debug.LogWarning("Target Camera is not assigned for zooming!");
        }
        else 
        {
            defaultFOV = targetCamera.fieldOfView; 
            defaultLocalPos = targetCamera.transform.localPosition;
            defaultLocalRot = targetCamera.transform.localRotation;
        }

        if (allowButton != null) allowButton.onClick.AddListener(OnAllowClicked);
        if (denyButton != null) denyButton.onClick.AddListener(OnDenyClicked);

        // Hide all panels on startup
        if (timeoutNotificationPanel != null) timeoutNotificationPanel.SetActive(false);
        if (deniedNotificationPanel != null) deniedNotificationPanel.SetActive(false);
        if (notificationPanel != null) notificationPanel.SetActive(false); 

        if (driverTimeoutNotificationPanel != null) driverTimeoutNotificationPanel.SetActive(false);
        if (driverDeniedNotificationPanel != null) driverDeniedNotificationPanel.SetActive(false);
        if (driverNotificationPanel != null) driverNotificationPanel.SetActive(false); 
        
        if (blinkingArrow != null) blinkingArrow.SetActive(false);

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

            float timePercentage = 1 - (timer / timeoutDuration);

            if (timeoutFillImage != null) timeoutFillImage.fillAmount = timePercentage;
            if (driverTimeoutFillImage != null) driverTimeoutFillImage.fillAmount = timePercentage;

            if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.LeftArrow)) OnAllowClicked();
            else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) OnDenyClicked();

            if (timer <= 0) OnTimeout();
        }
    }

    public void StartOvertakingRequest()
    {
        if (activeFeedbackCoroutine != null) StopCoroutine(activeFeedbackCoroutine);
        
        // Hide feedbacks
        if (timeoutNotificationPanel != null) timeoutNotificationPanel.SetActive(false);
        if (deniedNotificationPanel != null) deniedNotificationPanel.SetActive(false);
        if (driverTimeoutNotificationPanel != null) driverTimeoutNotificationPanel.SetActive(false);
        if (driverDeniedNotificationPanel != null) driverDeniedNotificationPanel.SetActive(false);

        isRequestActive = true;
        timer = timeoutDuration;
        
        // Reset and show requests
        if (timeoutFillImage != null) timeoutFillImage.fillAmount = 0f;
        if (driverTimeoutFillImage != null) driverTimeoutFillImage.fillAmount = 0f;
        
        if (notificationPanel != null) notificationPanel.SetActive(true);
        if (driverNotificationPanel != null) driverNotificationPanel.SetActive(true);

        // Start Arrow Blinking
        if (blinkingArrow != null)
        {
            blinkingArrow.SetActive(true);
            if (activeBlinkCoroutine != null) StopCoroutine(activeBlinkCoroutine);
            activeBlinkCoroutine = StartCoroutine(BlinkRoutine());
        }
        
        // Trigger Zoom Out, Move, and Rotate
        if (targetCamera != null)
        {
            Vector3 targetPos = new Vector3(defaultLocalPos.x, zoomOutLocalY, zoomOutLocalZ);
            Quaternion targetRot = Quaternion.Euler(zoomOutLocalRotationX, defaultLocalRot.eulerAngles.y, defaultLocalRot.eulerAngles.z);
            TriggerCameraTransition(zoomOutFOV, targetPos, targetRot);
        }
        
        Debug.Log("Overtaking Request Started.");
    }

    public void OnAllowClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking ALLOWED. Arrow continues blinking.");
        
        // Do NOT stop the arrow here. It will keep blinking automatically.
        EndRequest(false);
    }

    public void OnDenyClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking DENIED. Arrow hidden.");
        
        StopBlinkingArrow();
        EndRequest(true);
        ShowFeedback(deniedNotificationPanel, driverDeniedNotificationPanel);
    }

    private void OnTimeout()
    {
        Debug.Log("Request TIMED OUT. Arrow hidden.");
        
        StopBlinkingArrow();
        EndRequest(true);
        ShowFeedback(timeoutNotificationPanel, driverTimeoutNotificationPanel);
    }

    private void EndRequest(bool resetCamera)
    {
        isRequestActive = false;
        if (notificationPanel != null) notificationPanel.SetActive(false); 
        if (driverNotificationPanel != null) driverNotificationPanel.SetActive(false);
        
        if (resetCamera)
        {
            TriggerCameraTransition(defaultFOV, defaultLocalPos, defaultLocalRot);
        }
    }

    private void StopBlinkingArrow()
    {
        if (activeBlinkCoroutine != null)
        {
            StopCoroutine(activeBlinkCoroutine);
            activeBlinkCoroutine = null;
        }
        if (blinkingArrow != null) blinkingArrow.SetActive(false);
    }

    private IEnumerator BlinkRoutine()
    {
        // Automatically adds a CanvasGroup to the arrow if it doesn't have one, 
        // which allows us to safely fade its transparency.
        CanvasGroup arrowGroup = blinkingArrow.GetComponent<CanvasGroup>();
        if (arrowGroup == null) arrowGroup = blinkingArrow.AddComponent<CanvasGroup>();

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime / blinkSpeed;
            // Uses a Cosine wave for a buttery smooth fade in and fade out
            arrowGroup.alpha = (Mathf.Cos(t * Mathf.PI * 2f) * 0.5f) + 0.5f;
            yield return null;
        }
    }

    private void ShowFeedback(GameObject centralPanel, GameObject driverPanel)
    {
        if (activeFeedbackCoroutine != null) StopCoroutine(activeFeedbackCoroutine);
        activeFeedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(centralPanel, driverPanel));
    }

    private IEnumerator ShowFeedbackRoutine(GameObject centralPanel, GameObject driverPanel)
    {
        if (centralPanel != null) centralPanel.SetActive(true);
        if (driverPanel != null) driverPanel.SetActive(true);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        if (centralPanel != null) centralPanel.SetActive(false);
        if (driverPanel != null) driverPanel.SetActive(false);
        
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