using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SteamNetworking.Test
{
    public class ExpandSplitNetworkBehaviour : NetworkBehaviour
    {
        private struct MessageExpandSplit
        {
            public bool expand;

            public MessageExpandSplit (bool expand)
            {
                this.expand = expand;
            }
        };

        protected override void UpdateClient()
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                MessageExpandSplit message = new MessageExpandSplit(true);
                SendToServer(ByteSerializer.GetBytes(message), SendType.Reliable);
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                MessageExpandSplit message = new MessageExpandSplit(false);
                SendToServer(ByteSerializer.GetBytes(message), SendType.Reliable);
            }
        }

        protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
        {
            MessageExpandSplit message = ByteSerializer.FromBytes<MessageExpandSplit>(data);

            if (message.expand)
            {
                transform.localScale += Vector3.one;
            }
            else
            {
                // Make smaller and duplicate this object
                transform.localScale *= 0.5f;

                // Duplicate the object on the server scene
                Scene previouslyActiveScene = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(GameServer.Instance.gameObject.scene);
                Instantiate(gameObject, transform.parent, true);
                SceneManager.SetActiveScene(previouslyActiveScene);
            }
        }
    }
}
