namespace MastersOfTempest.Networking
{
    public enum NetworkMessageType
    {
        PingPong,
        StartGame,
        Initialization,
        ServerObject,
        ServerObjectList,
        DestroyServerObject,
        NetworkBehaviour,
        NetworkBehaviourInitialized
    };
}
