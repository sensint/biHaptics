using System.Collections.Specialized;
using UnityEngine;
//OVRPlugin.systemDisplayFrequency = 120.0f;

public class HapticGridController2 : MonoBehaviour
{
    [Header("Bin Settings")]
    [Range(1, 200)] public int horizontalBins = 100; // Slider for horizontal bins
    [Range(1, 200)] public int verticalBins = 50;   // Slider for vertical bins

    [Tooltip("Step size for bin numbers")]
    public int binStepSize = 10;

    void OnValidate()
    {
        horizontalBins = Mathf.RoundToInt(horizontalBins / (float)binStepSize) * binStepSize;
        verticalBins = Mathf.RoundToInt(verticalBins / (float)binStepSize) * binStepSize;
    }

    [Header("Movement Range")]
    public float maximumDistance = 1.0f; // Horizontal movement range
    public float verticalRange = 1.5f;   // Vertical movement range

    [Header("Haptic Settings")]
    public float VibrationFrequency = 100f; // Frequency of vibration
    public float VibrationDuration = 0.02f;      // Duration of vibration pulse
    public float VibrationAmplitude = 1.0f;
    private float minimumDistance = 0.05f;
    //private float maximumDistance = 1.0f;
    private float distanceBetweenControllers = 0f;

    [Header("Amplitude Mapping")]
    public float minimumAmplitude = 0.5f;
    public float maximumAmplitude = 1.0f;
    public AnimationCurve amplitudeMappingCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.0f);

    //[Header("Waveform Settings")]
    public enum WaveformType { Sine, Square, Sawtooth, Triangle }
    public WaveformType waveform = WaveformType.Sine;

    public enum MappingType { Linear, Logarithmic, Exponential }
    public MappingType amplitudeMappingType = MappingType.Linear;
    public MappingType binMappingType = MappingType.Linear;

    public int minimumBins = 10;
    public int maximumBins = 200;

    [Header("Controllers Setting")]
    public Transform leftHand;  // Reference to the left hand
    public Transform rightHand; // Reference to the right hand
    private float leftLastPosition;
    private float rightLastPosition;
    private float leftDistanceMoved;
    private float rightDistanceMoved;
    private bool rightControllerMoved = false;
    private bool leftControllerMoved = false;

    private bool isHapticsOnL = false; // Haptics state for left controller
    private bool isHapticsOnR = false; // Haptics state for right controller

    private Vector3 originalCubePosition;
    private Vector3 distanceBetweenControllersXYZ;
    private Vector3 lastDistanceBetweenControllersXYZ;
    private Quaternion relativeRotation;

    private int mappedBinId = 0;
    private int lastBinId = 0;
    private float vibrationStartTimeLeft = 0f;
    private float vibrationStartTimeRight = 0f;
    private float vibrationStartTimeBoth = 0f;
    private float kMovementThreshold = 0.005f;
    private bool isVibratingLeft = false;
    private bool isVibratingRight = false;
    private bool isVibratingBoth = false;
    private float mappedAmplitude = 0.5f;


    private void Start()
    {
        // Initialize the last known positions
        if (leftHand != null) leftLastPosition = leftHand.position.x;
        if (rightHand != null) rightLastPosition = rightHand.position.x;
    }

    void Update()
    {
        // Continuous Distance Measurement
        GetCoordinates();
        distanceBetweenControllers = Mathf.Clamp(distanceBetweenControllersXYZ.x, minimumDistance, maximumDistance);
        //Debug.Log(mappedAmplitude);
        //mappedBinId = Mathf.RoundToInt((distanceBetweenControllers - minimumDistance) * (horizontalBins - 0) / (maximumDistance - minimumDistance));
        ////mappedAmplitude = Mathf.RoundToInt((distanceBetweenControllers - minimumDistance) * (maximumAmplitude - minimumAmplitude) / (maximumDistance - minimumDistance) + minimumAmplitude);
        //float normalizedDistance = (distanceBetweenControllers - minimumDistance) / (maximumDistance - minimumDistance);
        //mappedAmplitude = amplitudeMappingCurve.Evaluate(normalizedDistance);
        //mappedAmplitude = Mathf.Clamp(mappedAmplitude, minimumAmplitude, maximumAmplitude);

        // Normalize distance for mapping functions
        float normalizedDistance = (distanceBetweenControllers - minimumDistance) / (maximumDistance - minimumDistance);

        // Choose mapping function for amplitude
        mappedAmplitude = MapAmplitude(normalizedDistance);
        mappedAmplitude = Mathf.Clamp(mappedAmplitude, minimumAmplitude, maximumAmplitude);

        // Debugging information
        Debug.Log($"Amplitude: {mappedAmplitude}, HorizontalBins: {horizontalBins}, MappedBinId: {mappedBinId}");

        // Adjust number of bins dynamically
        horizontalBins = Mathf.RoundToInt(MapBins(normalizedDistance));
        verticalBins = Mathf.RoundToInt(MapBins(normalizedDistance));

        // Map bin ID based on distance and updated bin count
        mappedBinId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));

        // Check input for toggling haptics
        HandleHapticsToggle(OVRInput.Controller.LTouch, ref isHapticsOnL);
        HandleHapticsToggle(OVRInput.Controller.RTouch, ref isHapticsOnR);

        // Check if the controller moved
        CheckLeftControllerMovement();
        CheckRightControllerMovement();

        // Run MCV
        if (isHapticsOnL && isHapticsOnR)
        {
            MotionCoupledVibrationBoth();
        }
        else if (isHapticsOnR && !isHapticsOnL)
        {
            MotionCoupledVibrationRight();
        }
        else if (isHapticsOnL && !isHapticsOnR)
        {
            MotionCoupledVibrationLeft();
        }
        leftLastPosition = leftHand.position.x;
        rightLastPosition = rightHand.position.x;
        isHapticsOnL = false;
        isHapticsOnR = false;
    }

    private void HandleHapticsToggle(OVRInput.Controller controller, ref bool isHapticsOn)
    {
        // Check if Primary Index Trigger is pressed
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller)){isHapticsOn = true;}
        else{isHapticsOn = false;}
    }

    private float ApplyWaveform(float baseAmplitude)
    {
        switch (waveform)
        {
            case WaveformType.Sine:
                return Mathf.Sin(Time.time * Mathf.PI * 2) * baseAmplitude;
            case WaveformType.Square:
                return Mathf.Sign(Mathf.Sin(Time.time * Mathf.PI * 2)) * baseAmplitude;
            case WaveformType.Sawtooth:
                return (Time.time % 1) * baseAmplitude;
            case WaveformType.Triangle:
                return (Mathf.Abs((Time.time % 1) * 2 - 1) * 2 - 1) * baseAmplitude;
            default:
                return baseAmplitude;
        }
    }

    // Map amplitude based on the selected mapping type
    private float MapAmplitude(float normalizedDistance)
    {
        switch (amplitudeMappingType)
        {
            case MappingType.Logarithmic:
                return Mathf.Log(1 + normalizedDistance * (Mathf.Exp(1) - 1)) * (maximumAmplitude - minimumAmplitude) + minimumAmplitude;
            case MappingType.Exponential:
                return Mathf.Pow(normalizedDistance, 2) * (maximumAmplitude - minimumAmplitude) + minimumAmplitude;
            case MappingType.Linear:
            default:
                return normalizedDistance * (maximumAmplitude - minimumAmplitude) + minimumAmplitude;
        }
    }

    // Map bins based on the selected mapping type
    private float MapBins(float normalizedDistance)
    {
        switch (binMappingType)
        {
            case MappingType.Logarithmic:
                return Mathf.Log(1 + normalizedDistance * (Mathf.Exp(1) - 1)) * (maximumBins - minimumBins) + minimumBins;
            case MappingType.Exponential:
                return Mathf.Pow(normalizedDistance, 2) * (maximumBins - minimumBins) + minimumBins;
            case MappingType.Linear:
            default:
                return normalizedDistance * (maximumBins - minimumBins) + minimumBins;
        }
    }

    private void GetCoordinates()
    {
        // Calculate distances along X, Y, Z axes
        Vector3 leftPosition = leftHand.position;
        Vector3 rightPosition = rightHand.position;
        distanceBetweenControllersXYZ = rightPosition - leftPosition;

        // Calculate relative rotation
        relativeRotation = Quaternion.Inverse(leftHand.rotation) * rightHand.rotation;
        Vector3 relativeAngles = relativeRotation.eulerAngles;

        //Debug.Log($"Distances - X: {distanceBetweenControllersXYZ.x:F4}, Y: {distanceBetweenControllersXYZ.y:F4}, Z: {distanceBetweenControllersXYZ.z:F4}");
        //Debug.Log($"Relative Angles - Pitch: {relativeAngles.x:F2}, Yaw: {relativeAngles.y:F2}, Roll: {relativeAngles.z:F2}");
    }

    private void CheckLeftControllerMovement()
    {
        // Calculate movement distance
        leftDistanceMoved = Mathf.Abs(leftLastPosition - leftHand.position.x);
        if (leftDistanceMoved > kMovementThreshold) // Threshold to ignore tiny movements
        {
            leftControllerMoved = true;
            //Debug.Log($"Left Controller Moved"); 
        }
        else { leftControllerMoved = false; }
    }

    private void CheckRightControllerMovement()
    {
        // Calculate movement distance
        rightDistanceMoved = Mathf.Abs(rightLastPosition - rightHand.position.x);
        //Debug.Log($"Right Distance: {rightDistanceMoved}");
        if (rightDistanceMoved > kMovementThreshold) // Threshold to ignore tiny movements
        {
            //Debug.Log($"Right Controller Moved");
            rightControllerMoved = true;
        }
        else { rightControllerMoved = false; }
    }

    /// Vibration Algorithms

    private void MotionCoupledVibrationLeft()
    {
        if (leftControllerMoved && mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingLeft)
            {
                StopVibration(OVRInput.Controller.LTouch);
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.LTouch, VibrationFrequency, mappedAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            lastDistanceBetweenControllersXYZ = distanceBetweenControllersXYZ;       
        }

        // Stop vibration after the pulse duration
        if (isVibratingLeft && Time.time - vibrationStartTimeLeft > VibrationDuration)
        {
            StopVibration(OVRInput.Controller.LTouch);
        }
    }

    private void MotionCoupledVibrationRight()
    {
        if (rightControllerMoved && mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingRight)
            {
                StopVibration(OVRInput.Controller.RTouch);
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.RTouch, VibrationFrequency, mappedAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            lastDistanceBetweenControllersXYZ = distanceBetweenControllersXYZ;
        }

        // Stop vibration after the pulse duration
        if (isVibratingRight && Time.time - vibrationStartTimeRight > VibrationDuration)
        {
            StopVibration(OVRInput.Controller.RTouch);
        }
    }

    private void MotionCoupledVibrationBoth()
    {
        if (mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingBoth)
            {
                StopVibrationBoth(OVRInput.Controller.RTouch);
                StopVibrationBoth(OVRInput.Controller.LTouch);
            }

            // Start vibration on both controllers
            StartVibrationBoth(OVRInput.Controller.RTouch, VibrationFrequency, mappedAmplitude);
            StartVibrationBoth(OVRInput.Controller.LTouch, VibrationFrequency, mappedAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            lastDistanceBetweenControllersXYZ = distanceBetweenControllersXYZ;
        }

        // Stop vibration after the pulse duration
        if (isVibratingBoth && Time.time - vibrationStartTimeBoth > VibrationDuration)
        {
            StopVibrationBoth(OVRInput.Controller.RTouch);
            StopVibrationBoth(OVRInput.Controller.LTouch);
        }
    }

    private void StartVibration(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        //vibrationStartTime = Time.time;
        //isVibratingRight = true;
        //isVibratingLeft = true;
        if (controller == OVRInput.Controller.RTouch) 
        { 
            isVibratingRight = true;
            vibrationStartTimeRight = Time.time;

        }
        if (controller == OVRInput.Controller.LTouch) 
        {
            isVibratingLeft = true;
            vibrationStartTimeLeft = Time.time;
        }
    }

    private void StartVibrationBoth(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        isVibratingBoth = true;
        vibrationStartTimeBoth = Time.time;
    }

    private void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        //isVibratingRight = false;
        //isVibratingLeft = false;
        if (controller == OVRInput.Controller.RTouch) { isVibratingRight = false;}
        if (controller == OVRInput.Controller.LTouch) { isVibratingLeft = false;}
    }

    private void StopVibrationBoth(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        isVibratingBoth = false;
    }
}
