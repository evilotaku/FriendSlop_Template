using System;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class PhysicsStatePayload : INetworkSerializable, IEquatable<PhysicsStatePayload>
{
    public Vector3 Position;
    public Quaternion Rotation;
       

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
    }

    public bool Equals(PhysicsStatePayload other)
    {
        if (other == null) return false;
        return Position == other.Position && Rotation == other.Rotation;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as PhysicsStatePayload);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Rotation);
    }
}
