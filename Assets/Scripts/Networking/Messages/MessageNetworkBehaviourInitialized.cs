using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    public struct MessageNetworkBehaviourInitialized
    {
        public int networkID;                                       // 4 bytes
        public int typeID;                                          // 4 bytes
                                                                    // 8 bytes

        public MessageNetworkBehaviourInitialized(int networkID, int typeID)
        {
            this.networkID = networkID;
            this.typeID = typeID;
        }
    }
}