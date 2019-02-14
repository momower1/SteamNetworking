using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

[RequireComponent(typeof(Player))]
public class PlayerInput : NetworkBehaviour
{
    [SerializeField]
    protected float inputsPerSec = 60;
    [SerializeField]
    protected float mouseSensitivity = 1;
    [SerializeField]
    protected float movementSpeed = 10;

    protected Player player;
    protected Camera playerCamera;
    protected PlayerInputMessage lastPlayerInputMessage;

    protected struct PlayerInputMessage
    {
        public float mouseX;
        public float mouseY;
        public int mouse0;
        public int w;
        public int a;
        public int s;
        public int d;
    };

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
    }

    protected override void UpdateClient()
    {
        if (player.isControlling)
        {
            // Get input
            float mouseX = mouseSensitivity * Input.GetAxisRaw("Mouse X");
            float mouseY = mouseSensitivity * Input.GetAxisRaw("Mouse Y");
            int w = Input.GetKey(KeyCode.W) ? 1 : 0;
            int a = Input.GetKey(KeyCode.A) ? 1 : 0;
            int s = Input.GetKey(KeyCode.S) ? 1 : 0;
            int d = Input.GetKey(KeyCode.D) ? 1 : 0;

            // Simulate camera movement locally
            SimulateMovement(playerCamera.transform, mouseX, mouseY, w, a, s, d);
        }
    }

    protected override void UpdateServer()
    {
        // Do the same movement as the client already did
        SimulateMovement
        (
            transform,
            lastPlayerInputMessage.mouseX,
            lastPlayerInputMessage.mouseY,
            lastPlayerInputMessage.w,
            lastPlayerInputMessage.a,
            lastPlayerInputMessage.s,
            lastPlayerInputMessage.d
        );
    }

    protected IEnumerator PlayerInputLoop ()
    {
        while (true)
        {
            PlayerInputMessage playerInputMessage = new PlayerInputMessage();
            playerInputMessage.mouseX = mouseSensitivity * Input.GetAxisRaw("Mouse X");
            playerInputMessage.mouseY = mouseSensitivity * Input.GetAxisRaw("Mouse Y");
            playerInputMessage.w = Input.GetKey(KeyCode.W) ? 1 : 0;
            playerInputMessage.a = Input.GetKey(KeyCode.A) ? 1 : 0;
            playerInputMessage.s = Input.GetKey(KeyCode.S) ? 1 : 0;
            playerInputMessage.d = Input.GetKey(KeyCode.D) ? 1 : 0;

            SendToServer(ByteSerializer.GetBytes(playerInputMessage), Facepunch.Steamworks.Networking.SendType.Unreliable);

            yield return new WaitForSeconds(1.0f / inputsPerSec);
        }
    }

    protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
    {
        lastPlayerInputMessage = ByteSerializer.FromBytes<PlayerInputMessage>(data);
    }

    protected void SimulateMovement (Transform target, float mouseX, float mouseY, int w, int a, int s, int d)
    {
        // Rotate
        Vector3 toRotation = target.rotation.eulerAngles;
        toRotation.x -= mouseY;
        toRotation.y += mouseX;

        // Clamp rotation into possible values to avoid upside down: 0-90, 270-360
        if (toRotation.x > 90 && toRotation.x <= 180)
        {
            toRotation.x = 90;
        }
        else if (toRotation.x < 270 && toRotation.x > 180)
        {
            toRotation.x = 270;
        }

        // Assign new rotation
        target.rotation = Quaternion.Euler(toRotation);

        // Move
        float movementX = d - a;
        float movementZ = w - s;

        target.position += movementX * Time.deltaTime * movementSpeed * new Vector3(target.right.x, 0, target.right.z).normalized;
        target.position += movementZ * Time.deltaTime * movementSpeed * new Vector3(target.forward.x, 0, target.forward.z).normalized;
    }

    public void StartPlayerInputLoop ()
    {
        // Lock and hide mouse
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Make sure that the player camera position and player position are in sync
        playerCamera = Camera.main;
        playerCamera.transform.position = transform.position;
        playerCamera.transform.rotation = transform.rotation;

        StartCoroutine(PlayerInputLoop());
    }
}
