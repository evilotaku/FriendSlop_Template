using Unity.Netcode;
using System.IO;
using System.IO.Compression;

public struct StateSnapshotPacket : INetworkSerializable
{
    public int StateTick;
    public byte[] CompressedWorldData;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref StateTick);

        if (serializer.IsWriter)
        {
            // Compress before sending
            byte[] compressed = Compress(CompressedWorldData);
            int length = compressed?.Length ?? 0;
            serializer.SerializeValue(ref length);
            if (length > 0) serializer.SerializeValue(ref compressed);
        }
        else
        {
            // Decompress upon receipt
            int length = 0;
            serializer.SerializeValue(ref length);
            if (length > 0)
            {
                byte[] compressed = new byte[length];
                serializer.SerializeValue(ref compressed);
                CompressedWorldData = Decompress(compressed);
            }
        }
    }

    private byte[] Compress(byte[] data)
    {
        if (data == null) return null;
        using var output = new MemoryStream();
        using (var dstream = new DeflateStream(output, CompressionLevel.Optimal))
        {
            dstream.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private byte[] Decompress(byte[] data)
    {
        if (data == null) return null;
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
        {
            dstream.CopyTo(output);
        }
        return output.ToArray();
    }
}