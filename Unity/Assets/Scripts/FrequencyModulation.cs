using UnityEngine;
using System.Collections;

public class FrequencyModulation : MonoBehaviour
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
    [Tooltip("Haptic amplitude (0-1)")]
    [Range(0f, 1f)]
    public float amplitude = 1.0f;
    [Header("Frequency Clamp (Hz)")]
    [Tooltip("Minimum frequency (Hz) for soft material")]
    [Range(40f, 320f)]
    public float minimumFrequency = 40f;
    [Tooltip("Maximum frequency (Hz) for rigid material")]
    [Range(40f, 320f)]
    public float maximumFrequency = 320f;
    [Header("Pulse Duration (seconds)")]
    [Tooltip("Minimum pulse duration (seconds)")]
    [Range(0.01f, 2f)]
    public float minimumPulseDuration = 1.0f;
    [Tooltip("Maximum pulse duration (seconds)")]
    [Range(0.01f, 2f)]
    public float maximumPulseDuration = 1.0f;

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
            Debug.Log("[FrequencyModulation] Custom material: parameters are user-controlled.");
            return;
        }
        switch (type)
        {
            case MaterialType.Sponge:
                stiffness = 0.0f;
                amplitude = 0.8f;
                minimumFrequency = 40f;
                maximumFrequency = 80f;
                minimumPulseDuration = 1.0f;
                maximumPulseDuration = 1.0f;
                break;
            case MaterialType.Rubber:
                stiffness = 0.5f;
                amplitude = 0.9f;
                minimumFrequency = 80f;
                maximumFrequency = 160f;
                minimumPulseDuration = 1.0f;
                maximumPulseDuration = 1.0f;
                break;
            case MaterialType.Steel:
                stiffness = 1.0f;
                amplitude = 1.0f;
                minimumFrequency = 200f;
                maximumFrequency = 320f;
                minimumPulseDuration = 1.0f;
                maximumPulseDuration = 1.0f;
                break;
            case MaterialType.Gelatin:
                stiffness = 0.0f;
                amplitude = 0.7f;
                minimumFrequency = 40f;
                maximumFrequency = 60f;
                minimumPulseDuration = 1.0f;
                maximumPulseDuration = 1.0f;
                break;
        }
        Debug.Log($"[FrequencyModulation] Applied material preset: {type}");
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
        float inputValue = 0f;
        float now = Time.time;
        float deltaTime = now - lastUpdateTime;
        if (modulationMode == ModulationMode.MotionCoupled)
        {
            if (leftHandTransform != null && rightHandTransform != null)
            {
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
                    Debug.Log($"[FrequencyModulation] Bin changed: {lastBinId} -> {binId} (input: {inputValue:F3})");
                    TriggerFrequencyModulationInternal($"MOTION-COUPLED BIN CHANGE (bin {lastBinId}->{binId})", inputValue);
                    lastBinId = binId;
                }
                lastDistance = inputValue;
                lastLeftHandPos = leftHandTransform.position;
                lastRightHandPos = rightHandTransform.position;
                lastUpdateTime = now;
                lastInputValue = inputValue;
            }
        }
    }

    /// <summary>
    /// Call this for non-motion-coupled frequency modulation (e.g., on trigger press)
    /// </summary>
    public void TriggerFrequencyModulationFreeSpace()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[FrequencyModulation] Non-motion-coupled: Triggered in FREE SPACE");
            TriggerFrequencyModulationInternal("NON-MOTION-COUPLED FREE SPACE");
        }
    }

    /// <summary>
    /// Call this for non-motion-coupled frequency modulation when attached (e.g., on trigger press)
    /// </summary>
    public void TriggerFrequencyModulationAttached()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[FrequencyModulation] Non-motion-coupled: Triggered ATTACHED");
            TriggerFrequencyModulationInternal("NON-MOTION-COUPLED ATTACHED");
        }
    }

    /// <summary>
    /// Triggers the frequency haptic modulation: actor hand gets one frequency, follower hand gets another (if desired)
    /// </summary>
    public void TriggerFrequencyModulation()
    {
        TriggerFrequencyModulationInternal("GENERIC TRIGGER");
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

    private void TriggerFrequencyModulationInternal(string context, float inputValue = -1f)
    {
        if (inputValue < 0f) inputValue = lastInputValue;
        float frequency = Mathf.Lerp(minimumFrequency, maximumFrequency, stiffness);
        frequency = Mathf.Clamp(frequency, 40f, 320f);
        float period = 1.0f / frequency;
        int cycles = Mathf.Max(1, Mathf.RoundToInt(minimumPulseDuration / period));
        float pulseDuration = cycles * period;
        if (pulseDuration < minimumPulseDuration)
        {
            cycles = Mathf.CeilToInt(minimumPulseDuration / period);
            pulseDuration = cycles * period;
        }
        //float mappedAmplitude = Mathf.Clamp(MapAmplitude(inputValue) * amplitude, 0f, 1f);
        float mappedAmplitude = Mathf.Clamp(MapAmplitude(inputValue, amplitude), 0f, 1f);
        Debug.Log($"[FrequencyModulation] {context} | Stiffness: {stiffness:F2}, Frequency: {frequency:F1}Hz, PulseDuration: {pulseDuration:F3}s, RightHandIsActor: {rightHandIsActor}, Input: {inputValue:F2}, Amplitude: {mappedAmplitude:F2}");
        if (rightHandIsActor)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, frequency, mappedAmplitude, pulseDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, frequency, mappedAmplitude, pulseDuration));
        }
        else
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, frequency, mappedAmplitude, pulseDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, frequency, mappedAmplitude, pulseDuration));
        }
    }
}