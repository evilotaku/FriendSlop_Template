using Unity.Netcode;

public struct InputPacket : INetworkSerializable
{
    public int TargetTick;
    // Transmit a small, redundant 3-frame buffer window to survive packet loss
    public PlayerInputPayload[] RedundantInputs;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TargetTick);

        int count = RedundantInputs?.Length ?? 0;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
        {
            RedundantInputs = new PlayerInputPayload[count];
        }

        for (int i = 0; i < count; i++)
        {
            serializer.SerializeValue(ref RedundantInputs[i]);
        }
    }
}
