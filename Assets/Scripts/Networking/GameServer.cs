using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MastersOfTempest.Networking
{
    public class GameServer : MonoBehaviour
    {
        public static GameServer Instance = null;

        [Header("Server Parameters")]
        public float hz = 16;
        [Tooltip("Will not send objects if they didn't change their transform. Enabling can cause teleportation for objects that start moving after being static.")]
        [SerializeField]
        private bool onlySendChanges = true;
        [Space(10)]
        public UnityEvent onServerInitialized;

        private Dictionary<int, ServerObject> serverObjects = new Dictionary<int, ServerObject>();
        private HashSet<ulong> clientsReadyForInitialization = new HashSet<ulong>();
        private bool allClientsInitialized = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogError("GameServer cannot have multiple instances!");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // Pause everything until all clients are initialized
            Time.timeScale = 0;

            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviour] += OnMessageNetworkBehaviour;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] += OnMessageNetworkBehaviourInitialized;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.Initialization] += OnMessageInitialization;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.PingPong] += OnMessagePingPong;
        }

        /// <summary>
        /// Sends a list of serverObjects to all clients with the server tick rate (hz).
        /// </summary>
        /// <returns></returns>
        IEnumerator ServerUpdate()
        {
            Time.timeScale = 1;

            while (true)
            {
                yield return new WaitForSecondsRealtime(1.0f / hz);

                SendAllServerObjects(onlySendChanges, Facepunch.Steamworks.Networking.SendType.Unreliable);
            }
        }

        private int GetHierarchyDepthOfTransform (Transform transform, int depth)
        {
            if (transform.parent != null)
            {
                return GetHierarchyDepthOfTransform(transform.parent, 1 + depth);
            }

            return depth;
        }

        private void SendAllServerObjects (bool onlySendChangedTransforms, Facepunch.Steamworks.Networking.SendType sendType)
        {
            // Save all server object messages that need to be sended into one pool
            // Sort the pool by the depth of the transform in the hierarchy
            // This makes sure that parents are instantiated before their children
            List<KeyValuePair<int, ServerObject>> serverObjectsToSend = new List<KeyValuePair<int, ServerObject>>();

            foreach (ServerObject serverObject in serverObjects.Values)
            {
                if (!onlySendChangedTransforms || serverObject.HasChanged())
                {
                    KeyValuePair<int, ServerObject> serverObjectToAdd = new KeyValuePair<int, ServerObject>(GetHierarchyDepthOfTransform(serverObject.transform, 0), serverObject);
                    serverObjectsToSend.Add(serverObjectToAdd);
                }
            }

            // Sort by the depth of the transform
            serverObjectsToSend.Sort
            (
                delegate (KeyValuePair<int, ServerObject> a, KeyValuePair<int, ServerObject> b)
                {
                    return a.Key - b.Key;
                }
            );

            // Standard is UDP packet size, 1200 bytes
            int maximumTransmissionLength = 1200;

            if (sendType == Facepunch.Steamworks.Networking.SendType.Reliable)
            {
                // TCP packet, up to 1MB
                maximumTransmissionLength = 1000000;
            }

            // Create and send server object list messages until the pool is empty
            while (serverObjectsToSend.Count > 0)
            {
                // Make sure that the message is small enough to fit into the UDP packet (1200 bytes)
                MessageServerObjectList messageServerObjectList = new MessageServerObjectList();

                while (true)
                {
                    if (serverObjectsToSend.Count > 0)
                    {
                        // Add next message
                        MessageServerObject message = new MessageServerObject(serverObjectsToSend[0].Value);
                        messageServerObjectList.messages.AddLast(message.ToBytes());

                        // Check if length is still small enough
                        if (messageServerObjectList.GetLength() <= maximumTransmissionLength)
                        {
                            // Small enough, keep message and remove from objects to send
                            serverObjectsToSend.RemoveAt(0);
                        }
                        else
                        {
                            // Too big, remove message and create a new list to send the rest
                            messageServerObjectList.messages.RemoveLast();
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Send the message to all clients
                NetworkManager.Instance.SendToAllClients(messageServerObjectList.ToBytes(), NetworkMessageType.ServerObjectList, sendType);
            }
        }

        public void RegisterAndSendMessageServerObject (ServerObject serverObject)
        {
            if (allClientsInitialized)
            {
                // Make sure that objects are spawned on the server (with UDP it could happen that they don't spawn)
                MessageServerObject message = new MessageServerObject(serverObject);
                NetworkManager.Instance.SendToAllClients(message.ToBytes(), NetworkMessageType.ServerObject, Facepunch.Steamworks.Networking.SendType.Reliable);
            }

            serverObjects.Add(serverObject.serverID, serverObject);
        }

        public ServerObject GetServerObject (int serverID)
        {
            return serverObjects[serverID];
        }

        public void RemoveServerObject (ServerObject serverObject)
        {
            serverObjects.Remove(serverObject.serverID);
        }

        public void SendMessageDestroyServerObject(ServerObject serverObject)
        {
            byte[] data = System.BitConverter.GetBytes(serverObject.serverID);
            NetworkManager.Instance.SendToAllClients(data, NetworkMessageType.DestroyServerObject, Facepunch.Steamworks.Networking.SendType.Reliable);
        }

        void OnMessageInitialization(byte[] data, ulong steamID)
        {
            // Only start the server loop if all the clients have loaded the scene and sent the message
            bool allClientsReady = true;
            clientsReadyForInitialization.Add(steamID);

            ulong[] lobbyMemberIDs = NetworkManager.Instance.GetLobbyMemberIDs();

            foreach (ulong id in lobbyMemberIDs)
            {
                if (!clientsReadyForInitialization.Contains(id))
                {
                    allClientsReady = false;
                    break;
                }
            }

            if (allClientsReady)
            {
                allClientsInitialized = true;

                // Make sure that all the objects on the server are spawned for all clients
                SendAllServerObjects(false, Facepunch.Steamworks.Networking.SendType.Reliable);
                NetworkManager.Instance.serverMessageEvents[NetworkMessageType.Initialization] -= OnMessageInitialization;

                // Answer to all the clients that the initialization finished
                // This works because the messages are reliable and in order (meaning all the objects on the client must have spawned when this message arrives)
                NetworkManager.Instance.SendToAllClients(data, NetworkMessageType.Initialization, Facepunch.Steamworks.Networking.SendType.Reliable);

                // Start the server loop and invoke all subscribed actions
                StartCoroutine(ServerUpdate());
                onServerInitialized.Invoke();
            }
        }

        void OnMessagePingPong(byte[] data, ulong steamID)
        {
            // Send the time back but also append the current server hz
            ArrayList tmp = new ArrayList(data);
            tmp.AddRange(System.BitConverter.GetBytes(hz));
            NetworkManager.Instance.SendToClient(steamID, (byte[])tmp.ToArray(typeof(byte)), NetworkMessageType.PingPong, Facepunch.Steamworks.Networking.SendType.Unreliable);
        }

        void OnMessageNetworkBehaviour(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviour message = MessageNetworkBehaviour.FromBytes(data, 0);
            serverObjects[message.serverID].HandleNetworkBehaviourMessage(message.index, message.data, steamID);
        }

        void OnMessageNetworkBehaviourInitialized(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviourInitialized message = ByteSerializer.FromBytes<MessageNetworkBehaviourInitialized>(data);
            serverObjects[message.serverID].HandleNetworkBehaviourInitializedMessage(message.typeID, steamID);
        }

        void OnDestroy()
        {
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviour] -= OnMessageNetworkBehaviour;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] -= OnMessageNetworkBehaviourInitialized;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.PingPong] -= OnMessagePingPong;
        }
    }
}
