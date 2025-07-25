using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Experiment1aAmpModCont_v3 : MonoBehaviour
{
    public enum InputType { Absolute, Relative }
    public enum CrosstalkLevel { CT0, CT33, CT66, CT100 }
    public enum MappingType { Stretching, Bending, Twisting, Combined }

    [Header("Experiment Condition Control")]
    public InputType inputType = InputType.Relative;
    public CrosstalkLevel crosstalk = CrosstalkLevel.CT0;
    public string[] experimentConditions = new string[] { "C1", "A100", "A66", "XX", "A33", "R0", "R66", "R33", "C2", "A0", "R100", "A0", "A100", "R33", "R100", "C1", "R0", "R66", "A66", "C2", "XX", "A33" };

    public int currentConditionIndex = 0;

    [Header("Haptic Settings")]
    public float vibrationFrequency = 125f;
    public float vibrationDuration = 0.008f; // For short pulses
    public float continuousAmplitude = 1f;

    [Header("Motion Coupling Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    [Tooltip("Minimal movement to trigger continuous vibration. For A/R, movement is detected by bin changes and used for amplitude assignment.")]
    public float movementThreshold = 0.003f; // Used for continuous vibration conditions C1/C2 AND for amplitude assignment in Rxx
    public float minimumDistance = 0.05f;
    public float maximumDistance = 1.0f;
    [Range(2, 500)]
    public int horizontalBins = 200;

    [Header("Mapping Type")]
    public MappingType mappingType = MappingType.Combined;

    private HapticController hapticController;
    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;

    // For A/R conditions, track last bin per hand and per axis for Combined mapping
    private int lastLeftStretchingBin = -1;
    private int lastLeftBendingBin = -1;
    private int lastLeftTwistingBin = -1;

    private int lastRightStretchingBin = -1;
    private int lastRightBendingBin = -1;
    private int lastRightTwistingBin = -1;

    private int lastRelativeBin = -1; // Used for Relative input type

    private float[] crosstalkValues = new float[] { 0f, 0.33f, 0.66f, 1f };

    private bool vibrationsDisabled = false;
    private bool inInterlude = false;
    private bool inContinuousVibrationMode = false;
    private ContinuousVibrationType currentContinuousVibrationType = ContinuousVibrationType.None;

    private enum ContinuousVibrationType { None, OnMovingHand, BothIfOneMoves }

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
        // Handle Space key for next condition / interlude
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!inInterlude)
            {
                inInterlude = true;
                ParseCondition("XX");
                Debug.Log($"[Experiment] Entering interlude (XX condition). Press Space again for next experiment.");
            }
            else
            {
                inInterlude = false;
                currentConditionIndex = (currentConditionIndex + 1) % experimentConditions.Length;
                ParseCondition(experimentConditions[currentConditionIndex]);
                Debug.Log($"[Experiment] Switched to next condition: {experimentConditions[currentConditionIndex]}");
            }
        }

        // Handle previous condition (B key) - this bypasses the interlude
        if (Input.GetKeyDown(KeyCode.B))
        {
            inInterlude = false;
            currentConditionIndex = (currentConditionIndex - 1 + experimentConditions.Length) % experimentConditions.Length;
            ParseCondition(experimentConditions[currentConditionIndex]);
            Debug.Log($"[Experiment] Switched to previous condition: {experimentConditions[currentConditionIndex]}");
        }

        // --- HAPTIC LOGIC START ---
        if (leftHandTransform == null || rightHandTransform == null) return;

        // Current positions for movement detection
        Vector3 currentLeftPos = leftHandTransform.position;
        Vector3 currentRightPos = rightHandTransform.position;

        // Detect *actual* movement based on movementThreshold
        // This is used for continuous conditions AND for amplitude assignment in Rxx conditions
        float leftMovementMagnitude = (currentLeftPos - lastLeftPos).magnitude;
        float rightMovementMagnitude = (currentRightPos - lastRightPos).magnitude;

        bool leftMoved = leftMovementMagnitude > movementThreshold;
        bool rightMoved = rightMovementMagnitude > movementThreshold;

        if (inContinuousVibrationMode)
        {
            switch (currentContinuousVibrationType)
            {
                case ContinuousVibrationType.OnMovingHand: // C1
                    if (leftMoved) hapticController.StartVibration(OVRInput.Controller.LTouch, vibrationFrequency, continuousAmplitude);
                    else hapticController.StopVibration(OVRInput.Controller.LTouch);

                    if (rightMoved) hapticController.StartVibration(OVRInput.Controller.RTouch, vibrationFrequency, continuousAmplitude);
                    else hapticController.StopVibration(OVRInput.Controller.RTouch);
                    break;
                case ContinuousVibrationType.BothIfOneMoves: // C2
                    if (leftMoved || rightMoved)
                    {
                        hapticController.StartVibration(OVRInput.Controller.LTouch, vibrationFrequency, continuousAmplitude);
                        hapticController.StartVibration(OVRInput.Controller.RTouch, vibrationFrequency, continuousAmplitude);
                    }
                    else
                    {
                        hapticController.StopVibration(OVRInput.Controller.LTouch);
                        hapticController.StopVibration(OVRInput.Controller.RTouch);
                    }
                    break;
            }
        }
        else if (!vibrationsDisabled) // This is for A/R (movement-based, short-pulse) conditions
        {
            if (inputType == InputType.Relative)
            {
                // Pass movement magnitudes/flags to HandleRelativeBins to determine primary mover
                HandleRelativeBins(leftMoved, rightMoved);
            }
            else // Absolute InputType
            {
                // Pass movement flags to HandleAbsoluteBins for triggering specific hand
                HandleAbsoluteBins(leftMoved, rightMoved);
            }
        }

        lastLeftPos = currentLeftPos;
        lastRightPos = currentRightPos;
    }

    private void ParseCondition(string cond)
    {
        hapticController.StopVibration(OVRInput.Controller.LTouch);
        hapticController.StopVibration(OVRInput.Controller.RTouch);

        inContinuousVibrationMode = false;
        currentContinuousVibrationType = ContinuousVibrationType.None;
        vibrationsDisabled = false;

        // Reset all bin memories for a clean start of the new condition
        lastLeftStretchingBin = -1;
        lastLeftBendingBin = -1;
        lastLeftTwistingBin = -1;
        lastRightStretchingBin = -1;
        lastRightBendingBin = -1;
        lastRightTwistingBin = -1;
        lastRelativeBin = -1;

        switch (cond)
        {
            case "XX":
                vibrationsDisabled = true;
                Debug.Log("[Haptics] All vibrations disabled (XX condition).");
                break;
            case "C1": // C1: Continuous, Only Moving Hand Vibrates
                inContinuousVibrationMode = true;
                currentContinuousVibrationType = ContinuousVibrationType.OnMovingHand;
                Debug.Log("[Haptics] Continuous vibration only on the moving hand (C1 condition).");
                break;
            case "C2": // C2: Continuous, Both Hands Vibrate If One Moves
                inContinuousVibrationMode = true;
                currentContinuousVibrationType = ContinuousVibrationType.BothIfOneMoves;
                Debug.Log("[Haptics] Continuous vibration on both hands if one moves (C2 condition).");
                break;
            default:
                inputType = cond[0] == 'A' ? InputType.Absolute : InputType.Relative;
                int ct = int.Parse(cond.Substring(1));
                switch (ct)
                {
                    case 0: crosstalk = CrosstalkLevel.CT0; break;
                    case 33: crosstalk = CrosstalkLevel.CT33; break;
                    case 66: crosstalk = CrosstalkLevel.CT66; break;
                    case 100: crosstalk = CrosstalkLevel.CT100; break;
                    default: Debug.LogWarning($"[Experiment] Unknown crosstalk level: {ct}. Defaulting to CT0."); crosstalk = CrosstalkLevel.CT0; break;
                }
                Debug.Log($"[Haptics] Set to {cond} (movement-based, short-pulse haptics).");
                break;
        }
    }

    // Handles relative input type (Rxx conditions)
    // Now takes movement info to decide which hand is primary
    private void HandleRelativeBins(bool leftMoved, bool rightMoved)
    {
        int currentRelativeBin = -1;

        switch (mappingType)
        {
            //case MappingType.Stretching: currentRelativeBin = CalculateRelativeStretchingBin(); break;
            case MappingType.Bending: currentRelativeBin = CalculateRelativeBendingBin(); break;
            case MappingType.Twisting: currentRelativeBin = CalculateRelativeTwistingBin(); break;
            //case MappingType.Combined:
            //    int stretchBin = CalculateRelativeStretchingBin();
            //    int bendBin = CalculateRelativeBendingBin();
            //    int twistBin = CalculateRelativeTwistingBin();

            //    if (stretchBin != lastRelativeBin || bendBin != lastRelativeBin || twistBin != lastRelativeBin)
            //    {
            //        currentRelativeBin = stretchBin; // Just set to one of them if any changed to update lastRelativeBin
            //    }
            //    break;
        }

        if (currentRelativeBin != -1 && currentRelativeBin != lastRelativeBin)
        {
            // Determine the "primary" moving hand for amplitude assignment
            if (leftMoved && rightMoved)
            {
            }
            else if (leftMoved)
            {
                TriggerRelativeVibration(true); // Only left moved, left is primary
            }
            else if (rightMoved)
            {
                TriggerRelativeVibration(false); // Only right moved, right is primary
            }
            else
            {
                // No individual hand moved above threshold, but relative bin changed (e.g., very slow movement)
                // Default to left as primary, or re-evaluate if needed (e.g., if one was static and the other moved only slightly)
                TriggerRelativeVibration(true); // Fallback: default to left as primary
            }

            lastRelativeBin = currentRelativeBin;
        }
    }

    // Handles absolute input type (Axx conditions)
    private void HandleAbsoluteBins(bool leftHandActuallyMoved, bool rightHandActuallyMoved)
    {
        // Left Hand Logic
        int currentLeftStretchingBin = CalculateAbsoluteStretchingBin(leftHandTransform);
        int currentLeftBendingBin = CalculateAbsoluteBendingBin(leftHandTransform);
        int currentLeftTwistingBin = CalculateAbsoluteTwistingBin(leftHandTransform);

        bool leftBinChanged = false;
        if (mappingType == MappingType.Stretching && currentLeftStretchingBin != lastLeftStretchingBin) leftBinChanged = true;
        else if (mappingType == MappingType.Bending && currentLeftBendingBin != lastLeftBendingBin) leftBinChanged = true;
        else if (mappingType == MappingType.Twisting && currentLeftTwistingBin != lastLeftTwistingBin) leftBinChanged = true;
        else if (mappingType == MappingType.Combined &&
            (currentLeftStretchingBin != lastLeftStretchingBin ||
             currentLeftBendingBin != lastLeftBendingBin ||
             currentLeftTwistingBin != lastLeftTwistingBin))
        {
            leftBinChanged = true;
        }

        if (leftHandActuallyMoved && leftBinChanged)
        {
            TriggerAbsoluteVibration(true, false);
        }

        // Always update last known bins for the left hand for accurate tracking next frame
        lastLeftStretchingBin = currentLeftStretchingBin;
        lastLeftBendingBin = currentLeftBendingBin;
        lastLeftTwistingBin = currentLeftTwistingBin;


        // Right Hand Logic (similar to left hand)
        int currentRightStretchingBin = CalculateAbsoluteStretchingBin(rightHandTransform);
        int currentRightBendingBin = CalculateAbsoluteBendingBin(rightHandTransform);
        int currentRightTwistingBin = CalculateAbsoluteTwistingBin(rightHandTransform);

        bool rightBinChanged = false;
        if (mappingType == MappingType.Stretching && currentRightStretchingBin != lastRightStretchingBin) rightBinChanged = true;
        else if (mappingType == MappingType.Bending && currentRightBendingBin != lastRightBendingBin) rightBinChanged = true;
        else if (mappingType == MappingType.Twisting && currentRightTwistingBin != lastRightTwistingBin) rightBinChanged = true;
        else if (mappingType == MappingType.Combined &&
            (currentRightStretchingBin != lastRightStretchingBin ||
             currentRightBendingBin != lastRightBendingBin ||
             currentRightTwistingBin != lastRightTwistingBin))
        {
            rightBinChanged = true;
        }

        if (rightHandActuallyMoved && rightBinChanged)
        {
            TriggerAbsoluteVibration(false, true);
        }

        // Always update last known bins for the right hand
        lastRightStretchingBin = currentRightStretchingBin;
        lastRightBendingBin = currentRightBendingBin;
        lastRightTwistingBin = currentRightTwistingBin;
    }


    // --- Bin Calculation methods ---
    //private int CalculateRelativeStretchingBin()
    //{
    //    float distance = Mathf.Clamp((rightHandTransform.position - leftHandTransform.position).magnitude, minimumDistance, maximumDistance);
    //    float normalizedDistance = (distance - minimumDistance) / (maximumDistance - minimumDistance);
    //    int binId = Mathf.RoundToInt(normalizedDistance * (horizontalBins - 1));
    //    if (binId != lastRelativeBin)
    //    {
    //        TriggerMovementVibration(leftMoved, rightMoved);
    //        lastRelativeBin = binId;
    //    }
    //}

    private int CalculateRelativeBendingBin()
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float rmsAngle = Mathf.Sqrt((relAngles.x * relAngles.x + relAngles.y * relAngles.y + relAngles.z * relAngles.z) / 3f);
        float normalizedAngle = Mathf.Clamp01(rmsAngle / 180f);
        return Mathf.RoundToInt(normalizedAngle * (horizontalBins - 1));
    }

    private int CalculateRelativeTwistingBin()
    {
        Quaternion relRot = Quaternion.Inverse(leftHandTransform.rotation) * rightHandTransform.rotation;
        Vector3 relAngles = NormalizeAngles(relRot.eulerAngles);
        float twist = relAngles.z;
        float normalizedTwist = Mathf.Clamp01((twist + 180f) / 360f);
        return Mathf.RoundToInt(normalizedTwist * (horizontalBins - 1));
    }

    private int CalculateAbsoluteStretchingBin(Transform handTransform)
    {
        // Assuming horizontal bin is based on X position for stretching in absolute mode
        float posX = handTransform.position.x;
        float normalizedPos = Mathf.Clamp01((posX - minimumDistance) / (maximumDistance - minimumDistance));
        return Mathf.RoundToInt(normalizedPos * (horizontalBins - 1));
    }

    private int CalculateAbsoluteBendingBin(Transform handTransform)
    {
        // Assuming bending is around X axis, mapping 0-180 degrees to bins
        float angleX = NormalizeAngle(handTransform.localEulerAngles.x); // Use localEulerAngles for consistent hand orientation
        float normalizedAngle = Mathf.Clamp01(Mathf.Abs(angleX) / 180f); // Map absolute angle (0-180)
        return Mathf.RoundToInt(normalizedAngle * (horizontalBins - 1));
    }

    private int CalculateAbsoluteTwistingBin(Transform handTransform)
    {
        // Assuming twisting is around Z axis, mapping full 360 degrees to bins
        float angleZ = NormalizeAngle(handTransform.localEulerAngles.z); // Use localEulerAngles
        float normalizedAngle = Mathf.Clamp01((angleZ + 180f) / 360f); // Map -180 to 180 to 0-1 range
        return Mathf.RoundToInt(normalizedAngle * (horizontalBins - 1));
    }

    // Triggering for Relative (Rxx) conditions - now determines primary hand
    private void TriggerRelativeVibration(bool leftIsPrimary)
    {
        float ctValue = crosstalkValues[(int)crosstalk];

        if (leftIsPrimary)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
        else // Right is primary
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
    }

    // Triggering for Absolute (Axx) conditions - specific hand moves, crosstalk to other
    private void TriggerAbsoluteVibration(bool leftTrigger, bool rightTrigger)
    {
        float ctValue = crosstalkValues[(int)crosstalk];
        if (leftTrigger)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f) // Only apply crosstalk if CT is not 0
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
        if (rightTrigger)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f) // Only apply crosstalk if CT is not 0
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, ctValue, vibrationDuration));
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