using UnityEngine;

// This script ensures that a kinematic Rigidbody correctly follows a target 
// Transform within the physics loop (FixedUpdate).
public class PhysicsAnchorFollow : MonoBehaviour
{
    public Transform targetToFollow;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (targetToFollow == null || rb == null) return;

        // Use MovePosition and MoveRotation to move a kinematic rigidbody
        // in a way that is smooth and predictable for the physics engine.
        rb.MovePosition(targetToFollow.position);
        rb.MoveRotation(targetToFollow.rotation);
    }
}