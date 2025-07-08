using UnityEngine;

public class RodInteraction_OVR : MonoBehaviour
{
    private AmplitudeRatioModulator amplitudeModulator;
    private OVRGrabbable grabbable;

    void Start()
    {
        amplitudeModulator = GetComponent<AmplitudeRatioModulator>();
        grabbable = GetComponent<OVRGrabbable>();
    }

    void Update()
    {
        // Find all OVRGrabber instances (usually two: left and right)
        var grabbers = FindObjectsOfType<OVRGrabber>();

        bool leftGrabbing = false;
        bool rightGrabbing = false;

        foreach (var grabber in grabbers)
        {
            if (grabber.grabbedObject == grabbable)
            {
                // Check the name of the grabber's GameObject or its parent
                string grabberName = grabber.gameObject.name.ToLower();
                string parentName = grabber.transform.parent ? grabber.transform.parent.name.ToLower() : "";

                if (grabberName.Contains("left") || parentName.Contains("left"))
                    leftGrabbing = true;
                if (grabberName.Contains("right") || parentName.Contains("right"))
                    rightGrabbing = true;
            }
        }

        // ATTACHED: Both hands grabbing rod
        if (leftGrabbing && rightGrabbing)
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                amplitudeModulator.TriggerAmplitudeModulationAttached();
            }
        }
        // FREE SPACE: Either hand not grabbing rod
        else
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                amplitudeModulator.TriggerAmplitudeModulationFreeSpace();
            }
        }
    }
}