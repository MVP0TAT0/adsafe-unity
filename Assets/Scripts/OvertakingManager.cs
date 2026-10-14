using UnityEngine;
using UnityEngine.UI;

public class OvertakingManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject notificationPanel; // The main UI panel containing the buttons
    public Image timeoutFillImage;       // The SVG fill layer you just created
    public Button allowButton;
    public Button denyButton;

    [Header("Negotiation Settings")]
    public float timeoutDuration = 5.0f; // Seconds before auto-deny
    
    private float timer;
    private bool isRequestActive = false;

    void Start()
    {
        // Tell the buttons what code to run when clicked by the VR hands
        allowButton.onClick.AddListener(OnAllowClicked);
        denyButton.onClick.AddListener(OnDenyClicked);

        // For now, immediately trigger the UI when the game starts so we can test it
        StartOvertakingRequest(); 
    }

void Update()
    {
        if (isRequestActive)
        {
            timer -= Time.deltaTime; // Count down the clock in real-time
            
            // Calculate percentage from 0 to 1, and apply it to the SVG fill
            float timePercentage = 1 - (timer / timeoutDuration);
            timeoutFillImage.fillAmount = timePercentage;

            // --- NEW: Keyboard Input ---
            // Allow overtaking with 'Y' or Right Arrow
            if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                OnAllowClicked();
            }
            // Deny overtaking with 'N' or Left Arrow
            else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow))
            {   
                OnDenyClicked();    
            }
            // ---------------------------

            if (timer <= 0)
            {
                OnTimeout();
            }
        }
    }

    public void StartOvertakingRequest()
    {
        isRequestActive = true;
        timer = timeoutDuration;
        timeoutFillImage.fillAmount = 0f; // Reset the fill visually
        notificationPanel.SetActive(true); // Show the UI
        Debug.Log("Overtaking Request Received.");
    }

    public void OnAllowClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking ALLOWED by driver.");
        EndRequest();
        // TODO: In the future, we will tell the other car to start its lane-change animation here
    }

    public void OnDenyClicked()
    {
        if (!isRequestActive) return;
        Debug.Log("Overtaking DENIED by driver.");
        EndRequest();
    }

    private void OnTimeout()
    {
        Debug.Log("Request TIMED OUT. Defaulting to Deny for safety.");
        EndRequest();
    }

    private void EndRequest()
    {
        isRequestActive = false;
        notificationPanel.SetActive(false); // Hide the UI panel completely
    }
}