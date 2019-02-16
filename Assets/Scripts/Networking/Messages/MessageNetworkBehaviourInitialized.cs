using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamNetworking.Messages
{
    public struct MessageNetworkBehaviourInitialized
    {
        public int networkID;                                       // 4 bytes
        public int index;                                           // 4 bytes
                                                                    // 8 bytes

        public MessageNetworkBehaviourInitialized(int networkID, int index)
        {
            this.networkID = networkID;
            this.index = index;
        }
    }
}