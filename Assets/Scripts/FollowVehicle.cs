using UnityEngine;

public class FollowVehicle : MonoBehaviour
{
    public VehicleManager manager;

    public int stationId;

    private Transform target;

    public Vector3 offset = new Vector3(0, 50, -100);

    public float smooth = 5f;

    void LateUpdate()
    {
        if (manager == null) return;

        Transform target =
           manager.GetVehicleVisual(stationId);
        if (target == null) return;
        Vector3 desiredPosition =
                   target.position + offset;

        transform.position =
            Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smooth);
        Vector3 flatForward = target.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation =
            Quaternion.LookRotation(flatForward);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                Time.deltaTime * smooth);
    }
}