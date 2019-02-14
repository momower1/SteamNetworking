using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;
using System;

[RequireComponent(typeof(PlayerInput))]
public class Player : NetworkBehaviour
{
    public ulong controllingSteamID = 0;
    public bool isControlling = false;

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
            isControlling = true;
            GetComponent<PlayerInput>().StartPlayerInputLoop();
        }
    }

    protected void OnGUI()
    {
        if (isControlling)
        {
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(Screen.width / 2, Screen.height / 2, Screen.height / 100, Screen.height / 100), Texture2D.whiteTexture);
        }
    }
}
