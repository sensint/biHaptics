using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HapticInteractionManager : MonoBehaviour
{
    [Header("Configuration")]
    public bool rightHandIsActor = true;
    [Header("Dominant Hand Haptics (Controllable by UI)")]
    [Range(0f, 1f)]
    public float dominantAmplitude = 1.0f;
    [Range(80f, 200f)]
    public float dominantFrequency = 150f;
    [Range(1, 400)]
    public int grains = 200; // Number of non-dominant pulses over a range

    [Header("Non-Dominant Multipliers (Controllable by UI)")]
    [Range(0f, 2f)]
    public float amplitudeMultiplier = 1.0f;
    [Range(0f, 2f)]
    public float frequencyMultiplier = 1.0f;
    [Range(0f, 500f)]
    public float delayMs = 0f;
    [Range(0f, 1f)]
    public float grainMultiplier = 1.0f;

    [Header("Motion Detection Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public float movementThreshold = 0.001f;
    public float minimumDistance = 0.05f;
    public float maximumDistance = 1.0f;

    // Private internal state
    private HapticController hapticController;
    private Vector3 lastLeftPos, lastRightPos;
    private int lastRelativeBin = -1;
    private int grainPulseInterval = 1;
    private const int GRAIN_SCALING_FACTOR = 200; // Used to calculate pulse interval
    private void Awake()
    {
        // Ensure HapticController exists on this GameObject
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
            hapticController = gameObject.AddComponent<HapticController>();

        if (leftHandTransform != null) lastLeftPos = leftHandTransform.position;
        if (rightHandTransform != null) lastRightPos = rightHandTransform.position;
    }

    private void Update()
    {
        // Detect movement (TO ADD: ROTATIONAL CHECKS AS WELL. BECAUSE WITHOUT A POSITION CHANGE I CAN ROTATE)
        bool leftMoved = (leftHandTransform.position - lastLeftPos).magnitude > movementThreshold;
        bool rightMoved = (rightHandTransform.position - lastRightPos).magnitude > movementThreshold;

        if (leftMoved || rightMoved)
        {
            HandleRelativeStretchingBins(leftMoved, rightMoved);
            HandleRelativeBendingBins(leftMoved, rightMoved);
            HandleRelativeTwistingBins(leftMoved, rightMoved);
        }
        lastLeftPos = leftHandTransform.position;
        lastRightPos = rightHandTransform.position;
    }

    private void ApplyCrosstalk(bool leftMoved, bool rightMoved)
    {
        bool dominantMoved = rightHandIsActor ? rightMoved : leftMoved;
        OVRInput.Controller dominantController = rightHandIsActor ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
        OVRInput.Controller nonDominantController = rightHandIsActor ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // --- Dominant Hand Vibration ---
        float dominantDuration = 1.0f / dominantFrequency;
        StartCoroutine(hapticController.StartVibrationForDuration(dominantController, dominantFrequency, dominantAmplitude, dominantDuration));

        // --- Non-Dominant Hand Vibration ---
        // This simulates granular feedback by only vibrating on certain intervals
        // Calculate the interval for non-dominant pulses. A multiplier of 0.5 means an interval of 2 (vibrate every 2nd bin).
        int nonDominantInterval = (grainMultiplier > 0) ? Mathf.RoundToInt(1.0f / grainMultiplier) : int.MaxValue;

        // Trigger if the current bin number is a multiple of the calculated interval.
        if (lastRelativeBin % nonDominantInterval == 0)
        {
            // Determines how many dominant pulses occur before one non-dominant pulse
            grainPulseInterval = Mathf.Max(1, GRAIN_SCALING_FACTOR / grains);

            // Trigger the non-dominant vibration based on the grain interval
            float nonDomAmp = dominantAmplitude * amplitudeMultiplier;
            float nonDomFreq = dominantFrequency * frequencyMultiplier;
            float nonDomDuration = 1.0f / nonDomFreq;

            if (delayMs > 0)
            {
                StartCoroutine(DelayedVibration(nonDominantController, delayMs / 1000f, nonDomAmp, nonDomFreq, nonDomDuration));
            }
            else
            {
                StartCoroutine(hapticController.StartVibrationForDuration(nonDominantController, nonDomFreq, nonDomAmp, nonDomDuration));
            }
        }
    }

    private IEnumerator DelayedVibration(OVRInput.Controller controller, float delay, float amp, float freq, float duration)
    {
        yield return new WaitForSeconds(delay);
        yield return hapticController.StartVibrationForDuration(controller, freq, amp, duration);
    }

    private void HandleRelativeStretchingBins(bool leftMoved, bool rightMoved)
    {
        float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
        float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
        int binId = Mathf.RoundToInt(normalizedDistance * (grains - 1));
        if (binId != lastRelativeBin)
        {
            ApplyCrosstalk(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
    }

    private void HandleRelativeBendingBins(bool leftMoved, bool rightMoved)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float rmsAngle = Mathf.Sqrt((relAngles.x * relAngles.x + relAngles.y * relAngles.y + relAngles.z * relAngles.z) / 3f);
        float normalizedAngle = Mathf.Clamp01(rmsAngle / 180f);
        int binId = Mathf.RoundToInt(normalizedAngle * (grains - 1));
        if (binId != lastRelativeBin)
        {
            ApplyCrosstalk(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
    }

    private void HandleRelativeTwistingBins(bool leftMoved, bool rightMoved)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float twist = relAngles.z;
        float normalizedTwist = Mathf.Clamp01((twist + 180f) / 360f);
        int binId = Mathf.RoundToInt(normalizedTwist * (grains - 1));
        if (binId != lastRelativeBin)
        {
            ApplyCrosstalk(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
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
        angle = angle % 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f; // Ensure range is consistently -180 to 180
        return angle;
    }

}

