using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    public class MessageNetworkBehaviour
    {
        // Fixed size
        public int networkID;                                       // 4 bytes
        public int index;                                           // 4 bytes

        // Dynamic size
        public byte[] data;                                         // ? bytes

        public MessageNetworkBehaviour ()
        {
            // Empty constructor
        }

        public MessageNetworkBehaviour(int networkID, int index, byte[] data)
        {
            this.networkID = networkID;
            this.index = index;
            this.data = data;
        }

        public byte[] ToBytes()
        {
            ArrayList bytes = new ArrayList();

            // Fixed size
            bytes.AddRange(BitConverter.GetBytes(networkID));
            bytes.AddRange(BitConverter.GetBytes(index));

            // Dynamic size
            bytes.AddRange(BitConverter.GetBytes(data.Length));
            bytes.AddRange(data);

            return (byte[])bytes.ToArray(typeof(byte));
        }

        public static MessageNetworkBehaviour FromBytes(byte[] data, int startIndex)
        {
            MessageNetworkBehaviour messageNetworkBehaviour = new MessageNetworkBehaviour();

            // Fixed size
            messageNetworkBehaviour.networkID = BitConverter.ToInt32(data, startIndex);
            messageNetworkBehaviour.index = BitConverter.ToInt32(data, startIndex + 4);

            // Dynamic size
            int dataLength = BitConverter.ToInt32(data, startIndex + 8);
            messageNetworkBehaviour.data = new byte[dataLength];
            Array.Copy(data, startIndex + 12, messageNetworkBehaviour.data, 0, dataLength);

            return messageNetworkBehaviour;
        }
    };
}
