using UnityEngine;
using UnityEngine.EventSystems;

public class carCam : MonoBehaviour
{
    [Header("Target to follow")]
    public Transform target;

    [Header("Isometric View Settings")]
    public float height = 15f;
    public float verticalAngle = 45f;
    public float sideTiltAngle = 15f;
    public float followSpeed = 5f;

    [Header("Orbit Settings")]
    public float rotationSpeed = 5f;
    public float snapAngle = 90f;
    public float rotationLerpSpeed = 5f;
    public float angleTolerance = 5f;
    [Min(0f)] public float returnToDefaultDelay = 4f;
    public float uiClearanceTime = 0.15f; 

    [Header("Zoom Settings")]
    public float minDistance = 5f;
    public float maxDistance = 30f;
    public float defaultDistance = 15f; // ✅ Added this variable
    public float zoomSpeed = 10f;

    private float currentDistance;
    private float targetDistance;
    private float manualYaw;
    private float snapYaw;
    private bool isManual;

    private Quaternion smoothRotation;
    private Vector3 smoothVelocity;

    private float lastUIHoverTime;
    private float lastManualCameraInputTime;
    void Start()
    {
        // ✅ Changed these two lines to use defaultDistance instead of maxDistance
        currentDistance = defaultDistance; 
        targetDistance = defaultDistance;

        snapYaw = Mathf.Round(target.eulerAngles.y / snapAngle) * snapAngle;
        manualYaw = snapYaw;
        smoothRotation = Quaternion.Euler(verticalAngle, manualYaw + sideTiltAngle, 0f);
    }

    void LateUpdate()
    {
        if (!target || PrologueController.IsGameplayBlocked) return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (overUI)
            lastUIHoverTime = Time.time;

        bool uiBlocking = (Time.time - lastUIHoverTime) < uiClearanceTime;

        HandleRotation(!uiBlocking);

        UpdateCameraPosition();
    }

    void HandleRotation(bool allowInput)
    {
        float mouseMovement = Input.GetAxis("Mouse X");
        if (allowInput && Mathf.Abs(mouseMovement) > 0.0001f)
        {
            isManual = true;
            lastManualCameraInputTime = Time.time;
            manualYaw += mouseMovement * rotationSpeed;
        }
        else if (!isManual || Time.time - lastManualCameraInputTime >= returnToDefaultDelay)
        {
            isManual = false;

            float targetYaw = target.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(snapYaw, targetYaw);

            if (Mathf.Abs(yawDelta) >= snapAngle - angleTolerance)
                snapYaw = Mathf.Round(targetYaw / snapAngle) * snapAngle;

            manualYaw = Mathf.LerpAngle(manualYaw, snapYaw, Time.deltaTime * rotationLerpSpeed);
        }

        float chosenYaw = manualYaw;
        Quaternion desiredRotation = Quaternion.Euler(verticalAngle, chosenYaw + sideTiltAngle, 0f);
        smoothRotation = Quaternion.Slerp(smoothRotation, desiredRotation, Time.deltaTime * rotationLerpSpeed);
    }

    void UpdateCameraPosition()
    {
        Vector3 targetPos = target.position + Vector3.up * height;
        Vector3 direction = smoothRotation * Vector3.back;
        Vector3 desiredCamPos = targetPos + direction * currentDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredCamPos, ref smoothVelocity, 1f / followSpeed
        );

        transform.LookAt(target.position + Vector3.up * 2f);
    }
}
