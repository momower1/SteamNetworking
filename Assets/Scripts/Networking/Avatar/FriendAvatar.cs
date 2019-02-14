using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SteamNetworking.Avatar
{
    public class FriendAvatar : Avatar
    {
        public void Invite()
        {
            Facepunch.Steamworks.Client.Instance.Lobby.InviteUserToLobby(steamID);
        }
    }
}
