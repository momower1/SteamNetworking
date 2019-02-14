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
        public string serverLayer = "Server";

        [Header("Client Parameters")]
        public string clientLayer = "Client";
        public bool interpolateOnClient = true;
        public bool removeChildColliders = true;
        public bool removeChildRigidbodies = true;

        // Interpolation variables
        private MessageNetworkObject currentMessage = null;
        private MessageNetworkObject lastMessage = null;
        private float timeSinceLastMessage = 0;
        private float timeExtrapolated = 0;
        private bool extrapolated = false;

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
                // Make sure that both messages exist
                if (currentMessage != null && lastMessage != null)
                {
                    // Interpolate between the transform from the last and the current message based on the time that passed since the last message
                    // This introduces a bit of latency but does not require any prediction
                    float dt = currentMessage.time - lastMessage.time;

                    if (dt > 0)
                    {
                        float interpolationFactor = timeSinceLastMessage / dt;

                        if (interpolationFactor <= 1)
                        {
                            // Interpolate
                            transform.localPosition = Vector3.Lerp(lastMessage.localPosition, currentMessage.localPosition, interpolationFactor);
                            transform.localRotation = Quaternion.Lerp(lastMessage.localRotation, currentMessage.localRotation, interpolationFactor);
                            transform.localScale = Vector3.Lerp(lastMessage.localScale, currentMessage.localScale, interpolationFactor);
                        }
                        else
                        {
                            if (timeExtrapolated < dt)
                            {
                                // Extrapolate, then shift the time for the interpolation based on the extrapolated time when the next message arrives
                                Vector3 velocity = (currentMessage.localPosition - lastMessage.localPosition) / dt;
                                transform.localPosition += Time.deltaTime * velocity;

                                timeExtrapolated += Time.deltaTime;
                                extrapolated = true;
                            }
                            else
                            {
                                // There is no message coming that can make use of the extrapolation, reset to actual position
                                transform.localPosition = Vector3.Lerp(transform.localPosition, currentMessage.localPosition, timeSinceLastMessage - dt - timeExtrapolated);
                                extrapolated = false;
                            }
                        }

                        timeSinceLastMessage += Time.deltaTime;
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
                    // Save data for interpolation
                    lastMessage = currentMessage;
                    currentMessage = messageNetworkObject;
                    timeSinceLastMessage = extrapolated ? timeExtrapolated : 0;
                    timeExtrapolated = 0;
                    extrapolated = false;

                    // Improves the interpolation when the actual time between messages is way larger than the server hz
                    // This happens when an object moves after it didn't move for some time and therefore also didn't send messages
                    // In that case correct the time of the last message to the time where it should have arrived based on the server hz (pessimistic)
                    if (lastMessage != null)
                    {
                        lastMessage.time = Mathf.Max(lastMessage.time, currentMessage.time - (1.0f / GameClient.Instance.GetServerHz()));
                    }
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

        public void SetLayerOfThisGameObjectAndAllChildren (string layer)
        {
            int layerToSet = LayerMask.NameToLayer(layer);

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
