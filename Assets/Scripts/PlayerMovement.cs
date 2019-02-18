using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

[RequireComponent(typeof(Player))]
public class PlayerMovement : NetworkBehaviour
{
    public float inputsPerSec = 60;
    public float mouseSensitivity = 1;
    public float movementSpeed = 10;

    protected Player player;
    protected Vector3 desync;
    protected Camera playerCamera;
    protected int playerTransformID = 0;
    protected PlayerInputMessage playerInputMessage;
    protected Dictionary<int, PlayerTransform> lastPlayerTransforms;

    protected struct PlayerInputMessage
    {
        public int id;
        public float mouseX;
        public float mouseY;
        public float w;
        public float a;
        public float s;
        public float d;

        public PlayerInputMessage (int id, float mouseX, float mouseY, float w, float a, float s, float d)
        {
            this.id = id;
            this.mouseX = mouseX;
            this.mouseY = mouseY;
            this.w = w;
            this.a = a;
            this.s = s;
            this.d = d;
        }
    };

    protected struct PlayerTransform
    {
        public int id;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    };

    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
        lastPlayerTransforms = new Dictionary<int, PlayerTransform>();
    }

    protected override void UpdateClient()
    {
        if (player.isControlling && !player.isDead)
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
        }
    }

    protected IEnumerator PlayerInputLoop ()
    {
        while (player.isControlling && !player.isDead)
        {
            SendToServer(ByteSerializer.GetBytes(playerInputMessage), SendType.Unreliable);

            // Save the last local player transform in order to compare it to the servers later
            PlayerTransform playerTransform = new PlayerTransform();
            playerTransform.id = playerTransformID;
            playerTransform.localPosition = playerCamera.transform.localPosition;
            playerTransform.localRotation = playerCamera.transform.localRotation;
            playerTransform.localScale = playerCamera.transform.localScale;

            lastPlayerTransforms[playerTransformID] = playerTransform;

            // Reset accumulated input
            playerTransformID++;
            playerInputMessage = new PlayerInputMessage(playerTransformID, 0, 0, 0, 0, 0, 0);

            yield return new WaitForSeconds(1.0f / inputsPerSec);
        }
    }

    protected override void OnServerReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // There is no gaurantee at all that the client message is valid
        // TODO: In order to make sure that the player cannot cheat:
        // - Check that this is a valid time to receive a message (e.g. message counter)
        // - Make sure that the WASD input times are lower equal to the interval time of the input rate
        PlayerInputMessage m = ByteSerializer.FromBytes<PlayerInputMessage>(data);

        // Do the same movement as the client already did
        SimulateMovement(transform, m.mouseX, m.mouseY, m.w, m.a, m.s, m.d);

        // Send the result with the same id back to the client in order to compare and correct it
        PlayerTransform playerTransform = new PlayerTransform();
        playerTransform.id = m.id;
        playerTransform.localPosition = transform.localPosition;
        playerTransform.localRotation = transform.localRotation;
        playerTransform.localScale = transform.localScale;

        SendToClient(steamID, ByteSerializer.GetBytes(playerTransform), SendType.Unreliable);
    }

    protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
    {
        // Get the result of the server simulation
        PlayerTransform playerTransform = ByteSerializer.FromBytes<PlayerTransform>(data);

        // Find the matching player transform from the past, calculate the error and correct it
        if (lastPlayerTransforms.TryGetValue(playerTransform.id, out PlayerTransform playerTransformMatch))
        {
            // The values should be the same if the ping, client and server times would add up perfectly and would be deterministic
            Vector3 positionError = playerTransform.localPosition - playerTransformMatch.localPosition;
            Quaternion rotationError = Quaternion.Inverse(playerTransformMatch.localRotation) * playerTransform.localRotation;
            Vector3 scaleError = playerTransform.localScale - playerTransformMatch.localScale;

            // Save the desync in order to display it
            desync.x = positionError.magnitude;
            desync.y = Quaternion.Angle(playerTransformMatch.localRotation, playerTransform.localRotation);
            desync.z = scaleError.magnitude;

            // Correct error from the past in the present as an offset
            playerCamera.transform.localPosition += positionError;
            playerCamera.transform.localRotation *= rotationError;
            playerCamera.transform.localScale += scaleError;

            // Any other corrections will be wrong after this one because we already corrected a bit
            lastPlayerTransforms.Clear();
        }
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

        // Disable the rendering of the own player
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach(Renderer r in renderers)
        {
            r.enabled = false;
        }

        StartCoroutine(PlayerInputLoop());
    }

    protected void OnGUI()
    {
        if (player.isControlling)
        {
            GUI.color = Color.black;
            string label = "Position:\t" + desync.x.ToString("0.0000") + "\nRotation:\t" + desync.y.ToString("0.0000") + "\nScale:\t" + desync.z.ToString("0.0000");
            GUI.Label(new Rect(Screen.width - Screen.width / 5, Screen.height - Screen.height / 5, Screen.width / 5, Screen.height / 5), "Desync\n" + label);
        }
    }
}
