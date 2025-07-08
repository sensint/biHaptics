using System.Collections.Specialized;
using UnityEngine;
//OVRPlugin.systemDisplayFrequency = 120.0f;

public class HapticGridControllerTranslation : MonoBehaviour
{
    /// <summary>
    /// Visuals in Unity Editor
    /// </summary>
    [Header("Bin Settings")]
    [Range(1, 200)] public int horizontalBins = 100; // Slider for horizontal bins
    [Range(1, 200)] public int verticalBins = 50;   // Slider for vertical bins

    [Tooltip("Step size for bin numbers")]
    public int binStepSize = 10;

    void OnValidate()
    {
        horizontalBins = Mathf.RoundToInt(horizontalBins / (float)binStepSize) * binStepSize;
        //verticalBins = Mathf.RoundToInt(verticalBins / (float)binStepSize) * binStepSize;
    }

    [Header("Movement Range")]
    public float minimumDistance = 0.01f;
    public float maximumDistance = 1.0f; // Horizontal movement range

    [Header("Haptic Settings")]
    public float VibrationFrequency = 125f; // Frequency of vibration
    public float VibrationDuration = 0.04f;      // Duration of vibration pulse
    public float VibrationAmplitude = 0.1f;
    private float distanceBetweenControllers = 0f;
   

    [Header("Amplitude Mapping")]
    public float minimumAmplitude = 0.5f;
    public float maximumAmplitude = 1.0f;
    //public AnimationCurve amplitudeMappingCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.0f);

    //[Header("Waveform Settings")]
    public enum WaveformType { Sine, Square, Sawtooth, Triangle }
    public WaveformType waveform = WaveformType.Sine;

    //[Header("Mapping Settings")]
    public enum MappingType { Linear, Logarithmic, Exponential }
    public MappingType amplitudeMappingType = MappingType.Linear;
    public MappingType binMappingType = MappingType.Linear;
    public int minimumBins = 10;
    public int maximumBins = 200;

    [Header("Controllers Setting")]
    public Transform leftHand;  // Reference to the left hand
    public Transform rightHand; // Reference to the right hand
   

    /// <summary>
    /// Detecting Movement of Hand Controllers
    /// </summary>
    private float leftLastPosition;
    private float rightLastPosition;
    private float leftDistanceMoved;
    private float rightDistanceMoved;
    private bool rightControllerMoved = false;
    private bool leftControllerMoved = false;

    /// <summary>
    /// Haptic State for Controllers
    /// </summary>
    private bool isTriggeredPressedLeft = false;
    private bool isTriggeredPressedRight = false;
    private bool isVibratingLeft = false;
    private bool isVibratingRight = false;
    private bool isVibratingBoth = false;

    private Vector3 originalCubePosition;

    /// <summary>
    /// Distance between Controllers + Mapping Details
    /// </summary>
    private Vector3 distanceBetweenControllersXYZ;
    private Vector3 lastDistanceBetweenControllersXYZ;
    private int mappedBinId = 0;
    private int lastBinId = 0;
    private float vibrationStartTimeLeft = 0f;
    private float vibrationStartTimeRight = 0f;
    private float vibrationStartTimeBoth = 0f;
    private float kMovementThreshold = 0.005f;
    private float kJitterThreshold = 0.01f;
    private float mappedAmplitude = 0.5f;

    [Header ("Amplitude Control")]
    [Tooltip("Amplitude adjustment step size")]
    public float amplitudeStep = 0.1f;

    private void Start()
    {
        // Initialize the last known positions
        if (leftHand != null) leftLastPosition = leftHand.position.x;
        if (rightHand != null) rightLastPosition = rightHand.position.x;
        lastDistanceBetweenControllersXYZ = distanceBetweenControllersXYZ;
    }

    void Update()
    {
        HandleAmplitudeControl();
        HandleBinControl();
        // Continuous Distance Measurement
        GetCoordinates();
        distanceBetweenControllers = Mathf.Clamp(distanceBetweenControllersXYZ.x, minimumDistance, maximumDistance);
        //mappedBinId = Mathf.RoundToInt((distanceBetweenControllers - minimumDistance) * (horizontalBins - 0) / (maximumDistance - minimumDistance));

        // Normalize distance for mapping functions
        float normalizedDistance = (distanceBetweenControllers - minimumDistance) / (maximumDistance - minimumDistance);

        // Choose mapping function for amplitude and bins
        mappedAmplitude = MapAmplitude(normalizedDistance);
        mappedAmplitude = Mathf.Clamp(mappedAmplitude, minimumAmplitude, maximumAmplitude);// Adjust number of bins dynamically
        horizontalBins = Mathf.RoundToInt(MapBins(normalizedDistance));
        mappedBinId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));

        //Debug.Log($"Amplitude: {mappedAmplitude}, HorizontalBins: {horizontalBins}, MappedBinId: {mappedBinId}");

        // Check input for toggling haptics
        HandleHapticsToggle(OVRInput.Controller.LTouch, ref isTriggeredPressedLeft);
        HandleHapticsToggle(OVRInput.Controller.RTouch, ref isTriggeredPressedRight);

        // Check if the controller moved
        CheckLeftControllerMovement();
        CheckRightControllerMovement();

        float netChange = Mathf.Abs(lastDistanceBetweenControllersXYZ.x - distanceBetweenControllersXYZ.x);
        if (netChange < kJitterThreshold)
        {
            StopVibrationBoth(OVRInput.Controller.RTouch);
            StopVibrationBoth(OVRInput.Controller.LTouch);
            return;
        }

        // Run MCV
        if (isTriggeredPressedLeft && isTriggeredPressedRight)
        {
            MotionCoupledVibrationBoth();
        }
        else if (isTriggeredPressedRight && !isTriggeredPressedLeft)
        {
            if (rightControllerMoved)
            {
                MotionCoupledVibrationRight();
            }
            else { StopVibration(OVRInput.Controller.RTouch); }
        }
        else if (isTriggeredPressedLeft && !isTriggeredPressedRight)
        {
            if (leftControllerMoved)
            {
                MotionCoupledVibrationLeft();
            }
            else { StopVibration(OVRInput.Controller.LTouch); }
        }
        else
        {
            isTriggeredPressedLeft = false;
            isTriggeredPressedRight = false;
            StopVibrationBoth(OVRInput.Controller.RTouch);
            StopVibrationBoth(OVRInput.Controller.LTouch);
        }

        leftLastPosition = leftHand.position.x;
        rightLastPosition = rightHand.position.x;
    }
   
    
    private void HandleAmplitudeControl(){
        if (Input.GetKeyDown(KeyCode.UpArrow)){
            // horizontalAmplitude += amplitudeStep;
            // verticalAmplitude += amplitudeStep;
            VibrationAmplitude = Mathf.Min(1, VibrationAmplitude + amplitudeStep);
            Debug.Log($"Increased Amp : Min{VibrationAmplitude}, Amplitude");
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow)){
            // horizontalAmplitude = Mathf.Max(0, horizontalAmplitude - amplitudeStep);
            // verticalAmplitude = Mathf.Max(0, verticalAmplitude - amplitudeStep);
            VibrationAmplitude = Mathf.Max(0, VibrationAmplitude - amplitudeStep);
            Debug.Log($"Decreased Amp: {VibrationAmplitude}, Amplitude");
        }
    }

    private void HandleBinControl(){
        if(Input.GetKeyDown(KeyCode.A)){
            horizontalBins = Mathf.Min(200, horizontalBins + binStepSize);
        }
        else if (Input.GetKeyDown(KeyCode.D)){
            horizontalBins = Mathf.Max(0, horizontalBins - binStepSize);
        }
        else if (Input.GetKeyDown(KeyCode.W)){
            verticalBins = Mathf.Max(200, verticalBins + binStepSize);
        }
        else if (Input.GetKeyDown(KeyCode.S)){
            verticalBins = Mathf.Max(0, verticalBins - binStepSize);
        }
    }


    private void HandleHapticsToggle(OVRInput.Controller controller, ref bool isTriggerPressed)
    {
        // Check if Primary Index Trigger is pressed
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller)){ isTriggerPressed = true;}
        else{ isTriggerPressed = false;}
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
        //Debug.Log($"Distances - X: {distanceBetweenControllersXYZ.x:F4}, Y: {distanceBetweenControllersXYZ.y:F4}, Z: {distanceBetweenControllersXYZ.z:F4}");
    }

    private void CheckLeftControllerMovement()
    {
        // Calculate movement distance
        leftDistanceMoved = Mathf.Abs(leftLastPosition - leftHand.position.x);
        if (leftDistanceMoved > kMovementThreshold) // Threshold to ignore tiny movements
        {
            leftControllerMoved = true;
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
            //leftLastPosition = leftHand.position.x;
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
        if (mappedBinId != lastBinId)
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
            //rightLastPosition = rightHand.position.x;
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
        if (controller == OVRInput.Controller.RTouch) { isVibratingRight = false;}
        if (controller == OVRInput.Controller.LTouch) { isVibratingLeft = false;}
    }

    private void StopVibrationBoth(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        isVibratingBoth = false;
    }
}
