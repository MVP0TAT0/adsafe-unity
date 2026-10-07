using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Settings")]
    public float moveSpeed = 10f;
    public float turnSpeed = 50f; // Increased for physics-based turning

    private Rigidbody rb;

    void Start()
    {
        // Automatically grab the Rigidbody component when the game starts
        rb = GetComponent<Rigidbody>();
    }

    // FixedUpdate is used instead of Update for all physics calculations
    void FixedUpdate()
    {
        float forwardInput = Input.GetAxis("Vertical"); 
        float turnInput = Input.GetAxis("Horizontal");  

        // 1. Forward/Backward Physical Movement
        // Calculate the physical push forward, but preserve the current Y velocity (gravity)
        Vector3 forwardMove = transform.forward * moveSpeed * forwardInput;
        rb.linearVelocity = new Vector3(forwardMove.x, rb.linearVelocity.y, forwardMove.z);

        // 2. Physical Steering
        if (forwardInput != 0)
        {
            float direction = Mathf.Sign(forwardInput);
            
            // Calculate a physical rotation and apply it
            Quaternion turnRotation = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime * direction, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
}