﻿using System.Collections;
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
        if (player.isControlling && !player.isDead)
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
        GameObject projectile = GameServer.Instance.InstantiateInScene(projectilePrefab, transform.position + transform.forward, transform.rotation, null);
        projectile.GetComponent<Projectile>().playerSteamID = player.controllingSteamID;
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