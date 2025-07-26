using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Experiment1aAmpModulator : MonoBehaviour
{
    public enum InputType { Absolute, Relative }
    public enum CrosstalkLevel { CT0, CT25, CT50, CT75, CT100 }
    public enum MappingType { Stretching, Bending, Twisting, Combined }

    [Header("Experiment Condition Control")]
    public InputType inputType = InputType.Relative;
    public CrosstalkLevel crosstalk = CrosstalkLevel.CT0;
    public string[] experimentConditions = new string[] { "A25", "A50", "A75", "A100", "R0", "XX", "A0", "R100", "R75", "R50", "R25", "A50", "A75", "A25", "R100", "R25", "XX", "A0", "R0", "A100", "R50", "R75" };
    public int currentConditionIndex = 0;

    [Header("Haptic Settings")]
    public float vibrationFrequency = 125f;
    public float vibrationDuration = 0.008f;

    [Header("Motion Coupling Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public float movementThreshold = 0.002f; // minimal movement to trigger pulse
    public float minimumDistance = 0.05f;
    public float maximumDistance = 1.0f;
    [Range(2, 500)]
    public int horizontalBins = 300;

    [Header("Mapping Type")]
    public MappingType mappingType = MappingType.Combined;

    private HapticController hapticController;
    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private int lastLeftBin = -1;
    private int lastRightBin = -1;
    private int lastRelativeBin = -1;
    private float[] crosstalkValues = new float[] { 0f, 0.25f, 0.5f, 0.75f, 1f };

    private bool vibrationsDisabled = false;
    private bool inInterlude = false;
    //private bool inContinuousVibration = false;

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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!inInterlude)
            {
                // Currently in experiment condition, transition to interlude
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

        // Handle previous condition (B key)
        if (Input.GetKeyDown(KeyCode.B))
        {
            inInterlude = false;
            currentConditionIndex = (currentConditionIndex - 1 + experimentConditions.Length) % experimentConditions.Length;
            ParseCondition(experimentConditions[currentConditionIndex]);
            Debug.Log($"[Experiment] Switched to previous condition: {experimentConditions[currentConditionIndex]}");
        }

        if (leftHandTransform == null || rightHandTransform == null) return;

        // Detect movement
        bool leftMoved = (leftHandTransform.position - lastLeftPos).magnitude > movementThreshold;
        bool rightMoved = (rightHandTransform.position - lastRightPos).magnitude > movementThreshold;

        Debug.Log("Here");

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

        lastLeftPos = leftHandTransform.position;
        lastRightPos = rightHandTransform.position;
    }

    private void ParseCondition(string cond)
    {
        if (cond == "XX")
        {
            Debug.Log("[Haptics] All vibrations disabled.");
            vibrationsDisabled = true;
            return;
        }
        vibrationsDisabled = false;
        inputType = cond[0] == 'A' ? InputType.Absolute : InputType.Relative;
        int ct = int.Parse(cond.Substring(1));
        switch (ct)
        {
            case 0: crosstalk = CrosstalkLevel.CT0; break;
            case 25: crosstalk = CrosstalkLevel.CT25; break;
            case 50: crosstalk = CrosstalkLevel.CT50; break;
            case 75: crosstalk = CrosstalkLevel.CT75; break;
            case 100: crosstalk = CrosstalkLevel.CT100; break;
        }
    }

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
        if (leftBin != lastLeftBin && leftMoved)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin && rightMoved)
        {
            TriggerMovementVibration(false, true);
            lastRightBin = rightBin;
        }
    }

    private void HandleAbsoluteBendingBins(bool leftMoved, bool rightMoved)
    {
        int leftBin = Mathf.RoundToInt(Mathf.Clamp01(Mathf.Abs(NormalizeAngle(leftHandTransform.eulerAngles.x)) / 180f) * (horizontalBins - 1));
        int rightBin = Mathf.RoundToInt(Mathf.Clamp01(Mathf.Abs(NormalizeAngle(rightHandTransform.eulerAngles.x)) / 180f) * (horizontalBins - 1));
        if (leftBin != lastLeftBin && leftMoved)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin && rightMoved)
        {
            TriggerMovementVibration(false, true);
            lastRightBin = rightBin;
        }
    }

    private void HandleAbsoluteTwistingBins(bool leftMoved, bool rightMoved)
    {
        int leftBin = Mathf.RoundToInt(Mathf.Clamp01((NormalizeAngle(leftHandTransform.eulerAngles.z) + 180f) / 360f) * (horizontalBins - 1));
        int rightBin = Mathf.RoundToInt(Mathf.Clamp01((NormalizeAngle(rightHandTransform.eulerAngles.z) + 180f) / 360f) * (horizontalBins - 1));
        if (leftBin != lastLeftBin && leftMoved)
        {
            TriggerMovementVibration(true, false);
            lastLeftBin = leftBin;
        }
        if (rightBin != lastRightBin && rightMoved)
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
        if (vibrationsDisabled) return;

        float ctValue = crosstalkValues[(int)crosstalk];
        if (leftMoved && rightMoved)
        {
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            return;
        }
        if (leftMoved)
        {
            hapticController.StopVibration(OVRInput.Controller.RTouch);
            // left is moving: full on left, ct% on right
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f)
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
        else if (rightMoved)
        {
            hapticController.StopVibration(OVRInput.Controller.LTouch);
            // right is moving: full on right, ct% on left
            StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.RTouch, vibrationFrequency, 1f, vibrationDuration));
            if (ctValue >= 0f)
                StartCoroutine(hapticController.StartVibrationForDuration(OVRInput.Controller.LTouch, vibrationFrequency, ctValue, vibrationDuration));
        }
    }
}

