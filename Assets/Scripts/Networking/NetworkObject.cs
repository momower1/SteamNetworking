using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking.Messages;

namespace SteamNetworking
{
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour
    {
        [ReadOnly]
        public int resourceID = -1;
        [ReadOnly]
        public bool onServer = true;
        [ReadOnly]
        public int networkID = 0;
        [ReadOnly]
        public float lastUpdate = 0;
        
        // Used when the network object is a child in a network object resource (resourceID < 0), set by editor script and saved in the prefab
        [SerializeField, HideInInspector]
        public NetworkObject root = null;
        [SerializeField, HideInInspector]
        public NetworkObject[] children = null;

        [Header("Server Parameters")]
        public LayerMask serverLayer = 1 << 9;

        [Header("Client Parameters")]
        public LayerMask clientLayer = 1 << 10;
        public bool interpolateOnClient = true;
        public bool removeChildColliders = true;
        public bool removeChildRigidbodies = true;

        // Interpolation variables
        private LinkedList<MessageNetworkObject> interpolationMessages = new LinkedList<MessageNetworkObject>();

        // Handles all the incoming network behaviour messages from the network behaviours
        private Dictionary<int, Action<byte[], ulong>> networkBehaviourEvents = new Dictionary<int, Action<byte[], ulong>>();
        private Dictionary<int, Action<ulong>> networkBehaviourInitializedEvents = new Dictionary<int, Action<ulong>>();

        private Vector3 lastLocalPosition;
        private Quaternion lastLocalRotation;
        private Vector3 lastLocalScale;

        void Start()
        {
            if (onServer)
            {
                // Check if the resource id is valid
                if (resourceID == 0)
                {
                    Debug.LogError("Resource id is not valid for " + nameof(NetworkObject) + gameObject.name);
                }

                // Set network ID
                networkID = transform.GetInstanceID();

                // Set layer, also for children
                SetLayerOfThisGameObjectAndAllChildren(serverLayer);

                // Register to game server
                GameServer.Instance.RegisterAndSendMessageNetworkObject(this);
            }
            else
            {
                // Apply layer and physics changes
                SetLayerOfThisGameObjectAndAllChildren(clientLayer);
                RemoveCollidersAndRigidbodies();
            }
        }

        void Update()
        {
            if (!onServer && interpolateOnClient)
            {
                // Find the message that is before and the message after the interpolation time
                // Use half the server tick rate as a buffer because some messages might not arrive on time
                float interpolationTime = GameClient.Instance.GetCurrentServerTime() - (1.5f / GameClient.Instance.GetServerHz());
                LinkedListNode<MessageNetworkObject> interpolationEnd = interpolationMessages.First;

                // Search for the message that is after the interpolation time
                while (interpolationEnd != null && interpolationEnd.Value.time <= interpolationTime)
                {
                    interpolationEnd = interpolationEnd.Next;
                }

                if (interpolationEnd != null)
                {
                    // Found message after the interpolation time, the message before that must be the start
                    LinkedListNode<MessageNetworkObject> interpolationStart = interpolationEnd.Previous;

                    // Only interpolate if there are two follow up messages
                    if (interpolationStart != null)
                    {
                        // Found message before the interpolation time, remove all the no longer needed previous entries from the list
                        while (!interpolationMessages.First.Equals(interpolationStart))
                        {
                            interpolationMessages.RemoveFirst();
                        }

                        // Improves the interpolation when the actual time between messages is way larger than the server hz
                        // This happens when an object moves after it didn't move for some time and therefore also didn't send messages
                        // In that case correct the time of the last message to the time where it should have arrived based on the server hz (pessimistic)
                        interpolationStart.Value.time = Mathf.Max(interpolationStart.Value.time, interpolationEnd.Value.time - (1.0f / GameClient.Instance.GetServerHz()));

                        // Interpolate between both messages
                        float interpolationFactor = (interpolationTime - interpolationStart.Value.time) / (interpolationEnd.Value.time - interpolationStart.Value.time);

                        transform.localPosition = Vector3.Lerp(interpolationStart.Value.localPosition, interpolationEnd.Value.localPosition, interpolationFactor);
                        transform.localRotation = Quaternion.Lerp(interpolationStart.Value.localRotation, interpolationEnd.Value.localRotation, interpolationFactor);
                        transform.localScale = Vector3.Lerp(interpolationStart.Value.localScale, interpolationEnd.Value.localScale, interpolationFactor);
                    }
                    else
                    {
                        // There is no previous message, just take the data from the end without interpolating
                        transform.localPosition = interpolationEnd.Value.localPosition;
                        transform.localRotation = interpolationEnd.Value.localRotation;
                        transform.localScale = interpolationEnd.Value.localScale;
                    }
                }
            }
        }

        private void RemoveCollidersAndRigidbodies()
        {
            if (removeChildColliders)
            {
                Collider[] colliders = GetComponentsInChildren<Collider>();

                foreach (Collider c in colliders)
                {
                    Destroy(c);
                }
            }

            if (removeChildRigidbodies)
            {
                Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>();

                foreach (Rigidbody r in rigidbodies)
                {
                    Destroy(r);
                }
            }
        }

        public bool HasChanged ()
        {
            bool changed = (transform.localPosition != lastLocalPosition) | (transform.localRotation != lastLocalRotation) | (transform.localScale != lastLocalScale);

            // Save new values
            lastLocalPosition = transform.localPosition;
            lastLocalRotation = transform.localRotation;
            lastLocalScale = transform.localScale;

            return changed;
        }

        public void UpdateTransformFromMessageNetworkObject(MessageNetworkObject messageNetworkObject)
        {
            if (!onServer)
            {
                if (interpolateOnClient)
                {
                    interpolationMessages.AddLast(messageNetworkObject);
                }
                else
                {
                    // Directly update the transform
                    transform.localPosition = messageNetworkObject.localPosition;
                    transform.localRotation = messageNetworkObject.localRotation;
                    transform.localScale = messageNetworkObject.localScale;
                }
            }
        }

        public void HandleNetworkBehaviourInitializedMessage (int index, ulong steamID)
        {
            networkBehaviourInitializedEvents[index].Invoke(steamID);
        }

        public void HandleNetworkBehaviourMessage(int index, byte[] data, ulong steamId)
        {
            networkBehaviourEvents[index].Invoke(data, steamId);
        }

        public void AddNetworkBehaviourEvents(int index, Action<byte[], ulong> behaviourAction, Action<ulong> initializedAction)
        {
            networkBehaviourEvents[index] = behaviourAction;
            networkBehaviourInitializedEvents[index] = initializedAction;
        }

        public void RemoveNetworkBehaviourEvents(int index)
        {
            networkBehaviourEvents.Remove(index);
            networkBehaviourInitializedEvents.Remove(index);
        }

        public void SetLayerOfThisGameObjectAndAllChildren (LayerMask layerMask)
        {
            int layerToSet = -1;

            for (int i = 0; i < 32; i++)
            {
                if (layerMask.value == (1 << i))
                {
                    layerToSet = i;
                }
            }

            if (layerToSet == -1)
            {
                Debug.LogError(nameof(NetworkObject) + " " + gameObject.name + " cannot have none or multiple layers set in its layer mask!");
                return;
            }

            // Also contains the own transform
            Transform[] thisAndAllChildren = GetComponentsInChildren<Transform>(true);

            foreach (Transform t in thisAndAllChildren)
            {
                t.gameObject.layer = layerToSet;
            }
        }

        void OnDestroy()
        {
            if (onServer)
            {
                // Send destroy message
                GameServer.Instance.RemoveNetworkObject(this);
                GameServer.Instance.SendMessageDestroyNetworkObject(this);
            }
        }
    }
}
