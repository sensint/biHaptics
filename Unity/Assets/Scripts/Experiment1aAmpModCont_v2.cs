using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Experiment1aAmpModCont_v2 : MonoBehaviour
{
    public enum InputType { Absolute, Relative }
    //public enum CrosstalkLevel { CT0, CT25, CT50, CT75, CT100 }
    public enum CrosstalkLevel { CT0, CT33, CT66, CT100 }
    public enum MappingType { Stretching, Bending, Twisting, Combined }

    [Header("Experiment Condition Control")]
    public InputType inputType = InputType.Relative;
    public CrosstalkLevel crosstalk = CrosstalkLevel.CT0;
    // Updated experimentConditions to remove "CC"
    public string[] experimentConditions = new string[] { "C2", "A33", "R66", "R100", "XX", "A66", "R0", "A0", "C1", "A100", "R33", "R66", "C2", "A66", "C1", "R0", "R33", "A33", "A0", "A100", "XX", "R100" };

    public int currentConditionIndex = 0;

    [Header("Haptic Settings")]
    public float vibrationFrequency = 125f;
    public float vibrationDuration = 0.008f; // This is for short, movement-triggered pulses in A/R conditions

    [Header("Motion Coupling Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public float movementThreshold = 0.003f; // minimal movement to trigger pulse
    public float minimumDistance = 0.05f;
    public float maximumDistance = 1.0f;
    [Range(2, 500)]
    public int horizontalBins = 200;

    [Header("Mapping Type")]
    public MappingType mappingType = MappingType.Combined;

    private HapticController hapticController;
    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private int lastLeftBin = -1;
    private int lastRightBin = -1;
    private int lastRelativeBin = -1;
    //private float[] crosstalkValues = new float[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
    private float[] crosstalkValues = new float[] { 0f, 0.33f, 0.66f, 1f };

    private bool vibrationsDisabled = false;
    private bool inInterlude = false;
    private bool inContinuousVibrationMode = false; // Flag to indicate if any continuous mode is active
    private bool isVibratingBoth = false;
    private bool isVibratingLeft = false;
    private bool isVibratingRight = false;
    private ContinuousVibrationType currentContinuousVibrationType = ContinuousVibrationType.None;

    // New enum to track different continuous vibration types
    private enum ContinuousVibrationType { None, OnMovingHand, BothIfOneMoves } // Removed AlwaysBoth

    private void Awake()
    {
        hapticController = GetComponent<HapticController>();
        if (hapticController == null)
        {
            hapticController = gameObject.AddComponent<HapticController>();
        }
        if (leftHandTransform != null) lastLeftPos = leftHandTransform.position;
        if (rightHandTransform != null) lastRightPos = rightHandTransform.position;

        // Parse the initial condition when the script awakes
        if (experimentConditions.Length > 0)
        {
            ParseCondition(experimentConditions[currentConditionIndex]);
            Debug.Log($"[Experiment] Initial condition: {experimentConditions[currentConditionIndex]}");
        }
    }

    private void Update()
    {
        // Handle Space key for next condition / interlude
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!inInterlude)
            {
                // Currently in an experiment condition, transition to interlude (XX)
                inInterlude = true;
                ParseCondition("XX"); // Force "XX" (no vibration)
                Debug.Log($"[Experiment] Entering interlude (XX condition). Press Space again for next experiment.");
            }
            else
            {
                // Currently in interlude, transition to next experiment condition
                inInterlude = false;
                currentConditionIndex = (currentConditionIndex + 1) % experimentConditions.Length;
                ParseCondition(experimentConditions[currentConditionIndex]);
                Debug.Log($"[Experiment] Switched to next condition: {experimentConditions[currentConditionIndex]}");
            }
        }

        // Handle previous condition (B key) - this bypasses the interlude
        if (Input.GetKeyDown(KeyCode.B)) // 'B' key
        {
            inInterlude = false; // Exit interlude if pressing B
            currentConditionIndex = (currentConditionIndex - 1 + experimentConditions.Length) % experimentConditions.Length;
            ParseCondition(experimentConditions[currentConditionIndex]);
            Debug.Log($"[Experiment] Switched to previous condition: {experimentConditions[currentConditionIndex]}");
        }

        // Handle continuous vibration based on currentContinuousVibrationType
        if (inContinuousVibrationMode && leftHandTransform != null && rightHandTransform != null)
        {
            // Detect movement
            bool leftMoved = (leftHandTransform.position - lastLeftPos).magnitude > movementThreshold;
            bool rightMoved = (rightHandTransform.position - lastRightPos).magnitude > movementThreshold;

            switch (currentContinuousVibrationType)
            {
                // case ContinuousVibrationType.AlwaysBoth: // Removed CC logic
                //    break;
                case ContinuousVibrationType.OnMovingHand:
                    if (leftMoved) hapticController.StartVibration(OVRInput.Controller.LTouch, vibrationFrequency, 1f);
                    else hapticController.StopVibration(OVRInput.Controller.LTouch);

                    if (rightMoved) hapticController.StartVibration(OVRInput.Controller.RTouch, vibrationFrequency, 1f);
                    else hapticController.StopVibration(OVRInput.Controller.RTouch);
                    break;
                case ContinuousVibrationType.BothIfOneMoves:
                    if (leftMoved || rightMoved)
                    {
                        hapticController.StartVibration(OVRInput.Controller.LTouch, vibrationFrequency, 1f);
                        hapticController.StartVibration(OVRInput.Controller.RTouch, vibrationFrequency, 1f);
                    }
                    else
                    {
                        hapticController.StopVibration(OVRInput.Controller.LTouch);
                        hapticController.StopVibration(OVRInput.Controller.RTouch);
                    }
                    break;
            }
        }

        // Only detect movement and trigger SHORT PULSE vibrations if not in a continuous vibration mode and not disabled
        if (!inContinuousVibrationMode && !vibrationsDisabled && leftHandTransform != null && rightHandTransform != null)
        {
            // Detect movement
            bool leftMoved = (leftHandTransform.position - lastLeftPos).magnitude > movementThreshold;
            bool rightMoved = (rightHandTransform.position - lastRightPos).magnitude > movementThreshold;

            if (leftMoved || rightMoved)
            {
                if (inputType == InputType.Relative)
                {
                    switch (mappingType)
                    {
                        case MappingType.Stretching:
                            HandleRelativeStretchingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Bending:
                            HandleRelativeBendingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Twisting:
                            HandleRelativeTwistingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Combined:
                            HandleRelativeStretchingBins(leftMoved, rightMoved);
                            HandleRelativeBendingBins(leftMoved, rightMoved);
                            HandleRelativeTwistingBins(leftMoved, rightMoved);
                            break;
                    }
                }
                else
                {
                    switch (mappingType)
                    {
                        case MappingType.Stretching:
                            HandleAbsoluteStretchingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Bending:
                            HandleAbsoluteBendingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Twisting:
                            HandleAbsoluteTwistingBins(leftMoved, rightMoved);
                            break;
                        case MappingType.Combined:
                            HandleAbsoluteStretchingBins(leftMoved, rightMoved);
                            HandleAbsoluteBendingBins(leftMoved, rightMoved);
                            HandleAbsoluteTwistingBins(leftMoved, rightMoved);
                            break;
                    }
                }
            }
        }

        lastLeftPos = leftHandTransform.position;
        lastRightPos = rightHandTransform.position;
    }

    private void ParseCondition(string cond)
    {
        // Stop any existing vibrations (continuous or pulse) before parsing new condition
        hapticController.StopVibration(OVRInput.Controller.LTouch);
        hapticController.StopVibration(OVRInput.Controller.RTouch);

        inContinuousVibrationMode = false; // Reset continuous vibration flag for new condition
        currentContinuousVibrationType = ContinuousVibrationType.None; // Reset specific type
        vibrationsDisabled = false; // Reset general vibration disabled flag

        switch (cond)
        {
            case "XX":
                vibrationsDisabled = true;
                Debug.Log("[Haptics] All vibrations disabled (XX condition).");
                break;
            case "C1": // Continuous, Only Moving Hand Vibrates
                inContinuousVibrationMode = true;
                currentContinuousVibrationType = ContinuousVibrationType.OnMovingHand;
                Debug.Log("[Haptics] Continuous vibration only on the moving hand (C_Mov condition).");
                break;
            case "C2": // Continuous, Both Hands Vibrate If One Moves
                inContinuousVibrationMode = true;
                currentContinuousVibrationType = ContinuousVibrationType.BothIfOneMoves;
                Debug.Log("[Haptics] Continuous vibration on both hands if one moves (C_Both condition).");
                break;
            default:
                // Existing logic for A/R conditions (short pulses based on movement)
                inputType = cond[0] == 'A' ? InputType.Absolute : InputType.Relative;
                int ct = int.Parse(cond.Substring(1));
                switch (ct)
                {
                    case 0: crosstalk = CrosstalkLevel.CT0; break;
                    //case 25: crosstalk = CrosstalkLevel.CT25; break;
                    case 33: crosstalk = CrosstalkLevel.CT33; break;
                    case 66: crosstalk = CrosstalkLevel.CT66; break;
                    //case 75: crosstalk = CrosstalkLevel.CT75; break;
                    case 100: crosstalk = CrosstalkLevel.CT100; break;
                }
                Debug.Log($"[Haptics] Set to {cond} (movement-based, short-pulse haptics).");
                break;
        }
    }

    // The following functions remain mostly unchanged,
    // they only get called if not in a continuous vibration mode.

    private void HandleRelativeStretchingBins(bool leftMoved, bool rightMoved)
    {
        float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
        float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
        int binId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));
        if (binId != lastRelativeBin)
        {
            TriggerMovementVibration(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
    }

    private void HandleRelativeBendingBins(bool leftMoved, bool rightMoved)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float rmsAngle = Mathf.Sqrt((relAngles.x * relAngles.x + relAngles.y * relAngles.y + relAngles.z * relAngles.z) / 3f);
        float normalizedAngle = Mathf.Clamp01(rmsAngle / 180f);
        int binId = Mathf.RoundToInt(normalizedAngle * (horizontalBins - 1));
        if (binId != lastRelativeBin)
        {
            TriggerMovementVibration(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
    }

    private void HandleRelativeTwistingBins(bool leftMoved, bool rightMoved)
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float twist = relAngles.z;
        float normalizedTwist = Mathf.Clamp01((twist + 180f) / 360f);
        int binId = Mathf.RoundToInt(normalizedTwist * (horizontalBins - 1));
        if (binId != lastRelativeBin)
        {
            TriggerMovementVibration(leftMoved, rightMoved);
            lastRelativeBin = binId;
        }
    }

    private void HandleAbsoluteStretchingBins(bool leftMoved, bool rightMoved)
    {
        int leftBin = Mathf.RoundToInt(Mathf.Clamp01((leftHandTransform.position.x - minimumDistance) / (maximumDistance - minimumDistance)) * (horizontalBins - 1));
        int rightBin = Mathf.RoundToInt(Mathf.Clamp01((rightHandTransform.position.x - minimumDistance) / (maximumDistance - minimumDistance)) * (horizontalBins - 1));
        if (leftBin != lastLeftBin)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin)
        {
            TriggerMovementVibration(false, true);
            lastRightBin = rightBin;
        }
    }

    private void HandleAbsoluteBendingBins(bool leftMoved, bool rightMoved)
    {
        int leftBin = Mathf.RoundToInt(Mathf.Clamp01(Mathf.Abs(NormalizeAngle(leftHandTransform.eulerAngles.x)) / 180f) * (horizontalBins - 1));
        int rightBin = Mathf.RoundToInt(Mathf.Clamp01(Mathf.Abs(NormalizeAngle(rightHandTransform.eulerAngles.x)) / 180f) * (horizontalBins - 1));
        if (leftBin != lastLeftBin)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin)
        {
            TriggerMovementVibration(false, true);
            lastRightBin = rightBin;
        }
    }

    private void HandleAbsoluteTwistingBins(bool leftMoved, bool rightMoved)
    {
        int leftBin = Mathf.RoundToInt(Mathf.Clamp01((NormalizeAngle(leftHandTransform.eulerAngles.z) + 180f) / 360f) * (horizontalBins - 1));
        int rightBin = Mathf.RoundToInt(Mathf.Clamp01((NormalizeAngle(rightHandTransform.eulerAngles.z) + 180f) / 360f) * (horizontalBins - 1));
        if (leftBin != lastLeftBin)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin)
        {
            TriggerMovementVibration(false, true);
            lastRightBin = rightBin;
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
        return angle;
    }

    private void TriggerMovementVibration(bool leftMoved, bool rightMoved)
    {
        // This method is only called for short pulses in A/R conditions.
        // Continuous vibration logic is handled separately in Update.
        if (vibrationsDisabled || inContinuousVibrationMode) return;

        float ctValue = crosstalkValues[(int)crosstalk];
        if (leftMoved && rightMoved)
        {
            if (isVibratingBoth)
            {
                hapticController.StopVibration(OVRInput.Controller.LTouch);
                hapticController.StopVibration(OVRInput.Controller.RTouch);
            }
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            return;
        }
        if (leftMoved)
        {
            // left is moving: full on left, ct% on right
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f)
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
        else if (rightMoved)
        {
            // right is moving: full on right, ct% on left
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f)
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
    }
}