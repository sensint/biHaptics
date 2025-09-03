using UnityEngine;
using Obi;

public class GlowstickTwist : MonoBehaviour
{
    [Header("Obi Setup")]
    public ObiRod rod;
    public ObiParticleAttachment leftAttachment;  // Attachment for the first particle (index 0)
    public ObiParticleAttachment rightAttachment; // Attachment for the last particle

    [Header("VR Anchors")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    [Header("Glowstick Properties")]
    public Renderer glowstickRenderer;
    public Color glowColor = Color.green;
    [Tooltip("The angle in degrees the hands must be twisted to crack the glowstick.")]
    public float twistActivationAngle = 90f;

    private bool isActivated = false;
    private Material originalMaterial;
    private Material glowingMaterialInstance;

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        if (glowstickRenderer != null)
        {
            // Store the original material to revert on reset
            originalMaterial = glowstickRenderer.sharedMaterial;
            // Create a new instance to modify at runtime
            glowingMaterialInstance = new Material(originalMaterial);
            glowstickRenderer.material = glowingMaterialInstance;
        }

        // Store initial transform for resetting
        startPos = rod.transform.position;
        startRot = rod.transform.rotation;

        // Ensure attachments are disabled at the start
        if (leftAttachment != null) leftAttachment.enabled = false;
        if (rightAttachment != null) rightAttachment.enabled = false;

        // Deactivate glow at start
        DeactivateGlow();
    }

    void Update()
    {
        // --- Handle Grabbing Input ---
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
            ToggleAttachment(leftAttachment, leftHandAnchor, 0);

        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
            ToggleAttachment(rightAttachment, rightHandAnchor, rod.activeParticleCount - 1);

        // --- Handle Twist Logic ---
        CheckForTwist();

        // --- Handle Reset Input ---
        if (OVRInput.GetDown(OVRInput.Button.Two) || Input.GetKeyDown(KeyCode.G))
            ResetGlowstick();
    }

    private void CheckForTwist()
    {
        // Can only twist if it's not already activated and both hands are grabbing
        if (isActivated || !leftAttachment.enabled || !rightAttachment.enabled)
            return;

        // Calculate the angle between the two hand rotations
        float angle = Quaternion.Angle(leftHandAnchor.rotation, rightHandAnchor.rotation);

        if (angle > twistActivationAngle)
        {
            ActivateGlow();
        }
    }

    private void ToggleAttachment(ObiParticleAttachment attachment, Transform anchor, int particleIndex)
    {
        if (attachment == null || anchor == null || rod.solver == null) return;

        if (attachment.enabled)
        {
            attachment.enabled = false;
            attachment.target = null;
        }
        else
        {
            // FIX: Convert the anchor's world position to the solver's local space.
            Vector3 localPos = rod.solver.transform.InverseTransformPoint(anchor.position);
            rod.solver.positions[rod.solverIndices[particleIndex]] = localPos;

            attachment.target = anchor;
            attachment.enabled = true;
        }
    }

    private void ActivateGlow()
    {
        isActivated = true;
        // Enable the emission property and set the color
        glowingMaterialInstance.EnableKeyword("_EMISSION");
        glowingMaterialInstance.SetColor("_EmissionColor", glowColor);

        // Optional: Play a "crack" sound effect here
        // AudioSource.PlayClipAtPoint(crackSound, transform.position);
    }

    private void DeactivateGlow()
    {
        isActivated = false;
        if (glowingMaterialInstance != null)
        {
            glowingMaterialInstance.DisableKeyword("_EMISSION");
        }
    }

    private void ResetGlowstick()
    {
        // Detach hands
        if (leftAttachment != null) leftAttachment.enabled = false;
        if (rightAttachment != null) rightAttachment.enabled = false;

        // Reset physical state
        rod.transform.position = startPos;
        rod.transform.rotation = startRot;
        rod.ResetParticles();

        // Reset visual state
        DeactivateGlow();
    }
}
