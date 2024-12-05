using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class CombinedHapticController : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;
    public Transform cube; 

    private float FrequencyButton = 100f;
    private float AmplitudeButton = 0.2f;

    private float moveSpeed = 2f; // Speed of movement
    private float xRange = 2f;   // Maximum range for X-axis movement
    private float zRange = 2f;   // Maximum range for forward/backward movement

    // Binning Algorithm Variables
    private float vibrationFrequencyBinning = 100f;
    private float vibrationAmplitudeBinningL = 1f;
    private float vibrationAmplitudeBinningR = 1f;
    private float minimumDistance = 0.2f;
    private float maximumDistance = 1.0f;
    private int numberOfBins = 200;
    private float pulseDuration = 0.01f;
    private float filterWeight = 0.7f;
    private float distanceBetweenControllers = 0f;
    private float distanceBetweenControllersFiltered = 0f;
    private int mappedBinId = 0;
    private int lastBinId = 0;
    private float vibrationStartTime = 0f;
    private bool isVibratingBoth = false;

    private Vector3 originalCubePosition;
    private Vector3 distanceBetweenControllersXYZ;
    private Quaternion relativeRotation;

    void Start()
    {
        // Get the OVRCameraRig
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();

        if (cameraRig != null)
        {
            if (leftHand == null)
                leftHand = cameraRig.leftHandAnchor;

            if (rightHand == null)
                rightHand = cameraRig.rightHandAnchor;
        }
        else
        {
            Debug.LogError("OVRCameraRig not found in the scene.");
        }

        // Store the cube's original position
        if (cube != null)
        {
            originalCubePosition = cube.position;
        }
        else
        {
            Debug.LogError("Cube is not assigned in the Inspector!");
        }
    }

    void Update()
    {
        if (leftHand == null || rightHand == null)
        {
            Debug.LogError("LeftHand or RightHand transform is not assigned. Ensure OVRCameraRig is correctly set up.");
            return;
        }

        GetCoordinates();

        // Calculate the current distance between the controllers
        distanceBetweenControllers = Vector3.Distance(leftHand.position, rightHand.position);
        distanceBetweenControllers = Mathf.Clamp(distanceBetweenControllers, minimumDistance, maximumDistance);
        mappedBinId = Mathf.RoundToInt((distanceBetweenControllers - minimumDistance) * (numberOfBins - 0) / (maximumDistance - minimumDistance));

        // Adjust amplitude using A and B buttons on the left controller
        HandleAmplitudeAdjustment();

        // Handle other controller actions
        HandleControllerActions(OVRInput.Controller.LTouch, leftHand);
        HandleControllerActions(OVRInput.Controller.RTouch, rightHand);
    }

    private void HandleAmplitudeAdjustment()
    {
        // Check if the B button is pressed to increase the amplitude
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) // Button.Two refers to the B button
        {
            vibrationAmplitudeBinningL = Mathf.Clamp(vibrationAmplitudeBinningL  + 0.1f, 0f, 1.0f); // Increment amplitude, clamped between 0 and 1
            //Debug.Log($"Amplitude increased to: {vibrationAmplitudeBinningL}");
        }

        // Check if the A button is pressed to decrease the amplitude
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) // Button.One refers to the A button
        {
            vibrationAmplitudeBinningL  = Mathf.Clamp(vibrationAmplitudeBinningL  - 0.1f, 0f, 1.0f); // Decrement amplitude, clamped between 0 and 1
            //Debug.Log($"Amplitude decreased to: {vibrationAmplitudeBinningL}");
        }
    }

    private void HandleControllerActions(OVRInput.Controller controller, Transform hand)
    {
        // Handle button press for index and hand triggers
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller))
        {
            // StartVibration(controller, FrequencyButton, AmplitudeButton);
            MotionCoupledVibration();
            MoveCubeTowardsZ(-1); // Move cube (backward)
        }
        else if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controller))
        {
            //StartVibration(controller, FrequencyButton, AmplitudeButton);
            MotionCoupledVibration();
            MoveCubeTowardsZ(1); // Move cube (forward)
        }
        else
        {
            StopVibration(controller);
        }

        // Handle thumbstick input for X and Z-axis movement
        Vector2 thumbstickInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);
        if (thumbstickInput.sqrMagnitude > 0.1f) // Detect if thumbstick is moved
        {
            StartVibration(controller, FrequencyButton, AmplitudeButton);
            MoveCubeOnXAxisZAxis(thumbstickInput);
        }
    }

    private void MoveCubeOnXAxisZAxis(Vector2 thumbstickInput)
{
    if (cube != null)
    {
        // Calculate new X and Z positions based on thumbstick input
        float newX = cube.position.x + thumbstickInput.x * moveSpeed * Time.deltaTime;
        float newZ = cube.position.z + thumbstickInput.y * moveSpeed * Time.deltaTime;

        // clamp positions within the ranges
        newX = Mathf.Clamp(newX, originalCubePosition.x - xRange, originalCubePosition.x + xRange);
        newZ = Mathf.Clamp(newZ, originalCubePosition.z - zRange, originalCubePosition.z + zRange);

        // Set the cube's new position
        cube.position = new Vector3(newX, cube.position.y, newZ);
    }
}


    private void MoveCubeTowardsZ(float direction)
    {
        if (cube != null)
        {
            // Calculate new Z position based on direction (-1 for toward, 1 for away)
            float newZ = cube.position.z + direction * moveSpeed * Time.deltaTime;

            // Clamp Z position within the specified range
            newZ = Mathf.Clamp(newZ, originalCubePosition.z - zRange, originalCubePosition.z + zRange);

            // Set the cube's new position, keeping X and Y unchanged
            cube.position = new Vector3(cube.position.x, cube.position.y, newZ);
        }
    }
    

    /// Get Position of the Coordinates ///

    private void GetCoordinates()
    {
        // Calculate distances along X, Y, Z axes
        Vector3 leftPosition = leftHand.position;
        Vector3 rightPosition = rightHand.position;
        distanceBetweenControllersXYZ = rightPosition - leftPosition;

        // Calculate relative rotation
        relativeRotation = Quaternion.Inverse(leftHand.rotation) * rightHand.rotation;

        // Extract the Euler angles from the relative rotation
        Vector3 relativeAngles = relativeRotation.eulerAngles;

        Debug.Log($"Distances - X: {distanceBetweenControllersXYZ.x:F4}, Y: {distanceBetweenControllersXYZ.y:F4}, Z: {distanceBetweenControllersXYZ.z:F4}");
        Debug.Log($"Relative Angles - Pitch: {relativeAngles.x:F2}, Yaw: {relativeAngles.y:F2}, Roll: {relativeAngles.z:F2}");
    }

    /// <summary>
    /// Vibration Algorithms
    /// </summary>

    private void MotionCoupledVibration()
    {
        if (mappedBinId != lastBinId)
        {
            // Stop any ongoing vibrations
            if (isVibratingBoth)
            {
                StopVibration(OVRInput.Controller.LTouch);
                StopVibration(OVRInput.Controller.RTouch);
            }

            // Start vibration on both controllers
            StartVibration(OVRInput.Controller.LTouch, vibrationFrequencyBinning, vibrationAmplitudeBinningL);
            StartVibration(OVRInput.Controller.RTouch, vibrationFrequencyBinning, vibrationAmplitudeBinningR);

            // Update the last bin ID and reset the vibration start time
            lastBinId = mappedBinId;
            vibrationStartTime = Time.time;
        }

        // Stop vibration after the pulse duration
        if (isVibratingBoth && Time.time - vibrationStartTime > pulseDuration)
        {
            StopVibration(OVRInput.Controller.LTouch);
            StopVibration(OVRInput.Controller.RTouch);
        }
    }

    private void ContinuousVibration()
    {
        StartVibration(OVRInput.Controller.LTouch, vibrationFrequencyBinning, vibrationAmplitudeBinningL);
        StartVibration(OVRInput.Controller.RTouch, vibrationFrequencyBinning, vibrationAmplitudeBinningR);
    }

    private void NoVibration()
    {
        StopVibration(OVRInput.Controller.LTouch);
        StopVibration(OVRInput.Controller.RTouch);
    }

    private void StartVibration(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        isVibratingBoth = true;
    }

    private void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        isVibratingBoth = false;
    }
}