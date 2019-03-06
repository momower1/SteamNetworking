using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

[RequireComponent(typeof(Player))]
public class PlayerShoot : NetworkBehaviour
{
    [SerializeField]
    protected GameObject projectilePrefab;

    protected Player player;
    protected PlayerMovement playerMovement;

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    protected override void UpdateClient()
    {
        if (player.isControlling && !player.isDead && !player.isShooting && Input.GetKeyDown(KeyCode.Mouse0))
        {
            SendToServer("Shoot", SendType.Reliable);
            player.isShooting = true;
        }
    }

    protected override void OnServerReceivedMessage(string message, ulong steamID)
    {
        player.isShooting = true;

        GameObject tmp = GameServer.Instance.InstantiateInScene(projectilePrefab, transform.position + transform.forward, transform.rotation, null);
        tmp.GetComponent<Projectile>().playerNetworkID = networkObject.networkID;
    }

    protected void OnGUI()
    {
        if (player.isControlling && !player.isDead)
        {
            // Crosshair
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(Screen.width / 2, Screen.height / 2, Screen.height / 200, Screen.height / 200), Texture2D.whiteTexture);
        }
    }
}
