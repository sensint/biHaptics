using UnityEngine;
using System.Collections;

public class AmplitudeRatioModulator : MonoBehaviour
{
    public enum ModulationMode { NonMotionCoupled, MotionCoupled }
    public enum MaterialType {  Custom, Sponge, Rubber, Steel }
    public enum InputType {  Position, Velocity }
    public enum AmplitudeFunctionType { Linear, Exponential, Gaussian, AmplitudeCondition }   

    [Header("Material Preset (Motion-Coupled Only)")]
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
    [Tooltip("Amplitude for actor hand when stiffness=0 (soft)")]
    [Range(0f, 1f)]
    public float softActorAmplitude = 0.8f;
    [Tooltip("Amplitude for follower hand when stiffness=0 (soft)")]
    [Range(0f, 1f)]
    public float softFollowerAmplitude = 0.2f;
    [Tooltip("Amplitude for both hands when stiffness=1 (rigid)")]
    [Range(0f, 1f)]
    public float rigidAmplitude = 1.0f;
    [Header("Ampltidue Clamp")]
    [Range(0f, 1f)]
    public float minimumAmplitude = 0.0f;
    [Range(0f, 1f)]
    public float maximumAmplitude = 1.0f;

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

    [Header("Motion Coupling Settings")]
    public ModulationMode modulationMode = ModulationMode.MotionCoupled;
    [Tooltip("Movement threshold to trigger haptics (meters)")]
    public float movementThreshold = 0.005f;
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

    [Header("Connectedness")]
    [Tooltip("0 = hands independent, 0.5 = fully blended/connected")]
    [Range(0f, 0.5f)]
    public float connectedness = 0.15f;

    private HapticController hapticController;
    private int lastBinId = -1;
    private float lastDistance = 0f;
    private Vector3 lastleftHandPos;
    private Vector3 lastrightHandPos;
    private float lastUpdateTime;
    private float lastInputValue = 0f;

    // Preset parameter sets for different materials (motion-coupled only)
    // Sponge: soft, low follower, moderate connectedness
    // Rubber: medium stiffness, moderate follower, higher connectedness
    // Steel: rigid, high follower, max connectedness
    private void ApplyMaterialPreset(MaterialType type)
    {
        if (type == MaterialType.Custom)
        {
            Debug.Log("[AmplitudeRatioModulator] Custom material: parameters are user-controlled.");
            return;
        }
        switch (type)
        {
            case MaterialType.Sponge:
                stiffness = 0.0f;
                softActorAmplitude = 0.8f;
                softFollowerAmplitude = 0.1f;
                rigidAmplitude = 0.7f;
                connectedness = 0.10f;
                vibrationFrequency = 120f;
                vibrationDuration = 0.04f;
                break;
            case MaterialType.Rubber:
                stiffness = 0.5f;
                softActorAmplitude = 0.7f;
                softFollowerAmplitude = 0.4f;
                rigidAmplitude = 0.9f;
                connectedness = 0.25f;
                vibrationFrequency = 130f;
                vibrationDuration = 0.045f;
                break;
            case MaterialType.Steel:
                stiffness = 1.0f;
                softActorAmplitude = 1.0f;
                softFollowerAmplitude = 1.0f;
                rigidAmplitude = 1.0f;
                connectedness = 0.5f;
                vibrationFrequency = 150f;
                vibrationDuration = 0.05f;
                break;
        }
        Debug.Log($"[AmplitudeRatioModulator] Applied material preset: {type}");
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
        // Ensure there is a HapticController component on this GameObject
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
        {
            hapticController = gameObject.AddComponent<HapticController>();
        }
        if (materialType != MaterialType.Custom)
            ApplyMaterialPreset(materialType);
        if (leftHandTransform != null) lastleftHandPos = leftHandTransform.position;
        if (rightHandTransform != null) lastrightHandPos = rightHandTransform.position;
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
                //float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
                //float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
                //int binId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));

                if (inputType == InputType.Position)
                {
                    inputValue = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
                    inputValue = (inputValue - minimumDistance) / (maximumDistance - minimumDistance);
                }
                else
                {
                    float leftVel = ((leftHandTransform.position - lastleftHandPos) / Mathf.Max(deltaTime, 1e-5f)).magnitude;
                    float rightVel = ((rightHandTransform.position - lastrightHandPos) / Mathf.Max(deltaTime, 1e-5f)).magnitude;
                    inputValue = Mathf.Max(leftVel, rightVel);
                    inputValue = Mathf.Clamp01(inputValue / 2.0f);
                }
                int binId = Mathf.RoundToInt(inputValue * (horizontalBins - 1));

                if (binId != lastBinId)
                {
                    Debug.Log($"[AmplitudeRatioModulator] Bin changed: {lastBinId} -> {binId} (input: {inputValue:F3})");
                    TriggerAmplitudeModulationInternal($"MOTION-COUPLED BIN CHANGE (bin {lastBinId}->{binId})", inputValue);
                    lastBinId = binId;
                }
                //lastDistance = distance;
                lastDistance = inputValue;
                lastleftHandPos = leftHandTransform.position;
                lastrightHandPos = rightHandTransform.position;
                lastUpdateTime = now;
                lastInputValue = inputValue;
            }

        }
    }

    public void TriggerAmplitudeModulationFreeSpace()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[AmplitudeRatioModulator] Non-motion-couipled: Trigger ATTACHED");
            TriggerAmplitudeModulationInternal("NON-MOTION-COUPLED ATTACHED");
        }
    }

    public void TriggerAmplitudeModulationAttached()
    {
        if (modulationMode == ModulationMode.NonMotionCoupled)
        {
            Debug.Log("[AmplitudeRatioModulator] Non-motion-coupled: Triggered ATTACHED");
            TriggerAmplitudeModulationInternal("NON-MOTION-COUPLED ATTACHED");
        }
    }

    private float MapAmplitude(float input, float materialAmplitude)
    {
        switch (amplitudeFunction)
        {
            case AmplitudeFunctionType.Linear:
                return Mathf.Lerp(minimumAmplitude, maximumAmplitude, input);
            case AmplitudeFunctionType.Exponential:
                return minimumAmplitude + (maximumAmplitude - minimumAmplitude) * (1 - Mathf.Exp(-exponentialB * input));
            case AmplitudeFunctionType.Gaussian:
                float gauss = Mathf.Exp(-Mathf.Pow(input - gaussianMu, 2) / (2 * gaussianSigma * gaussianSigma));
                return minimumAmplitude + (maximumAmplitude - minimumAmplitude) * gauss;
            case AmplitudeFunctionType.AmplitudeCondition:
                return Mathf.Clamp(materialAmplitude, minimumAmplitude, maximumAmplitude);
            default:
                return Mathf.Lerp(minimumAmplitude, maximumAmplitude, input);
        }
    }

    /// <summary>
    /// Triggers amplitude ratio modulation for both hands, context-aware (free or attached).
    /// </summary>
    /// <param name="attached">True if hands are attached to rod, false if in free space.</param>
    public void TriggerAmplitudeModulationInternal(string context, float inputValue = -1f)
    {   
        if (inputValue < 0f) inputValue = lastInputValue;
        // Calculate amplitudes based on stiffness
        float actorAmp = Mathf.Lerp(softActorAmplitude, rigidAmplitude, stiffness);
        float followerAmp = Mathf.Lerp(softFollowerAmplitude, rigidAmplitude, stiffness);
        float actorFinal = Mathf.Lerp(actorAmp, followerAmp, connectedness);
        float followerFinal = Mathf.Lerp(followerAmp, actorAmp, connectedness);
        //float clampedActor = Mathf.Clamp(actorFinal, minimumAmplitude, maximumAmplitude);
        //float clampedFollower = Mathf.Clamp(followerFinal, minimumAmplitude, maximumAmplitude);
        //Debug.Log($"[AmplitudeRatioModulator] {context} | Stiffness: {stiffness:F2}, ActorAmp: {clampedActor:F2}, FollowerAmp: {clampedFollower:F2}, RightHandIsActor: {rightHandIsActor}, Connectedness: {connectedness:F2}");
        float clampedActor = MapAmplitude(inputValue, actorFinal);
        float clampedFollower = MapAmplitude(inputValue, followerFinal);
        clampedActor = Mathf.Clamp(clampedActor, minimumAmplitude, maximumAmplitude);
        clampedFollower = Mathf.Clamp(clampedFollower, minimumAmplitude, maximumAmplitude);
        Debug.Log($"[AmplitudeRatioModulator] {context} | Stiffness: {stiffness:F2}, ActorAmp: {clampedActor:F2}, FollowerAmp: {clampedFollower:F2}, RightHandIsActor: {rightHandIsActor}, Connectedness: {connectedness:F2}, Input: {inputValue:F2}");

        if (rightHandIsActor)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, clampedActor, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, clampedFollower, vibrationDuration));
        }
        else
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, clampedActor, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, clampedFollower, vibrationDuration));
        }
    }
}