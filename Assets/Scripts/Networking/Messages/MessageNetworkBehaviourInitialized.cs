using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    public struct MessageNetworkBehaviourInitialized
    {
        public int serverID;                                        // 4 bytes
        public int typeID;                                          // 4 bytes
                                                                    // 8 bytes

        public MessageNetworkBehaviourInitialized(int serverID, int typeID)
        {
            this.serverID = serverID;
            this.typeID = typeID;
        }
    }
}