using UnityEngine;

using Obi;



public class RubberBandGrab : MonoBehaviour

{

    public ObiRope rope;

    public ObiParticleAttachment leftAttachment;

    public ObiParticleAttachment rightAttachment;



    public Transform leftHandAnchor;

    public Transform rightHandAnchor;



    public float detachStretchThreshold = 1.2f; // max distance before snapping



    private bool leftAttached = false;

    private bool rightAttached = false;



    // For detecting trigger clicks (toggle instead of hold)

    private bool lastLeftTriggerState = false;

    private bool lastRightTriggerState = false;



    private Vector3 ropeStartPosition;

    private Quaternion ropeStartRotation;



    void Start()

    {

        // Save initial rope transform

        ropeStartPosition = rope.transform.position;

        ropeStartRotation = rope.transform.rotation;

    }



    void Update()

    {

        // --- Left grip toggle ---

        bool leftGripPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) > 0.8f;

        if (leftGripPressed && !lastLeftTriggerState) // on press down

        {

            if (leftAttached) DetachLeft();

            else AttachLeft();

        }

        lastLeftTriggerState = leftGripPressed;



        // --- Right grip toggle ---

        bool rightGripPressed = OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) > 0.8f;

        if (rightGripPressed && !lastRightTriggerState)

        {

            if (rightAttached) DetachRight();

            else AttachRight();

        }

        lastRightTriggerState = rightGripPressed;



        // --- Overstretch check ---

        if (leftAttached && rightAttached)

        {

            float dist = Vector3.Distance(leftHandAnchor.position, rightHandAnchor.position);

            if (dist > detachStretchThreshold)

            {

                DetachLeft();

                DetachRight();

            }

        }



        // --- Reset on R key ---

        if (Input.GetKeyDown(KeyCode.R))

        {

            ResetRope();

        }

    }



    void AttachLeft()

    {

        leftAttachment.target = leftHandAnchor;

        leftAttachment.enabled = true;

        leftAttached = true;

    }



    void DetachLeft()

    {

        leftAttachment.enabled = false;

        leftAttached = false;

    }



    void AttachRight()

    {

        rightAttachment.target = rightHandAnchor;

        rightAttachment.enabled = true;

        rightAttached = true;

    }



    void DetachRight()

    {

        rightAttachment.enabled = false;

        rightAttached = false;

    }



    void ResetRope()

    {

        // Detach both ends

        DetachLeft();

        DetachRight();



        // Reset transform

        rope.transform.position = ropeStartPosition;

        rope.transform.rotation = ropeStartRotation;



        // Reset rope particles to initial state

        rope.ResetParticles();

    }

}