using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

public class PlayerInput : NetworkBehaviour
{
    [SerializeField]
    protected float inputsPerSec = 60;
    [SerializeField]
    protected float mouseSensitivity = 1;
    [SerializeField]
    protected float movementSpeed = 10;
    [SerializeField]
    protected GameObject projectilePrefab;

    private float accumulatedMouseX = 0;
    private float accumulatedMouseY = 0;
    private int accumulatedMouse0 = 0;

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

    protected override void UpdateClient()
    {
        accumulatedMouseX += mouseSensitivity * Input.GetAxisRaw("Mouse X");
        accumulatedMouseY += mouseSensitivity * Input.GetAxisRaw("Mouse Y");

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            accumulatedMouse0++;
        }
    }

    protected IEnumerator PlayerInputLoop ()
    {
        while (true)
        {
            PlayerInputMessage playerInputMessage = new PlayerInputMessage();
            playerInputMessage.mouseX = accumulatedMouseX;
            playerInputMessage.mouseY = accumulatedMouseY;
            playerInputMessage.mouse0 = accumulatedMouse0;
            playerInputMessage.w = Input.GetKey(KeyCode.W) ? 1 : 0;
            playerInputMessage.a = Input.GetKey(KeyCode.A) ? 1 : 0;
            playerInputMessage.s = Input.GetKey(KeyCode.S) ? 1 : 0;
            playerInputMessage.d = Input.GetKey(KeyCode.D) ? 1 : 0;

            accumulatedMouseX = 0;
            accumulatedMouseY = 0;
            accumulatedMouse0 = 0;

            SendToServer(ByteSerializer.GetBytes(playerInputMessage), Facepunch.Steamworks.Networking.SendType.Unreliable);

            yield return new WaitForSeconds(1.0f / inputsPerSec);
        }
    }

    protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
    {
        PlayerInputMessage playerInputMessage = ByteSerializer.FromBytes<PlayerInputMessage>(data);

        // Rotate player
        Vector3 toRotation = transform.rotation.eulerAngles;
        toRotation.x -= playerInputMessage.mouseY;
        toRotation.y += playerInputMessage.mouseX;

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
        transform.rotation = Quaternion.Euler(toRotation);

        // Move player
        float movementX = playerInputMessage.d - playerInputMessage.a;
        float movementZ = playerInputMessage.w - playerInputMessage.s;

        transform.position += movementX * Time.deltaTime * movementSpeed * new Vector3(transform.right.x, 0, transform.right.z).normalized;
        transform.position += movementZ * Time.deltaTime * movementSpeed * new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        // Spawn projectiles
        for (int i = 0; i < playerInputMessage.mouse0; i++)
        {
            GameServer.Instance.InstantiateInScene(projectilePrefab, transform.position, transform.rotation, null);
        }
    }

    public void StartPlayerInputLoop ()
    {
        // TODO: Remove
        networkObject.interpolateOnClient = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(PlayerInputLoop());
    }

    protected void OnGUI()
    {
        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(Screen.width / 2, Screen.height / 2, Screen.height / 100, Screen.height / 100), Texture2D.whiteTexture);
    }
}
