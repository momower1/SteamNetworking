using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;
using System;

[RequireComponent(typeof(Player))]
public class PlayerHealth : NetworkBehaviour
{
    [SerializeField, Range(0, 1)]
    protected float health = 1.0f;

    protected Player player;

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // Update health
        health = BitConverter.ToSingle(data, 0);
    }

    protected void OnTriggerEnter(Collider other)
    {
        Projectile projectile = other.GetComponent<Projectile>();

        // Only take damage if the projectile was shot by another player
        if (projectile != null && projectile.playerSteamID != player.controllingSteamID)
        {
            // Take damage
            health = Mathf.Clamp01(health - 0.1f);

            // Send new health to all clients
            SendToAllClients(BitConverter.GetBytes(health), SendType.Reliable);

            // Destroy projectile
            Destroy(other.gameObject);
        }
    }

    protected void OnGUI()
    {
        if (player.isControlling)
        {
            // Healthbar
            Rect healthbar = new Rect(Screen.height / 20, Screen.height - Screen.height / 10, Screen.width / 3, Screen.height / 20);
            GUI.color = Color.red;
            GUI.DrawTexture(healthbar, Texture2D.whiteTexture);
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(healthbar.x, healthbar.y, health * healthbar.width, healthbar.height), Texture2D.whiteTexture);
        }
    }
}
