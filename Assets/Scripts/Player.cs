﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;
using System;

public class Player : NetworkBehaviour
{
    public ulong controllingSteamID = 0;
    public PlayerInput playerInput;

    protected override void StartServer()
    {
        // Synchronize the assigned steam id
        SendToAllClients(BitConverter.GetBytes(controllingSteamID), Facepunch.Steamworks.Networking.SendType.Reliable);
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        controllingSteamID = BitConverter.ToUInt64(data, 0);

        // Attach camera to that player if this client should control it
        if (controllingSteamID == Facepunch.Steamworks.Client.Instance.SteamId)
        {
            Camera.main.transform.SetParent(transform, false);
            playerInput.StartPlayerInputLoop();
        }
    }
}