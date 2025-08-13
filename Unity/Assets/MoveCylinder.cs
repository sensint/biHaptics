using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class MoveCylinder : MonoBehaviour
{
    public Transform RHandTransform;
    public Transform LHandTransform;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = (RHandTransform.position + LHandTransform.position)/2;
        
    }
}
