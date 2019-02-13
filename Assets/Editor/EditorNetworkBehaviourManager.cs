using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SteamNetworking
{
    public class EditorNetworkBehaviourManager
    {
        [InitializeOnLoadMethod]
        static void SetNetworkBehaviourIndices()
        {
            NetworkObject[] networkObjects = Resources.LoadAll<NetworkObject>("NetworkObjects/");

            foreach (NetworkObject s in networkObjects)
            {
                NetworkObject[] children = s.GetComponentsInChildren<NetworkObject>();

                foreach (NetworkObject c in children)
                {
                    NetworkBehaviour[] networkBehaviours = c.GetComponents<NetworkBehaviour>();

                    for (int i = 0; i < networkBehaviours.Length; i++)
                    {
                        if (networkBehaviours[i].index != i)
                        {
                            networkBehaviours[i].index = i;
                            EditorUtility.SetDirty(s);
                        }
                    }
                }
            }
        }
    }
}
