using UnityEngine;

public class HapticGridController : MonoBehaviour
{
    [Header("Bin Settings")]
    [Range(1, 200)] public int horizontalBins = 10; // Slider for horizontal bins
    [Range(1, 200)] public int verticalBins = 10;   // Slider for vertical bins

    [Tooltip("Step size for bin numbers")]
    public int binStepSize = 5;

    void OnValidate(){
        horizontalBins = Mathf.RoundToInt(horizontalBins / (float)binStepSize) * binStepSize;
        verticalBins = Mathf.RoundToInt(verticalBins / (float)binStepSize) * binStepSize;
    }

    [Header("Movement Range")]
    public float horizontalRange = 1.0f; // Horizontal movement range
    public float verticalRange = 1.0f;   // Vertical movement range

    [Header("Haptic Settings")]
    public float vibrationFrequency = 100f; // Frequency of vibration
    // public float maxAmplitude = 1.0f;      
    public float pulseDuration = 0.1f;      // Duration of vibration pulse
    public float horizontalAmplitude = 0.5f;
    public float verticalAmplitude = 0.7f;

    //[Header("Waveform Settings")]
    public enum WaveformType{Sine, Square, Sawtooth, Triangle}
    public WaveformType waveform = WaveformType.Sine;

    [Header("Controllers Setting")]
    public Transform leftHand;  // Reference to the left hand
    public Transform rightHand; // Reference to the right hand

    private int lastHorizontalBinL = -1; // Last horizontal bin LController
    private int lastVerticalBinL = -1;   // Last vertical bin LController

    private int lastHorizontalBinR = -1; // Last horizontal bin RController
    private int lastVerticalBinR = -1;   // Last vertical bin RController

    private bool isHapticsOnL = false; // Haptics state for left controller
    private bool isHapticsOnR = false; // Haptics state for right controller

    private float vibrationStartTimeL = 0f; // Start time for left vibration
    private float vibrationStartTimeR = 0f; // Start time for right vibration

    void Update()
    {
        // Check input for toggling haptics
        HandleHapticsToggle(OVRInput.Controller.LTouch, ref isHapticsOnL);
        HandleHapticsToggle(OVRInput.Controller.RTouch, ref isHapticsOnR);

        // Handle left controller haptic feedback
        if (isHapticsOnL && leftHand != null)
        {
            HandleHapticFeedback(leftHand, OVRInput.Controller.LTouch, ref lastHorizontalBinL, ref lastVerticalBinL, ref vibrationStartTimeL);
        }

        // Handle right controller haptic feedback
        if (isHapticsOnR && rightHand != null)
        {
            HandleHapticFeedback(rightHand, OVRInput.Controller.RTouch, ref lastHorizontalBinR, ref lastVerticalBinR, ref vibrationStartTimeR);
        }
    }

    private void HandleHapticsToggle(OVRInput.Controller controller, ref bool isHapticsOn)
    {
        // Check if Primary Index Trigger is pressed
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller))
        {
            isHapticsOn = !isHapticsOn; // Toggle haptics state
            if (!isHapticsOn)
            {
                StopVibration(controller); // Stop haptics if toggled off
                Debug.Log($"Haptics turned off for {controller}");
            }
            else
            {
                Debug.Log($"Haptics turned on for {controller}");
            }
        }
    }

    private void HandleHapticFeedback(Transform hand, OVRInput.Controller controller, ref int lastHorizontalBin, ref int lastVerticalBin, ref float vibrationStartTime)
    {
        
        Vector3 handPosition = hand.position;

        // Map horizontal position to a bin
        int currentHorizontalBin = Mathf.Clamp(
            Mathf.FloorToInt((handPosition.x + horizontalRange / 2) / (horizontalRange / horizontalBins)),
            0, horizontalBins - 1
        );
        

        // Map vertical position to a bin
        int currentVerticalBin = Mathf.Clamp(
            Mathf.FloorToInt((handPosition.y + verticalRange / 2) / (verticalRange / verticalBins)),
            0, verticalBins - 1
        );
       

        // Trigger haptic feedback if the bin has changed
        if (currentHorizontalBin != lastHorizontalBin || currentVerticalBin != lastVerticalBin)
        {
            float baseAmplitude;

            if (currentHorizontalBin != lastHorizontalBin){
                baseAmplitude = horizontalAmplitude;
            }
            else if (currentVerticalBin != lastVerticalBin){
                baseAmplitude = verticalAmplitude;
            }
            else{
                baseAmplitude = 0f;
            }
            
            float amplitude = ApplyWaveform(baseAmplitude);

        
            // float horizontal_movement = (float) currentHorizontalBin/ horizontalBins;
            // float vertical_movement = (float) currentVerticalBin / verticalBins;
            // float amplitude = Mathf.Clamp(Mathf.Max(horizontal_movement, vertical_movement), 0f, maxAmplitude);

            StartVibration(controller, vibrationFrequency, amplitude);

            // Update last bin IDs and vibration start time
            lastHorizontalBin = currentHorizontalBin;
            lastVerticalBin = currentVerticalBin;
            vibrationStartTime = Time.time;

            Debug.Log($"Controller: {controller}, Horizontal Bin: {currentHorizontalBin}, Vertical Bin: {currentVerticalBin}, Base Amplitude: {baseAmplitude}, Amplitude wiith Waveform: {amplitude}, Waveform: {waveform}");
        }

        
        if (Time.time - vibrationStartTime > pulseDuration)
        {
            StopVibration(controller);
        }
    }

    private float ApplyWaveform(float baseAmplitude){
        switch (waveform){
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

    private void StartVibration(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
    }

    private void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
    }
}
