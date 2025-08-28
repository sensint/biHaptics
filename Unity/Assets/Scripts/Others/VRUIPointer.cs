//using UnityEngine;
//using UnityEngine.EventSystems;

///// <summary>
///// This script creates a laser pointer that originates from the VR controller
///// and interacts with UI elements. It draws a line and registers itself with the OVRInputModule.
///// </summary>
//[RequireComponent(typeof(LineRenderer))]
//public class VRUIPointer : MonoBehaviour
//{
//    [Tooltip("The distance the ray will travel if it doesn't hit anything.")]
//    public float defaultRayLength = 2.0f;

//    private LineRenderer lineRenderer;
//    private OVRInputModule ovrInputModule;

//    void Start()
//    {
//        lineRenderer = GetComponent<LineRenderer>();

//        // Find the OVRInputModule in the scene's EventSystem.
//        ovrInputModule = FindObjectOfType<OVRInputModule>();
//        if (ovrInputModule == null)
//        {
//            Debug.LogError("OVRInputModule not found in the scene. Please add it to the EventSystem.");
//            return;
//        }

//        // We tell the input module to use this object's transform as its ray for pointing.
//        ovrInputModule.rayTransform = transform;
//    }

//    void Update()
//    {
//        // This part of the script is only for VISUALLY drawing the line.
//        // The actual UI interaction is handled by OVRInputModule.

//        Vector3 endPosition;

//        // Get the current raycast result from the OVRInputModule.
//        // This is the correct, publicly accessible way to get the raycast data.
//        RaycastResult currentRaycastResult = ovrInputModule.currentRaycastResult;

//        if (currentRaycastResult.isValid)
//        {
//            // The ray hit a UI element, so the end of the line is at the hit point.
//            endPosition = currentRaycastResult.worldPosition;
//        }
//        else
//        {
//            // The ray did not hit a UI element, use the default length.
//            endPosition = transform.position + (transform.forward * defaultRayLength);
//        }

//        // Set the Line Renderer positions to draw the visible ray.
//        lineRenderer.SetPosition(0, transform.position);
//        lineRenderer.SetPosition(1, endPosition);
//    }
//}