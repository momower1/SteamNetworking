using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MastersOfTempest.Networking
{
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance = null;

        public float pingsPerSec = 1;
        [ReadOnly]
        [SerializeField]
        private bool initialized = false;
        [ReadOnly]
        [SerializeField]
        private float serverHz = 16;
        [ReadOnly]
        [SerializeField]
        private float ping = 0;
        private float lastPingTime = 0;
        [Space(10)]
        public UnityEvent onClientInitialized;

        // Stores all the server object prefabs based on their resource id
        private Dictionary<int, GameObject> serverObjectPrefabs = new Dictionary<int, GameObject>();

        // Use the gameobject instance id from the server to keep track of the objects
        private Dictionary<int, ServerObject> objectsFromServer = new Dictionary<int, ServerObject>();

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

            // Load all the prefabs for server objects
            ServerObject[] serverObjectResources = Resources.LoadAll<ServerObject>("ServerObjects/");

            foreach (ServerObject s in serverObjectResources)
            {
                if (s.resourceID > 0 && !serverObjectPrefabs.ContainsKey(s.resourceID))
                {
                    serverObjectPrefabs[s.resourceID] = s.gameObject;
                }
                else
                {
                    Debug.LogError("Server object " + s.name + " does not have a valid resource id!");
                }
            }
        }

        void Start()
        {
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.ServerObject] += OnMessageServerObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.ServerObjectList] += OnMessageServerObjectList;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.DestroyServerObject] += OnMessageDestroyServerObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.PingPong] += OnMessagePingPong;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviour] += OnMessageNetworkBehaviour;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] += OnMessageNetworkBehaviourInitialized;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.Initialization] += OnMessageInitialization;

            StartCoroutine(SendInitializationMessage());
        }

        void Update()
        {
            if (Time.time - lastPingTime > (1.0f / pingsPerSec))
            {
                byte[] data = System.BitConverter.GetBytes(Time.time);
                NetworkManager.Instance.SendToServer(data, NetworkMessageType.PingPong, Facepunch.Steamworks.Networking.SendType.Unreliable);
                lastPingTime = Time.time;
            }
        }

        IEnumerator SendInitializationMessage ()
        {
            while (!initialized)
            {
                // Send a message to initialize the server
                byte[] data = System.Text.Encoding.UTF8.GetBytes("Initialization");
                NetworkManager.Instance.SendToServer(data, NetworkMessageType.Initialization, Facepunch.Steamworks.Networking.SendType.Reliable);

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

        void OnMessageServerObject(byte[] data, ulong steamID)
        {
            UpdateServerObject(MessageServerObject.FromBytes(data, 0));
        }

        void UpdateServerObject(MessageServerObject messageServerObject)
        {
            // Create a new object if it doesn't exist yet
            if (!objectsFromServer.ContainsKey(messageServerObject.instanceID))
            {
                // Make sure that the parent exists already if it has one
                if (!messageServerObject.hasParent || objectsFromServer.ContainsKey(messageServerObject.parentInstanceID))
                {
                    // Check if the object is a prefab or a child of a prefab (resourceID < 0)
                    if (messageServerObject.resourceID > 0)
                    {
                        // Switch active scene so that instantiate creates the object as part of the client scene
                        Scene previouslyActiveScene = SceneManager.GetActiveScene();
                        SceneManager.SetActiveScene(gameObject.scene);

                        // Instantiate the object
                        ServerObject tmp = Instantiate(serverObjectPrefabs[messageServerObject.resourceID]).GetComponent<ServerObject>();
                        objectsFromServer[messageServerObject.instanceID] = tmp;

                        // Switch back to the previously active scene
                        SceneManager.SetActiveScene(previouslyActiveScene);

                        // Set attributes, also update transform after spawn
                        tmp.onServer = false;
                        tmp.serverID = messageServerObject.instanceID;
                        tmp.transform.localPosition = messageServerObject.localPosition;
                        tmp.transform.localRotation = messageServerObject.localRotation;
                        tmp.transform.localScale = messageServerObject.localScale;
                    }
                    else if (messageServerObject.resourceID < 0)
                    {
                        // Then only the transform should be synchronized and no objects should be spawned
                        // The root resource has an array of all children where the index is the childId
                        // Client children find themselves with root.children[-resourceID - 1]
                        // Find root, then child and assign to objectsFromServer
                        ServerObject root = objectsFromServer[messageServerObject.rootInstanceID];
                        ServerObject child = root.children[-messageServerObject.resourceID - 1];
                        child.serverID = messageServerObject.instanceID;
                        child.onServer = false;

                        // Add to the dictionairy to make sure that this object is updated with messages
                        objectsFromServer.Add(child.serverID, child);
                    }
                }
            }

            if (objectsFromServer.ContainsKey(messageServerObject.instanceID))
            {
                ServerObject serverObject = objectsFromServer[messageServerObject.instanceID];

                if (serverObject.lastUpdate <= messageServerObject.time)
                {
                    // Update values only if the UDP packet values are newer
                    serverObject.name = "[" + messageServerObject.instanceID + "]\t" + messageServerObject.name;
                    serverObject.lastUpdate = messageServerObject.time;

                    // Update the transform
                    serverObject.UpdateTransformFromMessageServerObject(messageServerObject);

                    // Update parent if possible
                    if (messageServerObject.hasParent)
                    {
                        if (objectsFromServer.ContainsKey(messageServerObject.parentInstanceID))
                        {
                            serverObject.transform.SetParent(objectsFromServer[messageServerObject.parentInstanceID].transform, false);
                        }
                    }
                    else
                    {
                        serverObject.transform.SetParent(null);
                    }
                }
            }
            else
            {
                // This can e.g. happen when loading a different scene and some messages from the previous scene arrive
                Debug.LogWarning(nameof(GameClient) + " does not have a " + nameof(ServerObject) + " with the instance id " + messageServerObject.instanceID + "!");
            }
        }

        void OnMessageServerObjectList (byte[] data, ulong steamID)
        {
            MessageServerObjectList messageServerObjectList = MessageServerObjectList.FromBytes(data, 0);

            foreach (byte[] b in messageServerObjectList.messages)
            {
                UpdateServerObject(MessageServerObject.FromBytes(b, 0));
            }
        }

        void OnMessageDestroyServerObject(byte[] data, ulong steamID)
        {
            int serverIDToDestroy = System.BitConverter.ToInt32(data, 0);

            if (objectsFromServer.ContainsKey(serverIDToDestroy))
            {
                Destroy(objectsFromServer[serverIDToDestroy].gameObject);
                objectsFromServer.Remove(serverIDToDestroy);
            }
        }

        void OnMessagePingPong(byte[] data, ulong steamID)
        {
            ping = Time.time - System.BitConverter.ToSingle(data, 0);
            serverHz = System.BitConverter.ToSingle(data, 4);
        }

        void OnMessageNetworkBehaviour(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviour message = MessageNetworkBehaviour.FromBytes(data, 0);
            objectsFromServer[message.serverID].HandleNetworkBehaviourMessage(message.index, message.data, steamID);
        }

        void OnMessageNetworkBehaviourInitialized(byte[] data, ulong steamID)
        {
            MessageNetworkBehaviourInitialized message = ByteSerializer.FromBytes<MessageNetworkBehaviourInitialized>(data);
            objectsFromServer[message.serverID].HandleNetworkBehaviourInitializedMessage(message.typeID, steamID);
        }

        public ServerObject GetObjectFromServer(int serverID)
        {
            return objectsFromServer[serverID];
        }

        public bool IsInitialized ()
        {
            return initialized;
        }

        public float GetServerHz ()
        {
            return serverHz;
        }

        public float GetPing ()
        {
            return ping;
        }

        void OnDestroy()
        {
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.ServerObject] -= OnMessageServerObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.ServerObjectList] -= OnMessageServerObjectList;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.DestroyServerObject] -= OnMessageDestroyServerObject;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.PingPong] -= OnMessagePingPong;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviour] -= OnMessageNetworkBehaviour;
            NetworkManager.Instance.clientMessageEvents[NetworkMessageType.NetworkBehaviourInitialized] -= OnMessageNetworkBehaviourInitialized;
        }
    }
}
