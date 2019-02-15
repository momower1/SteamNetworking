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
    protected PlayerInputMessage playerInputMessage;

    protected struct PlayerInputMessage
    {
        public float mouseX;
        public float mouseY;
        public float w;
        public float a;
        public float s;
        public float d;

        public PlayerInputMessage (float mouseX, float mouseY, float w, float a, float s, float d)
        {
            this.mouseX = mouseX;
            this.mouseY = mouseY;
            this.w = w;
            this.a = a;
            this.s = s;
            this.d = d;
        }
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
            float dt = Time.deltaTime;
            float mouseX = mouseSensitivity * Input.GetAxisRaw("Mouse X");
            float mouseY = mouseSensitivity * Input.GetAxisRaw("Mouse Y");
            float w = Input.GetKey(KeyCode.W) ? dt : 0;
            float a = Input.GetKey(KeyCode.A) ? dt : 0;
            float s = Input.GetKey(KeyCode.S) ? dt : 0;
            float d = Input.GetKey(KeyCode.D) ? dt : 0;

            // Accumulate input
            playerInputMessage.mouseX += mouseX;
            playerInputMessage.mouseY += mouseY;
            playerInputMessage.w += w;
            playerInputMessage.a += a;
            playerInputMessage.s += s;
            playerInputMessage.d += d;

            // Simulate camera movement locally
            SimulateMovement(playerCamera.transform, mouseX, mouseY, w, a, s, d);

            // Log the actual error between the client camera and the server player
            Vector3 positionError = playerCamera.transform.localPosition - transform.localPosition;
            Vector3 rotationError = playerCamera.transform.localRotation.eulerAngles - transform.localRotation.eulerAngles;
            Debug.Log("Position Error: " + positionError + " Rotation Error: " + rotationError);
        }
    }

    protected IEnumerator PlayerInputLoop ()
    {
        while (true)
        {
            SendToServer(ByteSerializer.GetBytes(playerInputMessage), SendType.Unreliable);

            // Reset accumulated input
            playerInputMessage = new PlayerInputMessage(0, 0, 0, 0, 0, 0);

            yield return new WaitForSeconds(1.0f / inputsPerSec);
        }
    }

    protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // There is no gaurantee at all that the client message is valid
        // In order to make sure that the player cannot cheat:
        // - Check that this is a valid time to receive a message (e.g. message counter)
        // - Make sure that the WASD input times are lower equal to the interval time of the input rate
        PlayerInputMessage m = ByteSerializer.FromBytes<PlayerInputMessage>(data);

        // Do the same movement as the client already did
        SimulateMovement(transform, m.mouseX, m.mouseY, m.w, m.a, m.s, m.d);
    }

    protected void SimulateMovement (Transform target, float mouseX, float mouseY, float w, float a, float s, float d)
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

        target.position += movementX * movementSpeed * new Vector3(target.right.x, 0, target.right.z).normalized;
        target.position += movementZ * movementSpeed * new Vector3(target.forward.x, 0, target.forward.z).normalized;
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
