using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public struct PlayerInputPayload : INetworkSerializeByMemcpy
{
    public ulong TargetEntityIdRaw; // Extracted via entityId.ToULong()
    public float2 MoveDirection;
    public bool JumpPressed;

    public EntityId TargetEntityId => EntityId.FromULong(TargetEntityIdRaw);
}
