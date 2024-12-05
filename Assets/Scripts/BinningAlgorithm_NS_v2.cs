using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class BinningAlgorithm : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;

    private float vibrationFrequency = 200f; // Frequency of vibration
    private float vibrationAmplitude = 5.0f; // Amplitude of vibration
    private float minimumDistance = 0.2f; // Minimum distance for binning
    private float maximumDistance = 1.0f; // Maximum distance for binning
    private int numberOfBins = 200; // Number of bins for mapping
    private float pulseDuration = 0.02f; // Duration of vibration pulse in seconds
    private float filterWeight = 0.7f; // Weight for distance filtering

    private float distanceBetweenControllers = 0f;
    private float distanceBetweenControllersFiltered = 0f;
    private int mappedBinId = 0;
    private int lastBinId = 0;
    private float vibrationStartTime = 0f;
    private bool isVibratingBoth = false;

    private string logFilePath;

    void Start()
    {
        Debug.Log("Log initialized.");
    }

    void Update()
    {
        if (leftHand == null || rightHand == null)
        {
            Debug.Log("LeftHand or RightHand transform is not assigned.");
            return;
        }

        // Calculate the current distance between the controllers
        distanceBetweenControllers = Vector3.Distance(leftHand.position, rightHand.position);

        // Apply first-order low-pass filtering to the distance
        distanceBetweenControllersFiltered = (1f - filterWeight) * distanceBetweenControllersFiltered + filterWeight * distanceBetweenControllers;

        // Map the filtered distance to a bin
        distanceBetweenControllersFiltered = Mathf.Clamp(distanceBetweenControllersFiltered, minimumDistance, maximumDistance);
        //distanceBetweenControllers = Mathf.Clamp(distanceBetweenControllers, minimumDistance, maximumDistance);

        mappedBinId = Mathf.RoundToInt((distanceBetweenControllers - minimumDistance) * (numberOfBins - 0) / (maximumDistance - minimumDistance));

        Debug.Log($"MaxDis: {maximumDistance}, DistanceBetCont, {distanceBetweenControllers}, DistanceBetContF: {distanceBetweenControllersFiltered}, BinID: {mappedBinId}");


        // Check if the bin has changed
        if (mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingBoth)
            {
                StopVibration(OVRInput.Controller.LTouch);
                StopVibration(OVRInput.Controller.RTouch);
                Debug.Log("Stopped vibration on both controllers.");
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.LTouch);
            StartVibration(OVRInput.Controller.RTouch);
            Debug.Log($"Started vibration. Bin changed to {mappedBinId}.");

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            vibrationStartTime = Time.time;
        }

        // Stop vibration after the pulse duration
        if (isVibratingBoth && Time.time - vibrationStartTime > pulseDuration)
        {
            StopVibration(OVRInput.Controller.LTouch);
            StopVibration(OVRInput.Controller.RTouch);
            Debug.Log("Vibration pulse duration ended.");
        }
    }

    private void StartVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(vibrationFrequency, vibrationAmplitude, controller);
        isVibratingBoth = true;
    }

    private void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        isVibratingBoth = false;
    }
}
