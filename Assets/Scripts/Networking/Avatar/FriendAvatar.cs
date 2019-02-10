using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MastersOfTempest.Networking
{
    public class FriendAvatar : Avatar
    {
        public void Invite()
        {
            Facepunch.Steamworks.Client.Instance.Lobby.InviteUserToLobby(steamID);
        }
    }
}
