using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;

namespace SteamNetworking
{
    public class LobbyChat : MonoBehaviour
    {
        public UnityEngine.UI.InputField inputFieldChat;
        public UnityEngine.UI.Text textChat;

        void Start()
        {
            if (Client.Instance != null)
            {
                Client.Instance.Lobby.OnChatMessageRecieved += OnChatMessageReceived;
            }
        }

        private void OnChatMessageReceived(ulong steamID, byte[] data, int dataLength)
        {
            byte[] messageData = new byte[dataLength];
            System.Array.Copy(data, messageData, dataLength);
            string message = System.Text.Encoding.UTF8.GetString(messageData);
            textChat.text += "<color=grey>[" + Client.Instance.Friends.Get(steamID).Name + "]: </color>" + message + "\n";
        }

        public void SendChatMessage()
        {
            if (Client.Instance != null)
            {
                Client.Instance.Lobby.SendChatMessage(inputFieldChat.text);

                inputFieldChat.text = "";
                inputFieldChat.ActivateInputField();
                inputFieldChat.Select();
                inputFieldChat.placeholder.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (Client.Instance != null)
            {
                Client.Instance.Lobby.OnChatMessageRecieved -= OnChatMessageReceived;
            }
        }
    }
}
