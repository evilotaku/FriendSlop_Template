using Unity.Mathematics;
using UnityEngine;

public enum ShapeType
{
    Box,
    Sphere,
    Capsule
}

[System.Serializable]
public struct ShapeConfig
{
    public ShapeType ShapeType;

    [Header("Dimensions")]
    public float3 BoxHalfExtents; // Used for Box
    public float SphereRadius;     // Used for Sphere
    public float CapsuleRadius;    // Used for Capsule
    public float CapsuleTop;       // Used for Capsule
    public float CapsuleBottom;    // Used for Capsule

    [Header("Material Settings")]
    public float Density;
    public float Friction;
    public float Restitution;

    public static ShapeConfig Default => new ShapeConfig
    {
        ShapeType = ShapeType.Box,
        BoxHalfExtents = new float3(0.5f, 0.5f, 0.5f),
        SphereRadius = 0.5f,
        CapsuleRadius = 0.4f,
        CapsuleTop = 2.0f,
        CapsuleBottom = -2.0f,
        Density = 1.0f,
        Friction = 0.6f,
        Restitution = 0.0f
    };
}
