using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    class MessageNetworkObjectList
    {
        // Dynamic size, stores byte representations of network object messages
        public LinkedList<byte[]> messages = new LinkedList<byte[]>();    // ? bytes

        public int GetLength ()
        {
            // Message count
            int length = 4;

            foreach (byte[] b in messages)
            {
                length += 4 + b.Length;
            }

            return length;
        }

        public byte[] ToBytes()
        {
            ArrayList data = new ArrayList();

            // Dynamic size
            data.AddRange(BitConverter.GetBytes(messages.Count));

            foreach (byte[] b in messages)
            {
                data.AddRange(BitConverter.GetBytes(b.Length));
                data.AddRange(b);
            }

            return (byte[])data.ToArray(typeof(byte));
        }

        public static MessageNetworkObjectList FromBytes(byte[] data, int startIndex)
        {
            MessageNetworkObjectList messageNetworkObjectList = new MessageNetworkObjectList();

            // Get messages length
            int messagesCount = BitConverter.ToInt32(data, startIndex);
            int index = startIndex + 4;

            // Read and assign all the dynamically sized network object messages
            for (int i = 0; i < messagesCount; i++)
            {
                int bLength = BitConverter.ToInt32(data, index);
                index += 4;

                byte[] b = new byte[bLength];
                Array.Copy(data, index, b, 0, bLength);
                index += bLength;

                messageNetworkObjectList.messages.AddLast(b);
            }

            return messageNetworkObjectList;
        }
    }
}