using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamNetworking.Test
{
    public class MoveNetworkBehaviour : NetworkBehaviour
    {
        [SerializeField]
        private float inputsPerSec = 1;
        [SerializeField]
        private float movementSpeed = 1;
        [SerializeField]
        private float jumpHeight = 1;

        private float horizontalInput = 0;
        private float verticalInput = 0;
        private bool jump = false;

        private struct MessageMove
        {
            public float horizontalInput;
            public float verticalInput;
            public bool jump;

            public MessageMove (float horizontalInput, float verticalInput, bool jump)
            {
                this.horizontalInput = horizontalInput;
                this.verticalInput = verticalInput;
                this.jump = jump;
            }
        };

        protected override void StartClient()
        {
            StartCoroutine(SendInputToServer());
        }

        protected override void UpdateClient()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                MessageMove message = new MessageMove(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), true);
                SendToServer(ByteSerializer.GetBytes(message), SendType.Reliable);
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                networkObject.interpolateOnClient = true;
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                networkObject.interpolateOnClient = false;
            }
        }

        IEnumerator SendInputToServer ()
        {
            while (true)
            {
                MessageMove message = new MessageMove(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), false);
                SendToServer(ByteSerializer.GetBytes(message), SendType.Unreliable);

                yield return new WaitForSecondsRealtime(1.0f / inputsPerSec);
            }
        }

        protected override void UpdateServer()
        {
            Vector3 force = movementSpeed * Time.deltaTime * (Vector3.forward * verticalInput + Vector3.right * horizontalInput);
            GetComponent<Rigidbody>().AddForce(force, ForceMode.VelocityChange);
        }

        protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
        {
            MessageMove message = ByteSerializer.FromBytes<MessageMove>(data);
            horizontalInput = message.horizontalInput;
            verticalInput = message.verticalInput;
            jump = message.jump;

            if (jump)
            {
                Vector3 jumpForce = jumpHeight * Vector3.up;
                GetComponent<Rigidbody>().AddForce(jumpForce, ForceMode.VelocityChange);
            }
        }
    }
}
