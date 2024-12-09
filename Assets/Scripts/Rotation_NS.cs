using System.Collections.Specialized;
using UnityEngine;

public class HapticGridControllerRotation : MonoBehaviour
{
    [Header("Bin Settings")]
    [Range(1, 200)] public int TotalBins = 50; // Slider for horizontal bins
    //[Range(1, 200)] public int verticalBins = 50;   // Slider for vertical bins

    [Tooltip("Step size for bin numbers")]
    public int binStepSize = 10;

    void OnValidate()
    {
        TotalBins = Mathf.RoundToInt(TotalBins / (float)binStepSize) * binStepSize;
        //verticalBins = Mathf.RoundToInt(verticalBins / (float)binStepSize) * binStepSize;
    }

    [Header("Haptic Settings")]
    public float VibrationFrequency = 125f; // Frequency of vibration
    public float VibrationDuration = 0.04f;      // Duration of vibration pulse
    public float VibrationAmplitude = 1.0f;
    private float minimumRotation = -180f;
    private float maximumRotation = 180f;
    private float rmsAngle = 0f;

    //[Header("Waveform Settings")]
    public enum WaveformType { Sine, Square, Sawtooth, Triangle }
    public WaveformType waveform = WaveformType.Sine;

    [Header("Controllers Setting")]
    public Transform leftHand;  // Reference to the left hand
    public Transform rightHand; // Reference to the right hand

    /// <summary>
    /// Detecting Movement of Hand Controllers
    /// </summary>
    private Quaternion lastLeftRotation;
    private Quaternion lastRightRotation;
    private Quaternion relativeRotation;
    private bool leftControllerIsRotated = false;
    private bool rightControllerIsRotated = false;
    private float leftAngleDifference = 0f;
    private float rightAngleDifference = 0f;
    private float lastRMSAngle = 0f;

    /// <summary>
    /// Haptic State for Controllers
    /// </summary>
    private bool isTriggerPressedLeft = false;
    private bool isTriggerPressedRight = false;
    private bool isVibratingLeft = false;
    private bool isVibratingRight = false;
    private bool isVibratingBoth = false;

    private Vector3 originalCubePosition;

    /// <summary>
    /// Distance between Controllers + Mapping Details
    /// </summary>
    private int mappedBinId = 0;
    private int lastBinId = 0;
    private float vibrationStartTimeLeft = 0f;
    private float vibrationStartTimeRight = 0f;
    private float vibrationStartTimeBoth = 0f;
    private float rotationThreshold = 1.0f;
    private float kJitterThreshold = 1.0f;

    void Start()
    {
        lastRMSAngle = rmsAngle;
    }

    void Update()
    {
        // Continuous Distance Measurement
        GetCoordinates();
        rmsAngle = Mathf.Clamp(rmsAngle, minimumRotation, maximumRotation);
        mappedBinId = Mathf.RoundToInt(((rmsAngle - minimumRotation) * (TotalBins - 0) / (maximumRotation - minimumRotation)+minimumRotation));

        // Check input for toggling haptics
        HandleHapticsToggle(OVRInput.Controller.LTouch, ref isTriggerPressedLeft);
        HandleHapticsToggle(OVRInput.Controller.RTouch, ref isTriggerPressedRight);

        float netChange = Mathf.Abs(lastRMSAngle - rmsAngle);
        if (netChange < kJitterThreshold)
        {
            StopVibrationBoth(OVRInput.Controller.RTouch);
            StopVibrationBoth(OVRInput.Controller.LTouch);
            return;
        }

        // Run MCV
        if (isTriggerPressedLeft && isTriggerPressedRight)
        {
            MotionCoupledVibrationBoth();
        }
        else if (isTriggerPressedRight && !isTriggerPressedLeft)
        {
            if (rightControllerIsRotated)
            {
                MotionCoupledVibrationRight();
            }
        }
        else if (isTriggerPressedLeft && !isTriggerPressedRight)
        {
            if (leftControllerIsRotated)
            {
                MotionCoupledVibrationLeft();
            }
        }
        else
        {
            isTriggerPressedLeft = false;
            isTriggerPressedRight = false;
            StopVibrationBoth(OVRInput.Controller.RTouch);
            StopVibrationBoth(OVRInput.Controller.LTouch);
        }
    }

    private void HandleHapticsToggle(OVRInput.Controller controller, ref bool isTriggerPressedLeft)
    {
        // Check if Primary Index Trigger is pressed
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller)) { isTriggerPressedLeft = true; }
        else { isTriggerPressedLeft = false; }
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

    private void GetCoordinates()
    {
        // Calculate current rotation for each hand
        Quaternion currentLeftRotation = leftHand.rotation;
        Quaternion currentRightRotation = rightHand.rotation;

        // Calculate relative rotation between controllers
        relativeRotation = Quaternion.Inverse(currentLeftRotation) * currentRightRotation;
        Vector3 relativeAngles = relativeRotation.eulerAngles;

        // Ensure relativeAngles are within a -180 to 180 range for accurate calculations
        relativeAngles = NormalizeAngles(relativeAngles);

        // Compute RMS of the relative angles
        rmsAngle = CalculateRMS(relativeAngles);
        Debug.Log($"RMS of Angular Shift: {rmsAngle:F2}");

        // Check for rotation changes
        CheckLeftControllerRotation(currentLeftRotation);
        CheckRightControllerRotation(currentRightRotation);

    }

    private void CheckLeftControllerRotation(Quaternion currentLeftRotation)
    {
        leftAngleDifference = Quaternion.Angle(lastLeftRotation, currentLeftRotation);
        if (leftAngleDifference > rotationThreshold)
        {
            leftControllerIsRotated = true;
            Debug.Log("Left Controller Rotated");
        }
        else { leftControllerIsRotated = false; } 
        lastLeftRotation = currentLeftRotation;
    }

    private void CheckRightControllerRotation(Quaternion currentRightRotation)
    {
        rightAngleDifference = Quaternion.Angle(lastRightRotation, currentRightRotation);
        if (rightAngleDifference > rotationThreshold)
        {
            rightControllerIsRotated = true;
            Debug.Log("Right Controller Rotated");
        }
        else { rightControllerIsRotated = false; }
        lastRightRotation = currentRightRotation;
    }

    private Vector3 NormalizeAngles(Vector3 angles)
    {
        angles.x = NormalizeAngle(angles.x);
        angles.y = NormalizeAngle(angles.y);
        angles.z = NormalizeAngle(angles.z);
        return angles;
    }

    private float NormalizeAngle(float angle)
    {
        angle = angle % 360; // Wrap angle to 0–360
        if (angle > 180) angle -= 360; // Convert to -180 to 180
        return angle;
    }

    private float CalculateRMS(Vector3 angles)
    {
        // Compute RMS: sqrt((x^2 + y^2 + z^2) / 3)
        return Mathf.Sqrt((angles.x * angles.x + angles.y * angles.y + angles.z * angles.z) / 3);
    }

    /// Vibration Algorithms

    private void MotionCoupledVibrationLeft()
    {
        if (leftControllerIsRotated && mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingLeft)
            {
                StopVibration(OVRInput.Controller.LTouch);
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.LTouch, VibrationFrequency, VibrationAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            //lastLeftRotation = currentLeftRotation;
        }

        // Stop vibration after the pulse duration
        if (isVibratingLeft && Time.time - vibrationStartTimeLeft > VibrationDuration)
        {
            StopVibration(OVRInput.Controller.LTouch);
        }
    }

    private void MotionCoupledVibrationRight()
    {
        if (rightControllerIsRotated && mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingRight)
            {
                StopVibration(OVRInput.Controller.RTouch);
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.RTouch, VibrationFrequency, VibrationAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            //lastRightRotation = currentRightRotation;
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
            StartVibrationBoth(OVRInput.Controller.RTouch, VibrationFrequency, VibrationAmplitude);
            StartVibrationBoth(OVRInput.Controller.LTouch, VibrationFrequency, VibrationAmplitude);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
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
        if (controller == OVRInput.Controller.RTouch) { isVibratingRight = false; }
        if (controller == OVRInput.Controller.LTouch) { isVibratingLeft = false; }
    }

    private void StopVibrationBoth(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        isVibratingBoth = false;
    }
}
