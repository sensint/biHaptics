using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightToggle : MonoBehaviour
{
    public Light[] lightsToToggle; // To assign in the inspector
    private bool lightsOn = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            lightsOn = !lightsOn;
            foreach (Light l in lightsToToggle)
            {
                if (l != null) l.enabled = lightsOn;
            }
        }
    }
}
