using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class ThreeWheelerController : MonoBehaviour
{
    [Header("Wheel Colliders (1 Front, 2 Rear)")]
    public WheelCollider frontWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Wheel Meshes")]
    public Transform frontWheelMesh;
    public Transform rearLeftWheelMesh;
    public Transform rearRightWheelMesh;

    [Header("Vehicle Specifications")]
    public float motorForce = 2000f;
    public float maxSteerAngle = 35f;
    public float brakeForce = 2500f;
    [Tooltip("Maximum driving speed in km/h. The motor stops adding torque once this speed is reached.")]
    public float maxDriveSpeedKmh = 70f;

    [Header("Automatic Hill-Climb Gearing")]
    [Tooltip("Maximum torque multiplier when the vehicle is slowly climbing a steep slope.")]
    public float maxHillClimbTorqueMultiplier = 15f;
    [Tooltip("Hill assistance fades out as the vehicle reaches this speed.")]
    public float hillClimbAssistSpeedKmh = 20f;
    [Tooltip("Minimum uphill slope before the low-gear boost starts. 0 is flat; 1 is vertical.")]
    [Range(0f, 1f)] public float hillClimbSlopeThreshold = 0.02f;

    [Header("Handling & Physics")]
    [Tooltip("Keep slightly high to maintain top-heavy momentum for cornering risk.")]
    public Vector3 centerOfMassOffset = new Vector3(0, -0.2f, 0); 
    public float leanForce = 1500f; 

    [Header("Automatic Terrain Stability Assist")]
    [Tooltip("Continuously rotates the vehicle upright relative to the ground beneath its wheels.")]
    public float terrainUprightForce = 8000f;
    [Tooltip("Continuously resists roll and pitch spin without damping steering/yaw.")]
    public float terrainAngularDamping = 450f;

    [Header("Engine Audio Settings")]
    public AudioSource engineAudioSource;
    [Tooltip("Pitch when resting at idle speed.")]
    public float minPitch = 0.75f;
    [Tooltip("Pitch when driving at top speed.")]
    public float maxPitch = 2.4f;
    [Tooltip("Top speed (in km/h) where engine pitch reaches its peak.")]
    public float maxSpeedKmh = 60f;
    [Tooltip("How fast the engine pitch revs up or down.")]
    public float revSmoothness = 6f;
    [Tooltip("Audio volume when idling.")]
    public float idleVolume = 0.35f;
    [Tooltip("Audio volume at max speed / acceleration.")]
    public float maxVolume = 0.9f;

    private float horizontalInput;
    private float verticalInput;
    private float leanInput;
    private bool isHandbraking;
    private bool dialogueHandbrakeActive;
    private bool vehicleLocked;
    private bool isCounterWeightModifierHeld;
    private Rigidbody rb;
    private RigidbodyConstraints constraintsBeforeVehicleLock;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset;

        // Auto-assign AudioSource if not manually referenced
        if (engineAudioSource == null)
        {
            engineAudioSource = GetComponent<AudioSource>();
        }

        // Initialize engine sound loop
        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.Play();
            }
        }
    }

    void Update()
    {
        GetInput();
        HandleEngineAudio();

        UpdateWheelVisuals(frontWheel, frontWheelMesh);
        UpdateWheelVisuals(rearLeftWheel, rearLeftWheelMesh);
        UpdateWheelVisuals(rearRightWheel, rearRightWheelMesh);
    }

    void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        HandleCounterWeight();
        HandleTerrainStabilityAssist();
    }

    private void GetInput()
    {
        if (PrologueController.IsGameplayBlocked)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            leanInput = 0f;
            isHandbraking = true;
            isCounterWeightModifierHeld = false;
            return;
        }

        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        
        // Dedicated handbrake key
        isHandbraking = Input.GetKey(KeyCode.Space) || dialogueHandbrakeActive || vehicleLocked;

        // Dialogue owns the vehicle until the conversation closes. Keeping the ordinary
        // handbrake path means parked vehicles stay physically planted on slopes as well.
        if (dialogueHandbrakeActive || vehicleLocked)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
        }

        // Terrain stability is automatic. Shift remains an optional counter-weight modifier.
        isCounterWeightModifierHeld = !dialogueHandbrakeActive && !vehicleLocked &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        // Lean / Counter-Weight logic (Shift + A/D)
        leanInput = 0f;
        if (isCounterWeightModifierHeld)
        {
            leanInput = Input.GetAxis("Horizontal"); 
        }
    }

    /// <summary>Lets conversation systems hold the vehicle's normal handbrake without faking input.</summary>
    public void SetDialogueHandbrake(bool active)
    {
        dialogueHandbrakeActive = active;
    }

    /// <summary>Freezes the completed vehicle in place until this is explicitly released.</summary>
    public void SetVehicleLocked(bool locked)
    {
        if (locked == vehicleLocked) return;
        if (rb == null) rb = GetComponent<Rigidbody>();

        vehicleLocked = locked;
        if (rb == null) return;

        if (locked)
        {
            constraintsBeforeVehicleLock = rb.constraints;
            horizontalInput = 0f;
            verticalInput = 0f;
            leanInput = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        else
        {
            rb.constraints = constraintsBeforeVehicleLock;
            rb.WakeUp();
        }
    }

    private void HandleMotor()
    {
        // Local forward speed (+ is forward, - is reverse)
        float forwardSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;
        float maxDriveSpeed = maxDriveSpeedKmh / 3.6f;

        float currentTorque = 0f;
        float currentBrake = 0f;
        float hillClimbAssist = 0f;

        if (isHandbraking)
        {
            currentBrake = brakeForce;
        }
        else if (verticalInput > 0) // Accelerating Forward (W / Up)
        {
            if (forwardSpeed < -0.5f) // If rolling backwards, apply brakes first
            {
                currentBrake = brakeForce;
            }
            else if (forwardSpeed < maxDriveSpeed) // Otherwise, accelerate until the speed cap
            {
                hillClimbAssist = GetHillClimbAssist(forwardSpeed);
                currentTorque = verticalInput * motorForce * Mathf.Lerp(1f, maxHillClimbTorqueMultiplier, hillClimbAssist);
            }
        }
        else if (verticalInput < 0) // Reversing / Braking (S / Down)
        {
            if (forwardSpeed > 0.5f) // If moving forward, apply brakes to slow down
            {
                currentBrake = brakeForce;
            }
            else if (forwardSpeed > -maxDriveSpeed) // Once stopped or rolling backward, reverse until the speed cap
            {
                hillClimbAssist = GetHillClimbAssist(forwardSpeed);
                currentTorque = verticalInput * motorForce * Mathf.Lerp(1f, maxHillClimbTorqueMultiplier, hillClimbAssist);
            }
        }

        // Apply torque to rear drive wheels
        rearLeftWheel.motorTorque = currentTorque;
        rearRightWheel.motorTorque = currentTorque;

        // Apply brakes across all wheels
        frontWheel.brakeTorque = currentBrake;
        rearLeftWheel.brakeTorque = currentBrake;
        rearRightWheel.brakeTorque = currentBrake;

    }

    private float GetHillClimbAssist(float forwardSpeed)
    {
        Vector3 groundNormal = Vector3.zero;
        int groundedWheelCount = 0;

        AddGroundNormal(frontWheel, ref groundNormal, ref groundedWheelCount);
        AddGroundNormal(rearLeftWheel, ref groundNormal, ref groundedWheelCount);
        AddGroundNormal(rearRightWheel, ref groundNormal, ref groundedWheelCount);

        if (groundedWheelCount == 0 || groundNormal.sqrMagnitude < Mathf.Epsilon)
        {
            return 1f;
        }

        groundNormal.Normalize();

        // A surface normal points slightly backward when the vehicle is facing uphill.
        // This also supports reversing uphill by using the requested drive direction.
        Vector3 driveDirection = transform.forward * Mathf.Sign(verticalInput);
        float uphillSteepness = Mathf.Max(0f, -Vector3.Dot(groundNormal, driveDirection));
        float slopeFactor = Mathf.InverseLerp(hillClimbSlopeThreshold, 1f, uphillSteepness);
        float assistSpeed = hillClimbAssistSpeedKmh / 3.6f;
        float strugglingFactor = 1f - Mathf.Clamp01(Mathf.Abs(forwardSpeed) / assistSpeed);

        return slopeFactor * strugglingFactor;
    }

    private void HandleSteering()
    {
        frontWheel.steerAngle = maxSteerAngle * horizontalInput;
    }

    private void HandleCounterWeight()
    {
        if (Mathf.Abs(leanInput) > 0.1f)
        {
            rb.AddRelativeTorque(Vector3.forward * leanInput * leanForce * -1f);
        }
    }

    private void HandleTerrainStabilityAssist()
    {
        Vector3 groundNormal = Vector3.zero;
        int groundedWheelCount = 0;

        AddGroundNormal(frontWheel, ref groundNormal, ref groundedWheelCount);
        AddGroundNormal(rearLeftWheel, ref groundNormal, ref groundedWheelCount);
        AddGroundNormal(rearRightWheel, ref groundNormal, ref groundedWheelCount);

        if (groundedWheelCount > 0 && groundNormal.sqrMagnitude > Mathf.Epsilon)
        {
            groundNormal.Normalize();
            Vector3 uprightCorrection = Vector3.Cross(transform.up, groundNormal);
            rb.AddTorque(uprightCorrection * terrainUprightForce, ForceMode.Force);
        }

        // Damp only roll/pitch angular velocity. Keeping the component around the vehicle's up
        // axis lets the vehicle still steer naturally.
        Vector3 rollAndPitchSpin = Vector3.ProjectOnPlane(rb.angularVelocity, transform.up);
        rb.AddTorque(-rollAndPitchSpin * terrainAngularDamping, ForceMode.Force);
    }

    private static void AddGroundNormal(WheelCollider wheel, ref Vector3 totalNormal, ref int groundedWheelCount)
    {
        if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
        {
            totalNormal += hit.normal;
            groundedWheelCount++;
        }
    }

    private void HandleEngineAudio()
    {
        if (engineAudioSource == null || engineAudioSource.clip == null) return;

        // Convert current velocity magnitude to km/h
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // Speed load vs throttle load blend
        float speedFactor = Mathf.Clamp01(speedKmh / maxSpeedKmh);
        float throttleFactor = Mathf.Abs(verticalInput);

        // Blended engine load: 70% actual wheel speed, 30% instant throttle input
        float engineLoad = Mathf.Clamp01(speedFactor * 0.7f + throttleFactor * 0.3f);

        // Calculate pitch and volume targets
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, engineLoad);
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, engineLoad);

        // Smooth pitch and volume changes to avoid abrupt audio pops
        engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.deltaTime * revSmoothness);
        engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVolume, Time.deltaTime * revSmoothness);
    }

    private void UpdateWheelVisuals(WheelCollider col, Transform mesh)
    {
        if (mesh == null) return;
        
        col.GetWorldPose(out Vector3 position, out Quaternion rotation);
        mesh.position = position;
        mesh.rotation = rotation;
    }
}
