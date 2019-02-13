namespace SteamNetworking
{
    public enum NetworkMessageType
    {
        PingPong,
        StartGame,
        Initialization,
        NetworkObject,
        NetworkObjectList,
        DestroyNetworkObject,
        NetworkBehaviour,
        NetworkBehaviourInitialized
    };
}
