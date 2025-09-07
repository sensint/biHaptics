using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HapticInteractionManager : MonoBehaviour
{
    // REMOVED: The static 'rightHandIsActor' boolean has been removed.

    [Header("Haptic Settings")]
    [Tooltip("Haptics for the hand that is moving more (the 'actor' hand).")]
    [Range(0f, 1f)]
    public float dominantAmplitude = 1.0f;
    [Range(80f, 200f)]
    public float dominantFrequency = 150f;
    [Tooltip("Number of discrete steps (bins) for haptic feedback over a range of motion.")]
    [Range(1, 400)]
    public int grains = 200;

    [Header("Crosstalk Haptics (for the other hand)")]
    [Tooltip("Amplitude multiplier for the non-dominant hand.")]
    [Range(0f, 2f)]
    public float amplitudeMultiplier = 1.0f;
    [Tooltip("Frequency multiplier for the non-dominant hand.")]
    [Range(0f, 2f)]
    public float frequencyMultiplier = 1.0f;
    [Tooltip("Delay in milliseconds before the non-dominant hand vibrates.")]
    [Range(0f, 500f)]
    public float delayMs = 0f;
    [Tooltip("Controls how often the non-dominant hand pulses. 1 = always, 0.5 = every other bin, 0 = never.")]
    [Range(0f, 1f)]
    public float grainMultiplier = 1.0f;

    [Header("Motion Detection Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    [Tooltip("The minimum distance the hand must move in one frame to be considered 'moving'.")]
    public float movementThreshold = 0.001f;

    [Header("Interaction Range")]
    [Tooltip("The minimum distance between hands for stretching haptics to start.")]
    public float minimumDistance = 0.05f;
    [Tooltip("The maximum distance between hands for stretching haptics.")]
    public float maximumDistance = 1.0f;

    // Private internal state
    private HapticController hapticController;
    private Vector3 lastLeftPos, lastRightPos;
    private Quaternion lastLeftRot, lastRightRot; // ADDED: To track rotation for future enhancements

    // CHANGED: Use separate bin trackers for each interaction type for more reliable haptics.
    private int lastStretchingBin = -1;
    private int lastBendingBin = -1;
    private int lastTwistingBin = -1;

    private void Awake()
    {
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
            hapticController = gameObject.AddComponent<HapticController>();

        if (leftHandTransform != null)
        {
            lastLeftPos = leftHandTransform.position;
            lastLeftRot = leftHandTransform.rotation; // ADDED: Initialize last rotation
        }
        if (rightHandTransform != null)
        {
            lastRightPos = rightHandTransform.position;
            lastRightRot = rightHandTransform.rotation; // ADDED: Initialize last rotation
        }
    }

    private void Update()
    {
        if (leftHandTransform == null || rightHandTransform == null) return;

        // CHANGED: Logic to determine the dominant hand dynamically.
        // 1. Calculate positional movement magnitude for both hands.
        float leftMovement = (leftHandTransform.position - lastLeftPos).magnitude;
        float rightMovement = (rightHandTransform.position - lastRightPos).magnitude;

        bool hasMoved = leftMovement > movementThreshold || rightMovement > movementThreshold;

        if (hasMoved)
        {
            // 2. The "Actor" hand is the one that moved more. The "Reactor" is the other.
            bool isRightHandActor = rightMovement > leftMovement;

            OVRInput.Controller actorController = isRightHandActor ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            OVRInput.Controller reactorController = isRightHandActor ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

            // 3. Pass the determined actor/reactor controllers to the interaction handlers.
            HandleRelativeStretchingBins(actorController, reactorController);
            HandleRelativeBendingBins(actorController, reactorController);
            HandleRelativeTwistingBins(actorController, reactorController);
        }

        // Update last known positions and rotations for the next frame
        lastLeftPos = leftHandTransform.position;
        lastRightPos = rightHandTransform.position;
        lastLeftRot = leftHandTransform.rotation;
        lastRightRot = rightHandTransform.rotation;
    }

    // CHANGED: This function now takes the actor/reactor controllers and the current interaction bin as arguments.
    private void ApplyCrosstalk(OVRInput.Controller actorController, OVRInput.Controller reactorController, int currentBin)
    {
        // --- Actor (Dominant) Hand Vibration ---
        float dominantDuration = 1.0f / dominantFrequency;
        StartCoroutine(hapticController.StartVibrationForDuration(actorController, dominantFrequency, dominantAmplitude, dominantDuration));

        // --- Reactor (Non-Dominant) Hand Vibration ---
        int nonDominantInterval = (grainMultiplier > 0) ? Mathf.RoundToInt(1.0f / grainMultiplier) : int.MaxValue;

        // Trigger the non-dominant pulse based on the current interaction bin and the grain multiplier.
        if (currentBin % nonDominantInterval == 0)
        {
            float nonDomAmp = dominantAmplitude * amplitudeMultiplier;
            float nonDomFreq = dominantFrequency * frequencyMultiplier;
            float nonDomDuration = 1.0f / nonDomFreq;

            if (delayMs > 0)
            {
                StartCoroutine(DelayedVibration(reactorController, delayMs / 1000f, nonDomAmp, nonDomFreq, nonDomDuration));
            }
            else
            {
                StartCoroutine(hapticController.StartVibrationForDuration(reactorController, nonDomFreq, nonDomAmp, nonDomDuration));
            }
        }
    }

    private IEnumerator DelayedVibration(OVRInput.Controller controller, float delay, float amp, float freq, float duration)
    {
        yield return new WaitForSeconds(delay);
        yield return hapticController.StartVibrationForDuration(controller, freq, amp, duration);
    }

    // --- Interaction Handlers ---

    // CHANGED: Methods now receive actor/reactor controllers and pass the current bin to ApplyCrosstalk.
    private void HandleRelativeStretchingBins(OVRInput.Controller actor, OVRInput.Controller reactor)
    {
        float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
        float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
        int binId = Mathf.RoundToInt(normalizedDistance * (grains - 1));

        if (binId != lastStretchingBin)
        {
            ApplyCrosstalk(actor, reactor, binId);
            lastStretchingBin = binId;
        }
    }

    private void HandleRelativeBendingBins(OVRInput.Controller actor, OVRInput.Controller reactor)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);

        float rmsAngle = Mathf.Sqrt((relAngles.x * relAngles.x + relAngles.y * relAngles.y + relAngles.z * relAngles.z) / 3f);
        float normalizedAngle = Mathf.Clamp01(rmsAngle / 180f);
        int binId = Mathf.RoundToInt(normalizedAngle * (grains - 1));

        if (binId != lastBendingBin)
        {
            ApplyCrosstalk(actor, reactor, binId);
            lastBendingBin = binId;
        }
    }

    private void HandleRelativeTwistingBins(OVRInput.Controller actor, OVRInput.Controller reactor)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);

        float twist = relAngles.z;
        float normalizedTwist = Mathf.Clamp01((twist + 180f) / 360f);
        int binId = Mathf.RoundToInt(normalizedTwist * (grains - 1));

        if (binId != lastTwistingBin)
        {
            ApplyCrosstalk(actor, reactor, binId);
            lastTwistingBin = binId;
        }
    }

    // --- Helper Functions ---

    private Vector3 NormalizeAngles(Vector3 angles)
    {
        angles.x = NormalizeAngle(angles.x);
        angles.y = NormalizeAngle(angles.y);
        angles.z = NormalizeAngle(angles.z);
        return angles;
    }

    // CHANGED: A more robust method for wrapping angles to the [-180, 180] range.
    private float NormalizeAngle(float angle)
    {
        while (angle <= -180f) angle += 360f;
        while (angle > 180f) angle -= 360f;
        return angle;
    }
}