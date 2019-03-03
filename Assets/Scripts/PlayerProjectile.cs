using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

[RequireComponent(typeof(Player))]
public class PlayerProjectile : NetworkBehaviour
{
    [SerializeField]
    protected GameObject projectilePrefab;

    protected Player player;
    protected PlayerMovement playerMovement;

    private Projectile projectile;

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    protected override void UpdateClient()
    {
        if (player.isControlling && !player.isDead)
        {
            if (player.isShooting)
            {
                if (Input.GetKeyDown(KeyCode.W))
                {
                    SendToServer("W", SendType.Reliable);
                }
                else if (Input.GetKeyDown(KeyCode.A))
                {
                    SendToServer("A", SendType.Reliable);
                }
                else if (Input.GetKeyDown(KeyCode.S))
                {
                    SendToServer("S", SendType.Reliable);
                }
                else if (Input.GetKeyDown(KeyCode.D))
                {
                    SendToServer("D", SendType.Reliable);
                }
            }
            else if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                SendToServer("ProjectileStart", SendType.Reliable);
                player.isShooting = true;
            }
        }
    }

    protected override void OnClientReceivedMessage(string message, ulong steamID)
    {
        player.isShooting = false;
    }

    protected override void OnServerReceivedMessage(string message, ulong steamID)
    {
        if (message.Equals("ProjectileStart"))
        {
            StartCoroutine(ProjectileStart());
        }
        else
        {
            projectile.DeflectServer(message);
        }
    }

    protected IEnumerator ProjectileStart ()
    {
        player.isShooting = true;

        // Wait until the player is at the position that the client was at when the button was pressed
        yield return new WaitForSecondsRealtime(1.0f / playerMovement.inputsPerSec);

        projectile = GameServer.Instance.InstantiateInScene(projectilePrefab, transform.position + transform.forward, transform.rotation, null).GetComponent<Projectile>();
        projectile.playerSteamID = player.controllingSteamID;
        projectile.SetPlayerProjectile(this);
    }

    public void ProjectileEnd ()
    {
        player.isShooting = false;
        Destroy(projectile.gameObject);
        SendToClient(player.controllingSteamID, "ProjectileEnd", SendType.Reliable);
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
