using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// To Do: CHECK FOR ToADD and CHECK

public class Exp2_v1 : MonoBehaviour
{
    public enum CrosstalkType { Amplitude, Frequency, Delay, Grain }
    public CrosstalkType currentCrosstalkType = CrosstalkType.Frequency;

    [Header("Experiment Condition Control")]
    public string[] experimentConditions = new string[] { "A33", "A66", "A100", "F80", "F170", "D50", "D100", "G50", "G10", "G25", "D400", "D200" }; 
    public int currentConditionIndex = 0;

    [Header("Haptic Settings")]
    [Tooltip("Dominant hand: true = right, false = left (only for Delay)")]
    public bool rightHandIsActor = true;
    public float vibrationFrequency = 125f; // default frequency
    public float vibrationDuration = 0.008f;
    public float amplitude = 1f; // default amplitude
    public float delayMs = 0f;
    public int grains = 200; // default grains/ bins

    [Header("Motion Detection Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public float movementThreshold = 0.001f;
    public float minimumDistance = 0.05f;
    public float maximumDistance = 2.0f;

    private HapticController hapticController;
    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private int lastRelativeBin = -1;

    private bool inInterlude = false;

    // -- New variables to handle grain pulsing logic
    private int pulseCounter = 0;
    private int grainPulseInterval = 1;

    private void Awake()
    {
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
        {
            hapticController = gameObject.AddComponent<HapticController>();
        }
        if (leftHandTransform != null) lastLeftPos = leftHandTransform.position;
        if (rightHandTransform != null) lastRightPos = rightHandTransform.position;

        if (experimentConditions.Length > 0)
        {
            ParseCondition(experimentConditions[currentConditionIndex]);
            Debug.Log($"[Experiment] Initial condition: {experimentConditions[currentConditionIndex]}");
        }
    }

    private void Update()
    {
        sequenceProceed();
        if (inInterlude) return;

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
        // Fixed values for the dominant hand (actor)
        const float dominantFrequency = 125f;
        const float dominantAmplitude = 1f;
        const float dominantDuration = 0.008f;

        bool dominantMoved = rightHandIsActor ? rightMoved : leftMoved;
        OVRInput.Controller dominantController = rightHandIsActor ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
        OVRInput.Controller nonDominantController = rightHandIsActor ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // The non-dominant hand only vibrates if the dominant hand moves
        if (dominantMoved)
        {
            // Vibrate the dominant hand with fixed, standard values
            StartCoroutine(hapticController.StartVibrationForDuration(
                dominantController,
                dominantFrequency,
                dominantAmplitude,
                dominantDuration
            ));
            Debug.Log($"Dominant hand ({dominantController}) - Playing: Freq: {dominantFrequency}Hz, Amp: {dominantAmplitude}, Dur: {dominantDuration}s");

            // Vibrate the non-dominant hand with modified parameters from ParseCondition
            switch (currentCrosstalkType)
            {
                case CrosstalkType.Frequency:
                    StartCoroutine(hapticController.StartVibrationForDuration(
                        nonDominantController,
                        vibrationFrequency,
                        amplitude,
                        vibrationDuration
                    ));
                    Debug.Log($"Non-Dominant hand ({nonDominantController}) - Playing: Freq: {vibrationFrequency}Hz, Amp: {amplitude}, Dur: {vibrationDuration}s");
                    break;
                case CrosstalkType.Grain:
                    pulseCounter++;
                    if (pulseCounter % grainPulseInterval == 0)
                    {
                        StartCoroutine(hapticController.StartVibrationForDuration(
                            nonDominantController,
                            vibrationFrequency,
                            amplitude,
                            vibrationDuration
                        ));
                    }
                    Debug.Log($"Non-Dominant hand ({nonDominantController}) - Playing Grain. Interval: {grainPulseInterval}, Freq: {vibrationFrequency}Hz, Amp: {amplitude}, Dur: {vibrationDuration}s");
                    break;
                case CrosstalkType.Delay:
                    StartCoroutine(DelayedVibration(
                        nonDominantController,
                        delayMs,
                        amplitude
                    ));
                    Debug.Log($"Non-Dominant hand ({nonDominantController}) - Playing with a Delay of {delayMs}ms. Freq: {vibrationFrequency}Hz, Amp: {amplitude}, Dur: {vibrationDuration}s");
                    break;
                case CrosstalkType.Amplitude:
                    StartCoroutine(hapticController.StartVibrationForDuration(
                        nonDominantController,
                        vibrationFrequency,
                        amplitude,
                        vibrationDuration
                    ));
                    Debug.Log($"Non-Dominant hand ({nonDominantController}) - Playing: Freq: {vibrationFrequency}Hz, Amp: {amplitude}, Dur: {vibrationDuration}s");
                    break;
            }
        }
    }

    private IEnumerator DelayedVibration(OVRInput.Controller controller, float delayMs, float mappedAmplitude)
    {
        if (delayMs > 0f)
            yield return new WaitForSeconds(delayMs / 1000f);
        yield return hapticController.StartVibrationForDuration(controller, vibrationFrequency, mappedAmplitude, vibrationDuration);
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

    private void sequenceProceed()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!inInterlude)
            {
                // Go to interlude
                inInterlude = true;
                Debug.Log("[Exp2] Entering interlude (no vibrations). Press Space for next condition.");
                hapticController.StopVibration(OVRInput.Controller.LTouch);
                hapticController.StopVibration(OVRInput.Controller.RTouch);
            }
            else
            {
                // Exit interlude → next condition
                inInterlude = false;
                currentConditionIndex = (currentConditionIndex + 1) % experimentConditions.Length;
                ParseCondition(experimentConditions[currentConditionIndex]);
                Debug.Log($"[Experiment] Switched to NEXT condition: {experimentConditions[currentConditionIndex]}");
            }
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            inInterlude = false;
            currentConditionIndex = (currentConditionIndex - 1 + experimentConditions.Length) % experimentConditions.Length;
            ParseCondition(experimentConditions[currentConditionIndex]);
        }
    }

    private void ParseCondition(string cond)
    {
        hapticController.StopVibration(OVRInput.Controller.LTouch);
        hapticController.StopVibration(OVRInput.Controller.RTouch);

        // Reset Defaults
        amplitude = 1.0f;
        vibrationFrequency = 125f;
        vibrationDuration = 0.008f;
        grains = 200;
        delayMs = 0f;

    // -- Reseting pulse counter for new condition
    pulseCounter = 0;
        grainPulseInterval = 1;

        if (cond.StartsWith("F"))
        {
            currentCrosstalkType = CrosstalkType.Frequency;
            vibrationFrequency = float.Parse(cond.Substring(1));
            vibrationDuration = 1f / vibrationFrequency;
        }
        else if (cond.StartsWith("D"))
        {
            currentCrosstalkType = CrosstalkType.Delay;
            delayMs = float.Parse(cond.Substring(1));
        }
        else if (cond.StartsWith("G"))
        {
            currentCrosstalkType = CrosstalkType.Grain;
            float factor = float.Parse((cond.Substring(1))) / 100f;
            grainPulseInterval = Mathf.RoundToInt(1f / factor);
        }
        else if (cond.StartsWith("A"))
        {
            currentCrosstalkType = CrosstalkType.Amplitude;
            amplitude = float.Parse(cond.Substring(1)) / 100f;
        }
        Debug.Log($"[Exp2] Condition: {cond}, CrosstalkType: {currentCrosstalkType}");
    }
}
