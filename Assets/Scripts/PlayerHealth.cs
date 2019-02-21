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

    protected override void Update()
    {
        base.Update();

        if (Input.GetKeyDown(KeyCode.Return))
        {
            health = 0;
        }
    }

    protected override void UpdateClient()
    {
        if (player.isControlling)
        {
            if (player.isDead)
            {
                // Synchronize the cameras
                Camera playerCamera = Camera.main;
                playerCamera.transform.position = transform.position;
                playerCamera.transform.rotation = transform.rotation;
            }
            else if (health <= 0)
            {
                // Die on the client, make sure that the player cannot move anymore
                player.isDead = true;
            }
        }
    }

    protected override void UpdateServer()
    {
        if (!player.isDead && health <= 0)
        {
            player.isDead = true;

            // Die on the server
            StartCoroutine(DieOnServer());
        }
    }

    protected IEnumerator DieOnServer ()
    {
        // Enable rigid body physics
        GetComponent<Rigidbody>().isKinematic = false;

        yield return new WaitForSecondsRealtime(3);

        // Spawn player again
        FindObjectOfType<PlayerSpawner>().SpawnPlayer(player.controllingSteamID);

        // Destroy old player
        Destroy(gameObject);
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // Update health
        health = BitConverter.ToSingle(data, 0);
    }

    protected void OnCollisionEnter(Collision collision)
    {
        Projectile projectile = collision.gameObject.GetComponent<Projectile>();

        // Only take damage if the projectile was shot by another player
        if (projectile != null && projectile.playerSteamID != player.controllingSteamID)
        {
            // Take damage
            health = Mathf.Clamp01(health - 0.05f);

            // Send new health to all clients
            SendToAllClients(BitConverter.GetBytes(health), SendType.Reliable);

            // Destroy projectile
            Destroy(collision.gameObject.gameObject);
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

            if (player.isDead)
            {
                // Deadscreen
                GUI.color = Color.red;
                GUI.Label(new Rect(Screen.width / 2, Screen.height / 2, Screen.width / 2, Screen.height / 2), "You are dead");
            }
        }
    }
}
