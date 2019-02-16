using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;
using SteamNetworking;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform[] playerSpawnTransforms;

    private int spawnIndex = 0;

    public void SpawnPlayers ()
    {
        ulong[] lobbyMemberIDs = Client.Instance.Lobby.GetMemberIDs();

        for (int i = 0; i < lobbyMemberIDs.Length; i++)
        {
            SpawnPlayer(lobbyMemberIDs[i]);
        }
    }

    public void SpawnPlayer (ulong steamID)
    {
        Vector3 spawnPosition = playerSpawnTransforms[spawnIndex].position + playerPrefab.transform.position;
        Quaternion spawnRotation = playerSpawnTransforms[spawnIndex].rotation * playerPrefab.transform.rotation;

        Player player = GameServer.Instance.InstantiateInScene(playerPrefab, spawnPosition, spawnRotation, null).GetComponent<Player>();
        player.controllingSteamID = steamID;

        spawnIndex = (spawnIndex + 1) % playerSpawnTransforms.Length;
    }
}
