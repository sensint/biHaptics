using UnityEngine;
using Obi;

public class PoolNoodleGrab2 : MonoBehaviour
{
    [Header("Obi")]
    public ObiRod rod;
    public ObiParticleAttachment leftAttachment;    // first rod particle
    public ObiParticleAttachment rightAttachment;  // last rod particle

    [Header("Anchors")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        startPos = rod.transform.position;
        startRot = rod.transform.rotation;

        // Ensure attachments are disabled at start to prevent auto-grabbing
        if (leftAttachment != null) leftAttachment.enabled = false;
        if (rightAttachment != null) rightAttachment.enabled = false;
    }

    void Update()
    {
        // Left grip toggles left end
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
            ToggleAttachment(leftAttachment, leftHandAnchor, 0);

        // Right grip toggles right end
        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
            ToggleAttachment(rightAttachment, rightHandAnchor, rod.activeParticleCount - 1);

        // Reset noodle on "B" (controller B or keyboard)
        if (OVRInput.GetDown(OVRInput.Button.Two) || Input.GetKeyDown(KeyCode.B))
            ResetRod();
    }

    private void ToggleAttachment(ObiParticleAttachment attachment, Transform anchor, int particleIndex)
    {
        // Added a null check for the rod's solver to prevent errors.
        if (attachment == null || anchor == null || rod.solver == null) return;

        if (attachment.enabled)
        {
            attachment.enabled = false;
            attachment.target = null;
        }
        else
        {
            // FIX: Convert the anchor's world position to the solver's local space.
            // This corrects any offsets if the solver is not at the world origin.
            Vector3 localPos = rod.solver.transform.InverseTransformPoint(anchor.position);
            rod.solver.positions[rod.solverIndices[particleIndex]] = localPos;

            attachment.target = anchor;
            attachment.enabled = true;
        }
    }

    private void ResetRod()
    {
        if (leftAttachment != null) { leftAttachment.enabled = false; leftAttachment.target = null; }
        if (rightAttachment != null) { rightAttachment.enabled = false; rightAttachment.target = null; }

        rod.transform.position = startPos;
        rod.transform.rotation = startRot;
        rod.ResetParticles();
    }
}

