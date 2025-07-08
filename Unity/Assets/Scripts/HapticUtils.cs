using UnityEngine;

public static class HapticUtils
{
    // Start vibration on a single controller
    public static void StartVibration(OVRInput.Controller controller, float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
    }

    // Start vibration on both controllers
    public static void StartVibrationBoth(float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
    }

    // Stop vibration on a single controller
    public static void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0, 0, controller);
    }

    // Stop vibration on both controllers
    public static void StopVibrationBoth()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    // Start vibration for a duration (coroutine helper)
    public static System.Collections.IEnumerator StartVibrationForDuration(OVRInput.Controller controller, float frequency, float amplitude, float duration)
    {
        StartVibration(controller, frequency, amplitude);
        yield return new WaitForSeconds(duration);
        StopVibration(controller);
    }

    // Start vibration on both controllers for a duration (coroutine helper)
    public static System.Collections.IEnumerator StartVibrationBothForDuration(float frequency, float amplitude, float duration)
    {
        StartVibrationBoth(frequency, amplitude);
        yield return new WaitForSeconds(duration);
        StopVibrationBoth();
    }
}