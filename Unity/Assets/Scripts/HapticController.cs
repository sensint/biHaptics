using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HapticController : MonoBehaviour
{
    // State flags
    public bool IsVibratingLeft { get; private set; } = false; 
    public bool IsVibratingRight { get; private set; } = false;
    public bool IsVibratingBoth { get; private set; } = false;

    // Start times
    public float VibrationStartTimeLeft { get; private set; } = 0f;
    public float VibrationStartTimeRight { get; private set; } = 0f;
    public float VibrationStartTimeBoth { get; private set ; } = 0f;

    // Start Vibration on a single controller
    public void StartVibration(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        if (controller == OVRInput.Controller.RTouch)
        {
            IsVibratingRight = true;
            VibrationStartTimeRight = Time.time;
        }
        if (controller == OVRInput.Controller.LTouch)
        {
            IsVibratingLeft = true;
            VibrationStartTimeLeft = Time.time;
        }
    }

    // Start Vibration on both controllers
    public void StartVibrationBoth(float frequency, float amplitude)
    {
        Debug.Log(amplitude);
        OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
        IsVibratingBoth = true;
        VibrationStartTimeBoth = Time.time;
    }

    // Stop Vibration on single controller
    public void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
        if (controller == OVRInput.Controller.RTouch) IsVibratingRight = false;
        if (controller == OVRInput.Controller.LTouch) IsVibratingLeft = false;
    }

    // Stop vibration on both controllers
    public void StopVibrationBoth()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        IsVibratingBoth = false;
    }

    // Start vibration for a duration (coroutine helper)
    public IEnumerator StartVibrationForDuration(OVRInput.Controller controller, float frequency, float amplitude, float duration)
    {
        StartVibration(controller, frequency, amplitude);
        yield return new WaitForSeconds(duration);
        StopVibration(controller);
    }

    // Start vibration on both controllers for a duration (coroutine helper)
    public IEnumerator StartVibrationBothForDuration(float frequency, float amplitude, float duration)
    {
        StartVibrationBoth(frequency, amplitude);
        yield return new WaitForSeconds(duration);
        StopVibrationBoth();
    }
}
