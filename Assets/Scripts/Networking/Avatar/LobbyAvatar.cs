using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SteamNetworking
{
    public class LobbyAvatar : Avatar
    {
        public bool ready = false;

        public Image imageReadyOutline;

        public void Refresh ()
        {
            if (bool.TryParse(Facepunch.Steamworks.Client.Instance.Lobby.GetMemberData(steamID, "Ready"), out ready))
            {
                imageReadyOutline.color = ready ? Color.green : Color.red;
            }
        }
    }
}
