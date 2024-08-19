using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Primitive : MonoBehaviour
{

    public enum PrimitiveType { Sphere, Cube, Torus};
    public enum Operation { None, Blend, Cut, Mask }

    public PrimitiveType primitiveType;
    public Operation operation;
    public Color colour = Color.white;
    [Range(0, 1)]
    public float blendStrength;
    [HideInInspector]
    public int numChildren;

    public Vector3 Position
    {
        get
        {
            return transform.position;
        }
    }

    public Vector3 Scale
    {
        get
        {
            Vector3 parentScale = Vector3.one;
            if (transform.parent != null && transform.parent.GetComponent<Primitive>() != null)
            {
                parentScale = transform.parent.GetComponent<Primitive>().Scale;
            }
            return Vector3.Scale(transform.localScale, parentScale);
        }
    }

    public Vector3 Rotation
    {
        get
        {
            return transform.rotation.eulerAngles;
        }
    }
}
