using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamNetworking
{
    /// <summary>
    /// Wrapper for the Facepunch.Steamworks.Networking.SendType
    /// </summary>
    public enum SendType
    {
        Unreliable = 0,
        UnreliableNoDelay = 1,
        Reliable = 2,
        ReliableWithBuffering = 3
    };

    public static class SendTypeExtensionMethods
    {
        public static Facepunch.Steamworks.Networking.SendType GetNetworkingSendType(this SendType sendType)
        {
            return (Facepunch.Steamworks.Networking.SendType)sendType;
        }
    }
}
