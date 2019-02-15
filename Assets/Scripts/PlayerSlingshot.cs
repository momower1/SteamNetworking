using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

[RequireComponent(typeof(Player))]
public class PlayerSlingshot : NetworkBehaviour
{
    [SerializeField]
    protected GameObject projectilePrefab;

    protected Player player;

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
    }

    protected override void UpdateClient()
    {
        if (player.isControlling)
        {
            // Send message to shoot projectile
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                SendToServer("Slingshot", SendType.Reliable);
            }
        }
    }

    protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // Spawn projectile
        GameServer.Instance.InstantiateInScene(projectilePrefab, transform.position, transform.rotation, null);
    }
}
