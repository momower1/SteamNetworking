using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using SteamNetworking.Messages;

namespace SteamNetworking
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

        private Dictionary<int, NetworkObject> networkObjects = new Dictionary<int, NetworkObject>();
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
        /// Sends a list of NetworkObjects to all clients with the server tick rate (hz).
        /// </summary>
        /// <returns></returns>
        IEnumerator ServerUpdate()
        {
            Time.timeScale = 1;

            while (true)
            {
                yield return new WaitForSecondsRealtime(1.0f / hz);

                SendAllNetworkObjects(onlySendChanges, Facepunch.Steamworks.Networking.SendType.Unreliable);
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

        private void SendAllNetworkObjects (bool onlySendChangedTransforms, Facepunch.Steamworks.Networking.SendType sendType)
        {
            // Save all network object messages that need to be sended into one pool
            // Sort the pool by the depth of the transform in the hierarchy
            // This makes sure that parents are instantiated before their children
            List<KeyValuePair<int, NetworkObject>> networkObjectsToSend = new List<KeyValuePair<int, NetworkObject>>();

            foreach (NetworkObject networkObject in networkObjects.Values)
            {
                if (!onlySendChangedTransforms || networkObject.HasChanged())
                {
                    KeyValuePair<int, NetworkObject> networkObjectToAdd = new KeyValuePair<int, NetworkObject>(GetHierarchyDepthOfTransform(networkObject.transform, 0), networkObject);
                    networkObjectsToSend.Add(networkObjectToAdd);
                }
            }

            // Sort by the depth of the transform
            networkObjectsToSend.Sort
            (
                delegate (KeyValuePair<int, NetworkObject> a, KeyValuePair<int, NetworkObject> b)
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

            // Create and send network object list messages until the pool is empty
            while (networkObjectsToSend.Count > 0)
            {
                // Make sure that the message is small enough to fit into the UDP packet (1200 bytes)
                MessageNetworkObjectList messageNetworkObjectList = new MessageNetworkObjectList();

                while (true)
                {
                    if (networkObjectsToSend.Count > 0)
                    {
                        // Add next message
                        MessageNetworkObject message = new MessageNetworkObject(networkObjectsToSend[0].Value);
                        messageNetworkObjectList.messages.AddLast(message.ToBytes());

                        // Check if length is still small enough
                        if (messageNetworkObjectList.GetLength() <= maximumTransmissionLength)
                        {
                            // Small enough, keep message and remove from objects to send
                            networkObjectsToSend.RemoveAt(0);
                        }
                        else
                        {
                            // Too big, remove message and create a new list to send the rest
                            messageNetworkObjectList.messages.RemoveLast();
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Send the message to all clients
                NetworkManager.Instance.SendToAllClients(messageNetworkObjectList.ToBytes(), NetworkMessageType.NetworkObjectList, sendType);
            }
        }

        public Object InstantiateInScene(Object original, Vector3 position, Quaternion rotation, Transform parent)
        {
            // Switch scenes, instantiate object and then switch the scene back
            Scene previouslyActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(gameObject.scene);
            Object result = Instantiate(original, position, rotation, parent);
            SceneManager.SetActiveScene(previouslyActiveScene);
            return result;
        }

        public void RegisterAndSendMessageNetworkObject (NetworkObject networkObject)
        {
            if (allClientsInitialized)
            {
                // Make sure that objects are spawned on the server (with UDP it could happen that they don't spawn)
                MessageNetworkObject message = new MessageNetworkObject(networkObject);
                NetworkManager.Instance.SendToAllClients(message.ToBytes(), NetworkMessageType.NetworkObject, Facepunch.Steamworks.Networking.SendType.Reliable);
            }

            networkObjects.Add(networkObject.networkID, networkObject);
        }

        public NetworkObject GetNetworkObject (int networkID)
        {
            return networkObjects[networkID];
        }

        public void RemoveNetworkObject (NetworkObject networkObject)
        {
            networkObjects.Remove(networkObject.networkID);
        }

        public void SendMessageDestroyNetworkObject(NetworkObject networkObject)
        {
            byte[] data = System.BitConverter.GetBytes(networkObject.networkID);
            NetworkManager.Instance.SendToAllClients(data, NetworkMessageType.DestroyNetworkObject, Facepunch.Steamworks.Networking.SendType.Reliable);
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
                SendAllNetworkObjects(false, Facepunch.Steamworks.Networking.SendType.Reliable);
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
            networkObjects[message.networkID].HandleNetworkBehaviourMessage(message.index, message.data, steamID);
        }

        void OnMessageNetworkBehaviourInitialized(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviourInitialized message = ByteSerializer.FromBytes<MessageNetworkBehaviourInitialized>(data);
            networkObjects[message.networkID].HandleNetworkBehaviourInitializedMessage(message.typeID, steamID);
        }

        void OnDestroy()
        {
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviour] -= OnMessageNetworkBehaviour;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] -= OnMessageNetworkBehaviourInitialized;
            NetworkManager.Instance.serverMessageEvents[NetworkMessageType.PingPong] -= OnMessagePingPong;
        }
    }
}
