using System.Collections;
using UnityEngine;

public class Exp3_v1 : MonoBehaviour
{
    [Header("Haptic Parameters (live updated)")]
    public bool rightHandIsActor = true;
    public float dominantAmplitude = 1f;
    public float dominantFrequency = 125f;
    public float dominantDuration = 0.008f;

    [Header("Non-Dominant Multipliers (from sliders)")]
    public float amplitudeMultiplier = 1f;
    public float frequencyMultiplier = 1f;
    public float delayMs = 0f;
    public int grains = 200;

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
    private int pulseCounter = 0;
    private int grainPulseInterval = 1;

    private void Awake()
    {
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
            hapticController = gameObject.AddComponent<HapticController>();

        if (leftHandTransform != null) lastLeftPos = leftHandTransform.position;
        if (rightHandTransform != null) lastRightPos = rightHandTransform.position;
    }

    private void Update()
    {
        // Check hand movement
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

        if (dominantMoved)
        {
            // Dominamt hand vibration
            StartCoroutine(hapticController.StartVibrationForDuration(dominantController, dominantFrequency, dominantAmplitude, dominantDuration));

            // Non-Dominant hand vibration
            pulseCounter++;
            if (grains > 0) grainPulseInterval = Mathf.Max(1, 200 / grains);
            if (pulseCounter % grainPulseInterval == 0)
            {
                float nonDomAmp = dominantAmplitude * amplitudeMultiplier;
                float nonDomFreq = dominantFrequency * frequencyMultiplier;

                if (delayMs > 0)
                {
                    StartCoroutine(DelayedVibration(nonDominantController, delayMs, nonDomAmp, nonDomFreq));
                }
                else
                {
                    StartCoroutine(hapticController.StartVibrationForDuration(nonDominantController, nonDomFreq, nonDomAmp, dominantDuration));
                }
            }

        }
    }

    private IEnumerator DelayedVibration(OVRInput.Controller controller, float delayMs, float amp, float freq)
    {
        if (delayMs > 0f)
            yield return new WaitForSeconds(delayMs / 1000f);

        yield return hapticController.StartVibrationForDuration(
            controller, freq, amp, 0.008f);
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
