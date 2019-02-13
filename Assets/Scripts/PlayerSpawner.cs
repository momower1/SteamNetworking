using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    public void SpawnPlayers ()
    {
        ulong[] memberIDs = Client.Instance.Lobby.GetMemberIDs();

        foreach (ulong steamID in memberIDs)
        {
            Player player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
            player.steamID = steamID;
        }
    }
}
