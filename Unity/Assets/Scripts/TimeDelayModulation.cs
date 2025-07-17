using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeDelayModulation : MonoBehaviour
{
    public enum ModulationMode { NonMotionCoupled, MotionCoupled }
    public enum MaterialType { Custom, Sponge, Rubber, Steel, Gelatin }
    public enum InputType { Position, Velocity }
    public enum AmplitudeFunctionType { Linear, Exponential, Gaussian, AmplitudeCondition }

    [Header("Material Preset")]
    public MaterialType materialType = MaterialType.Sponge;

    [Header("Haptic Settings")]
    [Tooltip("Dominant hand: true = right, false = left")]
    public bool rightHandIsActor = true;
    [Range(0f, 1f)]
    [Tooltip("Material stiffness: 0 = soft, 1 = rigid")]
    public float stiffness = 1.0f;
    [Tooltip("Haptic frequency (Hz)")]
    public float vibrationFrequency = 125f;
    [Tooltip("Haptic pulse duration (seconds)")]
    public float vibrationDuration = 0.04f;
    [Tooltip("Amplitude for both hands")]
    [Range(0f, 1f)]
    public float amplitude = 1.0f;
    [Header("Time Delay Clamp (ms)")]
    [Tooltip("Minimum delay (ms) for rigid material")]
    [Range(0f, 1000f)]
    public float minimumDelayMs = 0f;
    [Tooltip("Maximum delay (ms) for soft material")]
    [Range(0f, 1000f)]
    public float maximumDelayMs = 20f;

    [Header("Modulation Input")]
    public InputType inputType = InputType.Position;
    [Header("Amplitude Function")]
    public AmplitudeFunctionType amplitudeFunction = AmplitudeFunctionType.Linear;
    [Tooltip("Gaussian mu (center)")]
    public float gaussianMu = 0.5f;
    [Tooltip("Gaussian sigma (spread)")]
    public float gaussianSigma = 0.2f;
    [Tooltip("Exponential b (growth rate)")]
    public float exponentialB = 2.0f;

    [Header("Motion Coupling Settings (Bin-based)")]
    public ModulationMode modulationMode = ModulationMode.MotionCoupled;
    [Tooltip("Reference to left hand controller transform")]
    public Transform leftHandTransform;
    [Tooltip("Reference to right hand controller transform")]
    public Transform rightHandTransform;
    [Tooltip("Minimum distance between controllers (meters)")]
    public float minimumDistance = 0.05f;
    [Tooltip("Maximum distance between controllers (meters)")]
    public float maximumDistance = 1.0f;
    [Tooltip("Number of bins for distance mapping")]
    [Range(2, 200)]
    public int horizontalBins = 100;

    private HapticController hapticController;
    private int lastBinId = -1;
    private float lastDistance = 0f;
    private Vector3 lastLeftHandPos;
    private Vector3 lastRightHandPos;
    private float lastUpdateTime;
    private float lastInputValue = 0f;

    private void ApplyMaterialPreset(MaterialType type)
    {
        if (type == MaterialType.Custom)
        {
            Debug.Log("[TimeDelayModulation] Custom material: parameters are user-controlled.");
            return;
        }
        switch (type)
        {
            case MaterialType.Sponge:
                stiffness = 0.0f;
                amplitude = 0.8f;
                vibrationFrequency = 120f;
                vibrationDuration = 0.04f;
                minimumDelayMs = 40f;
                maximumDelayMs = 60f;
                break;
            case MaterialType.Rubber:
                stiffness = 0.5f;
                amplitude = 0.9f;
                vibrationFrequency = 130f;
                vibrationDuration = 0.045f;
                minimumDelayMs = 15f;
                maximumDelayMs = 30f;
                break;
            case MaterialType.Steel:
                stiffness = 1.0f;
                amplitude = 1.0f;
                vibrationFrequency = 150f;
                vibrationDuration = 0.05f;
                minimumDelayMs = 0f;
                maximumDelayMs = 1f;
                break;
            case MaterialType.Gelatin:
                stiffness = 0.0f;
                amplitude = 0.7f;
                vibrationFrequency = 100f;
                vibrationDuration = 0.06f;
                minimumDelayMs = 80f;
                maximumDelayMs = 100f;
                break;
        }
        Debug.Log($"[TimeDelayModulation] Applied material preset: {type}");
    }

    private void OnValidate()
    {
        if (materialType != MaterialType.Custom)
            ApplyMaterialPreset(materialType);
    }

    public void SetMaterial(MaterialType type)
    {
        materialType = type;
        ApplyMaterialPreset(type);
    }

    private void Awake()
    {
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
        {
            hapticController = gameObject.AddComponent<HapticController>();
        }
        if (materialType != MaterialType.Custom)
            ApplyMaterialPreset(materialType);
        if (leftHandTransform != null) lastLeftHandPos = leftHandTransform.position;
        if (rightHandTransform != null) lastRightHandPos = rightHandTransform.position;
        lastUpdateTime = Time.time;
    }

    private void Update()
    {
        if (modulationMode == ModulationMode.MotionCoupled)
        {
            float inputValue = 0f;
            float now = Time.time;
            float deltaTime = now - lastUpdateTime;
            if (leftHandTransform != null && rightHandTransform != null)
            {
                //float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
                //float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
                //int binId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));

                if (inputType == InputType.Position)
                {
                    inputValue = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
                    inputValue = (inputValue - minimumDistance) / (maximumDistance - minimumDistance);
                }
                else // Velocity
                {
                    float leftVel = ((leftHandTransform.position - lastLeftHandPos) / Mathf.Max(deltaTime, 1e-5f)).magnitude;
                    float rightVel = ((rightHandTransform.position - lastRightHandPos) / Mathf.Max(deltaTime, 1e-5f)).magnitude;
                    inputValue = Mathf.Max(leftVel, rightVel); // or average, or sum, as desired
                    inputValue = Mathf.Clamp01(inputValue / 2.0f); // Normalize (tune denominator as needed)
                }
                int binId = Mathf.RoundToInt(inputValue * (horizontalBins - 1));

                if (binId != lastBinId)
                {
                    Debug.Log($"[TimeDelayModulation] Bin changed: {lastBinId} -> {binId} (input: {inputValue:F3})");
                    TriggerTimeDelayModulationInternal($"MOTION-COUPLED BIN CHANGE (bin {lastBinId}->{binId})", inputValue);
                    lastBinId = binId;
                }
                //lastDistance = distance;
                lastDistance = inputValue;
                lastLeftHandPos = leftHandTransform.position;
                lastRightHandPos = rightHandTransform.position;
                lastUpdateTime = now;
                lastInputValue = inputValue;
            }
        }
    }

    /// <summary>
    /// Call this for non-motion-coupled time delay modulation (e.g., on trigger press)
    /// </summary>
    public void TriggerTimeDelayModulationFreeSpace()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[TimeDelayModulation] Non-motion-coupled: Triggered in FREE SPACE");
            TriggerTimeDelayModulationInternal("NON-MOTION-COUPLED FREE SPACE");
        }
    }

    /// <summary>
    /// Call this for non-motion-coupled time delay modulation when attached (e.g., on trigger press)
    /// </summary>
    public void TriggerTimeDelayModulationAttached()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[TimeDelayModulation] Non-motion-coupled: Triggered ATTACHED");
            TriggerTimeDelayModulationInternal("NON-MOTION-COUPLED ATTACHED");
        }
    }

    /// <summary>
    /// Triggers the time-delay haptic modulation: actor hand gets immediate pulse, follower hand gets delayed pulse.
    /// </summary>
    public void TriggerTimeDelayModulation()
    {
        TriggerTimeDelayModulationInternal("GENERIC TRIGGER");
    }

    private float MapAmplitude(float input, float materialAmplitude)
    {
        switch (amplitudeFunction)
        {
            case AmplitudeFunctionType.Linear:
                return Mathf.Lerp(0f, 1f, input);
            case AmplitudeFunctionType.Exponential:
                return 1f - Mathf.Exp(-exponentialB * input);
            case AmplitudeFunctionType.Gaussian:
                float gauss = Mathf.Exp(-Mathf.Pow(input - gaussianMu, 2) / (2 * gaussianSigma * gaussianSigma));
                return gauss;
            case AmplitudeFunctionType.AmplitudeCondition:
                return Mathf.Clamp(materialAmplitude, 0f, 1f);
            default:
                return Mathf.Lerp(0f, 1f, input);
        }
    }

    private void TriggerTimeDelayModulationInternal(string context, float inputValue = -1f)
    {
        if (inputValue < 0f) inputValue = lastInputValue;
        float delayMs = Mathf.Lerp(maximumDelayMs, minimumDelayMs, stiffness); // 0ms (rigid) to max (soft)
        //Debug.Log($"[TimeDelayModulation] {context} | Stiffness: {stiffness:F2}, Delay: {delayMs:F1}ms, RightHandIsActor: {rightHandIsActor}");
        //float mappedAmplitude = Mathf.Clamp(MapAmplitude(inputValue) * amplitude, 0f, 1f);
        float mappedAmplitude = Mathf.Clamp(MapAmplitude(inputValue, amplitude), 0f, 1f);
        Debug.Log($"[TimeDelayModulation] {context} | Stiffness: {stiffness:F2}, Delay: {delayMs:F1}ms, RightHandIsActor: {rightHandIsActor}, Input: {inputValue:F2}, Amplitude: {mappedAmplitude:F2}");
        if (rightHandIsActor)
        {
            // Actor: right, Follower: left
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, mappedAmplitude, vibrationDuration));
            StartCoroutine(DelayedVibration(OVRInput.Controller.LTouch, delayMs, mappedAmplitude));
        }
        else
        {
            // Actor: left, Follower: right
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, mappedAmplitude, vibrationDuration));
            StartCoroutine(DelayedVibration(OVRInput.Controller.RTouch, delayMs, mappedAmplitude));
        }
    }

    private IEnumerator DelayedVibration(OVRInput.Controller controller, float delayMs, float mappedAmplitude)
    {
        if (delayMs > 0f)
            yield return new WaitForSeconds(delayMs / 1000f);
        yield return hapticController.StartVibrationForDuration(controller, vibrationFrequency, mappedAmplitude, vibrationDuration);
    }

}
