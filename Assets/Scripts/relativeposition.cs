using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class relativeposition : MonoBehaviour
{
    [Header("Bin Settings")]
    [Range(1, 200)] public int horizontalBins = 10; // Slider for horizontal bins
    [Range(1, 200)] public int verticalBins = 10;   // Slider for vertical bins

    [Header("Movement Range")]
    public float horizontalRange = 1.5f; // Horizontal movement range
    public float verticalRange = 1.5f;   // Vertical movement range

    [Header("Haptic Settings")]
    public float vibrationFrequency = 100f; // Frequency of vibration
    public float maxAmplitude = 1.0f;       // Maximum amplitude of vibration
    public float pulseDuration = 0.1f;      // Duration of vibration pulse

    public Transform leftHand;  // Reference to the left hand
    public Transform rightHand; // Reference to the right hand

    private int lastHorizontalBin = -1; // Last horizontal bin based on relative position
    private int lastVerticalBin = -1;   // Last vertical bin based on relative position

    private float vibrationStartTime = 0f; // Start time for vibration

    void Update()
    {
        
        if (leftHand != null && rightHand != null)
        {
            HandleHapticFeedbackWithTwoControllers();
        }
    }

    private void HandleHapticFeedbackWithTwoControllers()
    {
    
        Vector3 relativePosition = leftHand.localPosition - rightHand.localPosition;

        // Map the horizontal relative position to a bin
        int currentHorizontalBin = Mathf.Clamp(
            Mathf.FloorToInt((relativePosition.x + horizontalRange / 2) / (horizontalRange / horizontalBins)),
            0, horizontalBins - 1
        );

        // Map the vertical relative position to a bin
        int currentVerticalBin = Mathf.Clamp(
            Mathf.FloorToInt((relativePosition.y + verticalRange / 2) / (verticalRange / verticalBins)),
            0, verticalBins - 1
        );

        Debug.Log($"Relative Horizontal Bin: {currentHorizontalBin}, Relative Vertical Bin: {currentVerticalBin}");

        // Trigger haptic feedback if the bin has changed
        if (currentHorizontalBin != lastHorizontalBin || currentVerticalBin != lastVerticalBin)
        {
            // Calculate amplitude based on horizontal and vertical movements
            float horizontalContribution = (float)currentHorizontalBin / horizontalBins;
            float verticalContribution = (float)currentVerticalBin / verticalBins;

            // Use the maximum contribution for amplitude
            float amplitude = Mathf.Clamp(
                Mathf.Max(horizontalContribution, verticalContribution),
                0f, maxAmplitude
            );

            // Start vibration for both controllers
            StartVibration(OVRInput.Controller.LTouch, vibrationFrequency, amplitude);
            StartVibration(OVRInput.Controller.RTouch, vibrationFrequency, amplitude);

            // Update the last bin IDs and vibration start time
            lastHorizontalBin = currentHorizontalBin;
            lastVerticalBin = currentVerticalBin;
            vibrationStartTime = Time.time;

            Debug.Log($"Amplitude: {amplitude}, Frequency: {vibrationFrequency}");
        }

        // Stop vibration after the pulse duration
        if (Time.time - vibrationStartTime > pulseDuration)
        {
            StopVibration(OVRInput.Controller.LTouch);
            StopVibration(OVRInput.Controller.RTouch);
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
