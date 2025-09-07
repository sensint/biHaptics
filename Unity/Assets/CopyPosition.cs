using UnityEngine;

/// <summary>
/// Attaches this GameObject to a target Transform (like a VR controller anchor),
/// with user-definable position and rotation offsets. This allows for precise
/// alignment of held objects like tools or rackets.
/// </summary>
public class AnchorFollow : MonoBehaviour
{
    [Tooltip("The controller or hand anchor this object should follow.")]
    public Transform anchor;

    [Tooltip("Fine-tune the local position of the object relative to the anchor.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Fine-tune the local rotation of the object relative to the anchor (in degrees).")]
    public Vector3 rotationOffset = Vector3.zero;

    void Update()
    {
        if (anchor == null)
        {
            // If no anchor is assigned, do nothing. This prevents errors.
            return;
        }

        // --- Position Calculation ---
        // Start with the anchor's position and add the offset.
        // The offset is rotated by the anchor's rotation to ensure it's always
        // relative to the controller's current orientation (e.g., "forward" is always away from the hand).
        transform.position = anchor.position + (anchor.rotation * positionOffset);

        // --- Rotation Calculation ---
        // Start with the anchor's rotation and apply the rotation offset.
        // Quaternion.Euler converts our user-friendly Vector3 offset into a Quaternion.
        transform.rotation = anchor.rotation * Quaternion.Euler(rotationOffset);
    }
}
