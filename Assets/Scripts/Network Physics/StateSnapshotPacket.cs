using Unity.Netcode;

public struct StateSnapshotPacket : INetworkSerializable
{
    public int StateTick;
    public byte[] CompressedWorldData;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref StateTick);

        int length = CompressedWorldData?.Length ?? 0;
        serializer.SerializeValue(ref length);

        if (serializer.IsReader)
        {
            CompressedWorldData = new byte[length];
        }

        serializer.SerializeValue(ref CompressedWorldData);
    }
}
