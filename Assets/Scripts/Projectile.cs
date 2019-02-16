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

    protected override void StartServer()
    {
        GetComponent<Rigidbody>().velocity = speed * transform.forward;

        SendToAllClients(BitConverter.GetBytes(playerSteamID), SendType.Reliable);
    }

    protected override void UpdateServer()
    {
        if (timeUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }

        timeUntilDestroy -= Time.deltaTime;
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        playerSteamID = BitConverter.ToUInt64(data, 0);
    }
}
