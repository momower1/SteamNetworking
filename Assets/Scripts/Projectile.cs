using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;
using System;

public class Projectile : NetworkBehaviour
{
    // The steam id of the player that shot this projectile
    public ulong playerSteamID;
    [SerializeField]
    protected float speed = 10;
    [SerializeField]
    protected float timeUntilDestroy = 10;
    [SerializeField]
    protected LineRenderer lineRenderer;

    private struct ProjectileMessage
    {
        public ulong playerSteamID;
        public bool deflect;
        public Vector3 deflectPosition;

        public ProjectileMessage (ulong playerSteamID, bool deflect, Vector3 deflectPosition)
        {
            this.playerSteamID = playerSteamID;
            this.deflect = deflect;
            this.deflectPosition = deflectPosition;
        }
    }

    private PlayerProjectile playerProjectile;
    private bool finished = false;

    protected override void Start()
    {
        base.Start();

        lineRenderer.SetPositions(new Vector3[] { transform.position, transform.position });
    }

    protected override void StartServer()
    {
        ProjectileMessage projectileMessage = new ProjectileMessage(playerSteamID, false, Vector3.zero);
        SendToAllClients(ByteSerializer.GetBytes(projectileMessage), SendType.Reliable);
    }

    protected override void Update()
    {
        base.Update();

        lineRenderer.SetPosition(lineRenderer.positionCount - 1, transform.position);
    }

    protected override void UpdateServer()
    {
        if (!finished)
        {
            if (timeUntilDestroy <= 0)
            {
                FinishProjectile();
            }
            else
            {
                transform.position += transform.forward * speed * Time.deltaTime;
                timeUntilDestroy -= Time.deltaTime;
            }
        }
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        ProjectileMessage projectileMessage = ByteSerializer.FromBytes<ProjectileMessage>(data);
        playerSteamID = projectileMessage.playerSteamID;

        if (projectileMessage.deflect)
        {
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, projectileMessage.deflectPosition);
            lineRenderer.positionCount++;
        }
    }

    public void SetPlayerProjectile (PlayerProjectile playerProjectile)
    {
        this.playerProjectile = playerProjectile;
    }

    public void DeflectServer (string input)
    {
        if (input.Equals("W"))
        {
            transform.Rotate(transform.right, 90, Space.World);
        }
        else if (input.Equals("A"))
        {
            transform.Rotate(transform.up, -90, Space.World);
        }
        else if (input.Equals("S"))
        {
            transform.Rotate(transform.right, -90, Space.World);
        }
        else if (input.Equals("D"))
        {
            transform.Rotate(transform.up, 90, Space.World);
        }

        // Send this to the client
        ProjectileMessage projectileMessage = new ProjectileMessage(playerSteamID, true, lineRenderer.GetPosition(lineRenderer.positionCount - 1));
        SendToAllClients(ByteSerializer.GetBytes(projectileMessage), SendType.Reliable);

        // Add this position to the line renderer
        lineRenderer.positionCount++;
    }

    public void FinishProjectile()
    {
        finished = true;
        playerProjectile.ProjectileEnd();
    }
}
