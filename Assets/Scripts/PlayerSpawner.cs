using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;
using SteamNetworking;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    public void SpawnPlayers ()
    {
        ulong[] memberIDs = Client.Instance.Lobby.GetMemberIDs();

        foreach (ulong steamID in memberIDs)
        {
            Player player = GameServer.Instance.InstantiateInScene(playerPrefab, playerPrefab.transform.position, playerPrefab.transform.rotation, null).GetComponent<Player>();
            player.controllingSteamID = steamID;
        }
    }
}
