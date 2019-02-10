using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MastersOfTempest.Networking
{
    public class MessageServerObject
    {
        // Fixed size
        public float time;                                          // 4 bytes
        public bool hasParent;                                      // 1 byte
        public int resourceID;                                      // 4 bytes
        public int instanceID;                                      // 4 bytes
        public int rootInstanceID;                                  // 4 bytes
        public int parentInstanceID;                                // 4 bytes
        public Vector3 localPosition;                               // 12 bytes
        public Quaternion localRotation;                            // 16 bytes
        public Vector3 localScale;                                  // 12 bytes

        // Dynamic size
        public string name;                                         // ? bytes

        public MessageServerObject ()
        {
            // Empty constructor
        }

        public MessageServerObject(ServerObject serverObject)
        {
            if (serverObject.transform.parent != null)
            {
                parentInstanceID = serverObject.transform.parent.GetInstanceID();
                hasParent = true;
            }
            else
            {
                parentInstanceID = 0;
                hasParent = false;
            }

            // Set and save the last update time of the server object
            time = serverObject.lastUpdate = Time.time;
            name = serverObject.name;
            resourceID = serverObject.resourceID;
            instanceID = serverObject.transform.GetInstanceID();
            rootInstanceID = serverObject.root.transform.GetInstanceID();
            localPosition = serverObject.transform.localPosition;
            localRotation = serverObject.transform.localRotation;
            localScale = serverObject.transform.localScale;
        }

        public byte[] ToBytes ()
        {
            ArrayList data = new ArrayList();

            // Fixed size
            data.AddRange(BitConverter.GetBytes(time));
            data.AddRange(BitConverter.GetBytes(hasParent));
            data.AddRange(BitConverter.GetBytes(resourceID));
            data.AddRange(BitConverter.GetBytes(instanceID));
            data.AddRange(BitConverter.GetBytes(rootInstanceID));
            data.AddRange(BitConverter.GetBytes(parentInstanceID));
            data.AddRange(BitConverter.GetBytes(localPosition.x));
            data.AddRange(BitConverter.GetBytes(localPosition.y));
            data.AddRange(BitConverter.GetBytes(localPosition.z));
            data.AddRange(BitConverter.GetBytes(localRotation.x));
            data.AddRange(BitConverter.GetBytes(localRotation.y));
            data.AddRange(BitConverter.GetBytes(localRotation.z));
            data.AddRange(BitConverter.GetBytes(localRotation.w));
            data.AddRange(BitConverter.GetBytes(localScale.x));
            data.AddRange(BitConverter.GetBytes(localScale.y));
            data.AddRange(BitConverter.GetBytes(localScale.z));

            // Dynamic size
            byte[] nameData = System.Text.Encoding.UTF8.GetBytes(name);
            data.AddRange(BitConverter.GetBytes(nameData.Length));
            data.AddRange(nameData);

            return (byte[]) data.ToArray(typeof(byte));
        }

        public static MessageServerObject FromBytes (byte[] data, int startIndex)
        {
            MessageServerObject messageServerObject = new MessageServerObject();

            // Fixed size
            messageServerObject.time = BitConverter.ToSingle(data, startIndex);
            messageServerObject.hasParent = BitConverter.ToBoolean(data, startIndex + 4);
            messageServerObject.resourceID = BitConverter.ToInt32(data, startIndex + 5);
            messageServerObject.instanceID = BitConverter.ToInt32(data, startIndex + 9);
            messageServerObject.rootInstanceID = BitConverter.ToInt32(data, startIndex + 13);
            messageServerObject.parentInstanceID = BitConverter.ToInt32(data, startIndex + 17);
            messageServerObject.localPosition.x = BitConverter.ToSingle(data, startIndex + 21);
            messageServerObject.localPosition.y = BitConverter.ToSingle(data, startIndex + 25);
            messageServerObject.localPosition.z = BitConverter.ToSingle(data, startIndex + 29);
            messageServerObject.localRotation.x = BitConverter.ToSingle(data, startIndex + 33);
            messageServerObject.localRotation.y = BitConverter.ToSingle(data, startIndex + 37);
            messageServerObject.localRotation.z = BitConverter.ToSingle(data, startIndex + 41);
            messageServerObject.localRotation.w = BitConverter.ToSingle(data, startIndex + 45);
            messageServerObject.localScale.x = BitConverter.ToSingle(data, startIndex + 49);
            messageServerObject.localScale.y = BitConverter.ToSingle(data, startIndex + 53);
            messageServerObject.localScale.z = BitConverter.ToSingle(data, startIndex + 57);

            // Dynamic size
            int nameDataLength = BitConverter.ToInt32(data, startIndex + 61);
            messageServerObject.name = System.Text.Encoding.UTF8.GetString(data, startIndex + 65, nameDataLength);

            return messageServerObject;
        }
    }
}
