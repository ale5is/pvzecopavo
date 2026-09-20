using Unity.Netcode;

public struct OnlineMessage : INetworkSerializable
{
    public byte Type1;
    public byte Type2;
    public string Content;

    public OnlineMessage(byte type1, byte type2, string content)
    {
        Type1 = type1;
        Type2 = type2;
        Content = content ?? string.Empty;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type1);
        serializer.SerializeValue(ref Type2);
        serializer.SerializeValue(ref Content);
    }
}