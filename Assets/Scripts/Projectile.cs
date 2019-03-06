using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;
using System;

public class Projectile : NetworkBehaviour
{
    // The id of the player that shot this projectile
    public int playerNetworkID;
    [SerializeField]
    protected float speed = 10;
    [SerializeField]
    protected float timeUntilDestroy = 10;
    [SerializeField]
    protected LineRenderer lineRenderer;

    private Player player;

    private struct ProjectileMessage
    {
        public int playerNetworkID;
        public bool deflect;
        public Vector3 deflectPosition;

        public ProjectileMessage (int playerNetworkID, bool deflect, Vector3 deflectPosition)
        {
            this.playerNetworkID = playerNetworkID;
            this.deflect = deflect;
            this.deflectPosition = deflectPosition;
        }
    }

    protected override void Start()
    {
        base.Start();

        lineRenderer.SetPositions(new Vector3[] { transform.position, transform.position });
    }

    protected override void StartServer()
    {
        ProjectileMessage projectileMessage = new ProjectileMessage(playerNetworkID, false, Vector3.zero);
        SendToAllClients(ByteSerializer.GetBytes(projectileMessage), SendType.Reliable);

        player = GameServer.Instance.GetNetworkObject(playerNetworkID).GetComponent<Player>();
    }

    protected override void Update()
    {
        base.Update();

        lineRenderer.SetPosition(lineRenderer.positionCount - 1, transform.position);
    }

    protected override void UpdateClient()
    {
        if (player != null && player.isControlling)
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

            // Make camera follow the projectile on the client
            Camera.main.transform.rotation = transform.rotation;
            Camera.main.transform.position = transform.position - 2 * transform.forward + 0.1f * transform.up;
        }
    }

    protected override void UpdateServer()
    {
        if (timeUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            transform.position += transform.forward * speed * Time.deltaTime;
            timeUntilDestroy -= Time.deltaTime;
        }
    }

    protected void OnTriggerEnter(Collider other)
    {
        if (networkObject.onServer)
        {
            Player player = other.gameObject.GetComponent<Player>();

            if (player != null && playerNetworkID != player.GetComponent<NetworkObject>().networkID)
            {
                player.GetComponent<PlayerHealth>().DamageOnServer(0.1f);
            }

            Destroy(gameObject);
        }
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        ProjectileMessage projectileMessage = ByteSerializer.FromBytes<ProjectileMessage>(data);
        playerNetworkID = projectileMessage.playerNetworkID;

        // Find the corresponding player
        player = GameClient.Instance.GetObjectFromServer(playerNetworkID).GetComponent<Player>();

        if (projectileMessage.deflect)
        {
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, projectileMessage.deflectPosition);
            lineRenderer.positionCount++;
        }
    }

    protected override void OnServerReceivedMessage(string message, ulong steamID)
    {
        if (message.Equals("W"))
        {
            transform.Rotate(transform.right, 90, Space.World);
        }
        else if (message.Equals("A"))
        {
            transform.Rotate(transform.up, -90, Space.World);
        }
        else if (message.Equals("S"))
        {
            transform.Rotate(transform.right, -90, Space.World);
        }
        else if (message.Equals("D"))
        {
            transform.Rotate(transform.up, 90, Space.World);
        }

        // Send this to the client
        ProjectileMessage projectileMessage = new ProjectileMessage(playerNetworkID, true, lineRenderer.GetPosition(lineRenderer.positionCount - 1));
        SendToAllClients(ByteSerializer.GetBytes(projectileMessage), SendType.Reliable);

        // Add this position to the line renderer
        lineRenderer.positionCount++;
    }

    protected override void OnDestroyClient()
    {
        if (player != null)
        {
            player.isShooting = false;
        }
    }

    protected override void OnDestroyServer()
    {
        if (player != null)
        {
            player.isShooting = false;
        }
    }
}
