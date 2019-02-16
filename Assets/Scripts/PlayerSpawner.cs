using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Facepunch.Steamworks;
using SteamNetworking;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform[] playerSpawnTransforms;

    public void SpawnPlayers ()
    {
        ulong[] lobbyMemberIDs = Client.Instance.Lobby.GetMemberIDs();

        for (int i = 0; i < lobbyMemberIDs.Length; i++)
        {
            Vector3 spawnPosition = playerSpawnTransforms[i].position + playerPrefab.transform.position;
            Quaternion spawnRotation = playerSpawnTransforms[i].rotation * playerPrefab.transform.rotation;

            Player player = GameServer.Instance.InstantiateInScene(playerPrefab, spawnPosition, spawnRotation, null).GetComponent<Player>();
            player.controllingSteamID = lobbyMemberIDs[i];
        }
    }
}
