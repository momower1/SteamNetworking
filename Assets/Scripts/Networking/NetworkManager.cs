using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;
using SteamNetworking.GUI;

namespace SteamNetworking
{
    //  1. Initializes steam on startup
    //  2. Calls Update
    //  3. Handles all the networking messages
    //  4. Disposes and shuts down Steam on close

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance = null;

        // The app id should be 480 for testing purposes
        public uint appId = 480;
        public bool debugClientMessages = false;
        public bool debugServerMessages = false;

        // Dynamically let other classes subscribe to these events
        public Dictionary<NetworkMessageType, System.Action<byte[], ulong>> clientMessageEvents;
        public Dictionary<NetworkMessageType, System.Action<byte[], ulong>> serverMessageEvents;

        private Client client = null;
        private int serverMessagesOffset = 0;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
                Debug.LogWarning(nameof(NetworkManager) + " cannot have multiple instances! Duplicate destroyed.");
                return;
            }

            // Make sure that the plugins are found in both editor and build
            System.Environment.SetEnvironmentVariable("PATH", Application.dataPath + "/Plugins/", System.EnvironmentVariableTarget.Process);

            // Configurate facepunch steamworks sdk
            Config.ForUnity(Application.platform.ToString());

            try
            {
                // Create a steam_appid.txt with the app id in it, required by the SDK
                System.IO.File.WriteAllText(Application.dataPath + "/../steam_appid.txt", appId.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogError("Couldn't write steam_appid.txt: " + e.Message);
            }

            // Create the client
            client = new Client(appId);

            if (client != null)
            {
                if (client.IsValid)
                {
                    Debug.Log("Steam Initialized: " + client.Username + " / " + client.SteamId);
                }
                else
                {
                    client.Dispose();
                    DialogBox.Show("Make sure that you are online and Steam is running.\nDo you want to exit the game?", true, true, Application.Quit, null);
                    return;
                }
            }
            else
            {
                DialogBox.Show("Client initialization for Steam failed!\nThe client is null.", false, false, null, null);
                return;
            }

            // Create all the actions for incoming network messages
            clientMessageEvents = new Dictionary<NetworkMessageType, System.Action<byte[], ulong>>();
            serverMessageEvents = new Dictionary<NetworkMessageType, System.Action<byte[], ulong>>();

            System.Array types = System.Enum.GetValues(typeof(NetworkMessageType));
            serverMessagesOffset = types.Length;

            // Listen to all the network messages on different channels - each represents a message type
            foreach (NetworkMessageType type in types)
            {
                // Listen to messages for the client
                client.Networking.SetListenChannel((int)type, true);
                clientMessageEvents[type] = new System.Action<byte[], ulong>(DebugClientMessageEvent);

                // Listen to all messages for the server with an offset -> know which messages are for the server
                client.Networking.SetListenChannel(serverMessagesOffset + (int)type, true);
                serverMessageEvents[type] = new System.Action<byte[], ulong>(DebugServerMessageEvent);
            }

            client.Networking.OnIncomingConnection += OnIncomingConnection;
            client.Networking.OnConnectionFailed += OnConnectionFailed;
            client.Networking.OnP2PData += OnP2PData;
        }

        void Update()
        {
            if (IsInitialized())
            {
                UnityEngine.Profiling.Profiler.BeginSample("Steam Update");
                client.Update();
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        public bool IsInitialized ()
        {
            return client != null && client.IsValid;
        }

        void DebugClientMessageEvent(byte[] data, ulong steamID)
        {
            if (debugClientMessages)
            {
                string message = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log("Client received " + data.Length + " bytes from " + steamID + ":\n" + message);
            }
        }

        void DebugServerMessageEvent(byte[] data, ulong steamID)
        {
            if (debugServerMessages)
            {
                string message = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log("Server received " + data.Length + " bytes from " + steamID + ":\n" + message);
            }
        }

        bool OnIncomingConnection(ulong steamID)
        {
            Debug.Log("Incoming peer to peer connection from user " + steamID);
            return true;
        }

        void OnConnectionFailed(ulong steamID, Networking.SessionError sessionError)
        {
            DialogBox.Show("Connection failed with user " + steamID + ", " + sessionError, false, false, null, null);
        }

        // This is where all the messages are received and delegated to the respective events
        void OnP2PData(ulong steamID, byte[] data, int dataLength, int channel)
        {
            byte[] trimmedData = new byte[dataLength];
            System.Array.Copy(data, trimmedData, dataLength);

            if (channel < serverMessagesOffset)
            {
                // The message is for the client
                NetworkMessageType messageType = (NetworkMessageType)channel;
                clientMessageEvents[messageType].Invoke(trimmedData, steamID);
            }
            else
            {
                // The message is for the server (which is running on this client)
                NetworkMessageType messageType = (NetworkMessageType)(channel - serverMessagesOffset);
                serverMessageEvents[messageType].Invoke(trimmedData, steamID);
            }
        }

        private void SendToClient(ulong steamID, byte[] data, int channel, SendType sendType)
        {
            if (client != null && client.IsValid)
            {
                // Send the message to the client on the channel of this message type
                if (!client.Networking.SendP2PPacket(steamID, data, data.Length, sendType.GetNetworkingSendType(), channel))
                {
                    Debug.Log("Could not send peer to peer packet to user " + steamID);
                }
                else if (debugClientMessages)
                {
                    Debug.Log("Sending message to " + steamID + ":\n" + System.Text.Encoding.UTF8.GetString(data));
                }
            }
        }

        public void SendToClient(ulong steamID, byte[] data, NetworkMessageType networkMessageType, SendType sendType)
        {
            SendToClient(steamID, data, (int)networkMessageType, sendType);
        }

        public void SendToAllClients(byte[] data, NetworkMessageType networkMessageType, SendType sendType)
        {
            if (client != null && client.IsValid)
            {
                ulong[] lobbyMemberIDs = client.Lobby.GetMemberIDs();

                foreach (ulong steamID in lobbyMemberIDs)
                {
                    SendToClient(steamID, data, (int)networkMessageType, sendType);
                }
            }
        }

        public void SendToServer(byte[] data, NetworkMessageType networkMessageType, SendType sendType)
        {
            // Messages for the server are sent on a different channel than messages for a client
            // This way the client knows if the incoming message is for him as a client or him as a server
            SendToClient(client.Lobby.Owner, data, serverMessagesOffset + (int)networkMessageType, sendType);
        }

        public ulong[] GetLobbyMemberIDs()
        {
            return client.Lobby.GetMemberIDs();
        }

        void OnDestroy()
        {
            if (IsInitialized())
            {
                client.Networking.OnIncomingConnection -= OnIncomingConnection;
                client.Networking.OnConnectionFailed -= OnConnectionFailed;
                client.Networking.OnP2PData -= OnP2PData;
                client.Dispose();
                client = null;
            }
        }
    }
}
