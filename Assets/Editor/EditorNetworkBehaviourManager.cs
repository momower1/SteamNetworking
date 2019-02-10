using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    public class EditorNetworkBehaviourManager
    {
        [InitializeOnLoadMethod]
        static void SetNetworkBehaviourIndices()
        {
            ServerObject[] serverObjects = Resources.LoadAll<ServerObject>("ServerObjects/");

            foreach (ServerObject s in serverObjects)
            {
                ServerObject[] children = s.GetComponentsInChildren<ServerObject>();

                foreach (ServerObject c in children)
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
