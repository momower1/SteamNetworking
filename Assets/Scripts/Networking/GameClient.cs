using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using SteamNetworking.Messages;

namespace SteamNetworking
{
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance = null;

        public float pingsPerSec = 16;
        [SerializeField, ReadOnly]
        private bool initialized = false;
        [SerializeField, ReadOnly]
        private float ping = 0;
        private float lastPingTime = 0;
        [SerializeField, ReadOnly]
        private float serverHz = 16;
        [SerializeField, ReadOnly, Tooltip("Always ahead of the " + nameof(currentServerTime))]
        private float currentClientTime = 0;
        [SerializeField, ReadOnly, Tooltip("Always after the " + nameof(currentClientTime))]
        private float currentServerTime = 0;
        [Space(10)]
        public UnityEvent onClientInitialized;

        // Stores all the network object prefabs based on their resource id
        private Dictionary<int, GameObject> networkObjectPrefabs = new Dictionary<int, GameObject>();

        // Use the gameobject instance id from the server to keep track of the objects
        private Dictionary<int, NetworkObject> objectsFromServer = new Dictionary<int, NetworkObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogError(nameof(GameClient) + " cannot have multiple instances!");
                Destroy(gameObject);
            }

            // Load all the prefabs for network objects
            NetworkObject[] networkObjectResources = Resources.LoadAll<NetworkObject>("NetworkObjects/");

            foreach (NetworkObject s in networkObjectResources)
            {
                if (s.resourceID > 0 && !networkObjectPrefabs.ContainsKey(s.resourceID))
                {
                    networkObjectPrefabs[s.resourceID] = s.gameObject;
                }
                else
                {
                    Debug.LogError(nameof(NetworkObject) + s.name + " does not have a valid resource id!");
                }
            }
        }

        void Start()
        {
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkObject] += OnMessageNetworkObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkObjectList] += OnMessageNetworkObjectList;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.DestroyNetworkObject] += OnMessageDestroyNetworkObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.PingPong] += OnMessagePingPong;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviour] += OnMessageNetworkBehaviour;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] += OnMessageNetworkBehaviourInitialized;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.Initialization] += OnMessageInitialization;

            StartCoroutine(SendInitializationMessage());
        }

        void Update()
        {
            if (Time.unscaledTime - lastPingTime > (1.0f / pingsPerSec))
            {
                byte[] data = System.BitConverter.GetBytes(Time.unscaledTime);
                NetworkManager.Instance.SendToServer(data, NetworkMessageType.PingPong, SendType.Unreliable);
                lastPingTime = Time.unscaledTime;
            }

            currentClientTime += Time.unscaledDeltaTime;
            currentServerTime += Time.unscaledDeltaTime;
        }

        IEnumerator SendInitializationMessage ()
        {
            while (!initialized)
            {
                // Send a message to initialize the server
                byte[] data = System.Text.Encoding.UTF8.GetBytes("Initialization");
                NetworkManager.Instance.SendToServer(data, NetworkMessageType.Initialization, SendType.Reliable);

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        void OnMessageInitialization(byte[] data, ulong steamID)
        {
            if (!initialized)
            {
                initialized = true;
                onClientInitialized.Invoke();
                NetworkManager.Instance.clientMessageEvents[NetworkMessageType.Initialization] -= OnMessageInitialization;
            }
        }

        void OnMessageNetworkObject(byte[] data, ulong steamID)
        {
            UpdateNetworkObject(MessageNetworkObject.FromBytes(data, 0));
        }

        void UpdateNetworkObject(MessageNetworkObject messageNetworkObject)
        {
            // Create a new object if it doesn't exist yet
            if (!objectsFromServer.ContainsKey(messageNetworkObject.instanceID))
            {
                // Make sure that the parent exists already if it has one
                if (!messageNetworkObject.hasParent || objectsFromServer.ContainsKey(messageNetworkObject.parentInstanceID))
                {
                    // Check if the object is a prefab or a child of a prefab (resourceID < 0)
                    if (messageNetworkObject.resourceID > 0)
                    {
                        // Switch active scene so that instantiate creates the object as part of the client scene
                        Scene previouslyActiveScene = SceneManager.GetActiveScene();
                        SceneManager.SetActiveScene(gameObject.scene);

                        // Instantiate the object
                        NetworkObject tmp = Instantiate(networkObjectPrefabs[messageNetworkObject.resourceID]).GetComponent<NetworkObject>();
                        objectsFromServer[messageNetworkObject.instanceID] = tmp;

                        // Switch back to the previously active scene
                        SceneManager.SetActiveScene(previouslyActiveScene);

                        // Set attributes, also update transform after spawn
                        tmp.onServer = false;
                        tmp.networkID = messageNetworkObject.instanceID;
                        tmp.transform.localPosition = messageNetworkObject.localPosition;
                        tmp.transform.localRotation = messageNetworkObject.localRotation;
                        tmp.transform.localScale = messageNetworkObject.localScale;
                    }
                    else if (messageNetworkObject.resourceID < 0)
                    {
                        // Then only the transform should be synchronized and no objects should be spawned
                        // The root resource has an array of all children where the index is the childId
                        // Client children find themselves with root.children[-resourceID - 1]
                        // Find root, then child and assign to objectsFromServer
                        NetworkObject root = objectsFromServer[messageNetworkObject.rootInstanceID];
                        NetworkObject child = root.children[-messageNetworkObject.resourceID - 1];
                        child.networkID = messageNetworkObject.instanceID;
                        child.onServer = false;

                        // Add to the dictionairy to make sure that this object is updated with messages
                        objectsFromServer.Add(child.networkID, child);
                    }
                }
            }

            if (objectsFromServer.ContainsKey(messageNetworkObject.instanceID))
            {
                NetworkObject networkObject = objectsFromServer[messageNetworkObject.instanceID];

                if (networkObject.lastUpdate <= messageNetworkObject.time)
                {
                    // Update values only if the UDP packet values are newer
                    networkObject.name = "[" + messageNetworkObject.instanceID + "]\t" + messageNetworkObject.name;
                    networkObject.lastUpdate = messageNetworkObject.time;

                    // Update the transform
                    networkObject.UpdateTransformFromMessageNetworkObject(messageNetworkObject);

                    // Update parent if possible
                    if (messageNetworkObject.hasParent)
                    {
                        if (objectsFromServer.ContainsKey(messageNetworkObject.parentInstanceID))
                        {
                            networkObject.transform.SetParent(objectsFromServer[messageNetworkObject.parentInstanceID].transform, false);
                        }
                    }
                    else
                    {
                        networkObject.transform.SetParent(null);
                    }
                }
            }
            else
            {
                // This can e.g. happen when loading a different scene and some messages from the previous scene arrive
                Debug.LogWarning(nameof(GameClient) + " does not have a " + nameof(NetworkObject) + " with the instance id " + messageNetworkObject.instanceID + "!");
            }
        }

        void OnMessageNetworkObjectList (byte[] data, ulong steamID)
        {
            MessageNetworkObjectList messageNetworkObjectList = MessageNetworkObjectList.FromBytes(data, 0);

            foreach (byte[] b in messageNetworkObjectList.messages)
            {
                UpdateNetworkObject(MessageNetworkObject.FromBytes(b, 0));
            }
        }

        void OnMessageDestroyNetworkObject(byte[] data, ulong steamID)
        {
            int networkIDToDestroy = System.BitConverter.ToInt32(data, 0);

            if (objectsFromServer.TryGetValue(networkIDToDestroy, out NetworkObject networkObjectToDestroy))
            {
                Destroy(networkObjectToDestroy.gameObject);
                objectsFromServer.Remove(networkIDToDestroy);
            }
        }

        void OnMessagePingPong(byte[] data, ulong steamID)
        {
            // Update ping, server hz, client and server time
            ping = Time.unscaledTime - System.BitConverter.ToSingle(data, 0);
            serverHz = System.BitConverter.ToSingle(data, 4);
            currentServerTime = System.BitConverter.ToSingle(data, 8) + ping / 2;
            currentClientTime = currentServerTime + ping / 2;
        }

        void OnMessageNetworkBehaviour(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviour message = MessageNetworkBehaviour.FromBytes(data, 0);

            if (objectsFromServer.TryGetValue(message.networkID, out NetworkObject networkObject))
            {
                networkObject.HandleNetworkBehaviourMessage(message.index, message.data, steamID);
            }
            else
            {
                Debug.LogWarning(nameof(GameClient) + " does not have " + nameof(NetworkBehaviour) + " " + message.networkID + "[" + message.index + "] and therefore its message cannot be handled!");
            }
        }

        void OnMessageNetworkBehaviourInitialized(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviourInitialized message = ByteSerializer.FromBytes<MessageNetworkBehaviourInitialized>(data);

            if (objectsFromServer.TryGetValue(message.networkID, out NetworkObject networkObject))
            {
                networkObject.HandleNetworkBehaviourInitializedMessage(message.index, steamID);
            }
            else
            {
                Debug.LogError(nameof(GameClient) + " does not have " + nameof(NetworkBehaviour) + " " + message.networkID + "[" + message.index + "] and therefore cannot be initialized!");
            }
        }

        public NetworkObject GetObjectFromServer(int networkID)
        {
            if (objectsFromServer.TryGetValue(networkID, out NetworkObject networkObject))
            {
                return networkObject;
            }

            Debug.LogWarning(nameof(GetObjectFromServer) + " returns null because " + nameof(GameClient) + " does not have " + nameof(NetworkObject) + " " + networkID);
            return null;
        }

        public bool IsInitialized ()
        {
            return initialized;
        }

        public float GetPing()
        {
            return ping;
        }

        public float GetServerHz ()
        {
            return serverHz;
        }

        public float GetCurrentClientTime ()
        {
            return currentClientTime;
        }

        public float GetCurrentServerTime ()
        {
            return currentServerTime;
        }

        void OnDestroy()
        {
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkObject] -= OnMessageNetworkObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkObjectList] -= OnMessageNetworkObjectList;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.DestroyNetworkObject] -= OnMessageDestroyNetworkObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.PingPong] -= OnMessagePingPong;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviour] -= OnMessageNetworkBehaviour;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] -= OnMessageNetworkBehaviourInitialized;
        }
    }
}
