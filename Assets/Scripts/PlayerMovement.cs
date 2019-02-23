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
    protected LinkedList<PlayerInputMessage> lastPlayerInputs;

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
        lastPlayerInputs = new LinkedList<PlayerInputMessage>();
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

            // Save input in order to replay it later
            lastPlayerInputs.AddLast(playerInputMessage);

            // Reset accumulated input
            playerTransformID++;
            playerInputMessage = new PlayerInputMessage(playerTransformID, 0, 0, 0, 0, 0, 0);

            yield return new WaitForSecondsRealtime(1.0f / inputsPerSec);
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

        // Send the result with the same id back to the client in order to replay the input from there
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

        // Remove all the inputs that are no longer needed
        while (lastPlayerInputs.First != null && lastPlayerInputs.First.Value.id <= playerTransform.id)
        {
            lastPlayerInputs.RemoveFirst();
        }

        // Save current transform in oder to display the desync later
        Vector3 positionBeforeCorrection = playerCamera.transform.localPosition;
        Quaternion rotationBeforeCorrection = playerCamera.transform.localRotation;
        Vector3 scaleBeforeCorrection = playerCamera.transform.localScale;

        // Reset transform to the one from the server
        playerCamera.transform.localPosition = playerTransform.localPosition;
        playerCamera.transform.localRotation = playerTransform.localRotation;
        playerCamera.transform.localScale = playerTransform.localScale;

        // Resimulate all the inputs since that state
        foreach (PlayerInputMessage input in lastPlayerInputs)
        {
            SimulateMovement(playerCamera.transform, input.mouseX, input.mouseY, input.w, input.a, input.s, input.d);
        }

        SimulateMovement(playerCamera.transform, playerInputMessage.mouseX, playerInputMessage.mouseY, playerInputMessage.w, playerInputMessage.a, playerInputMessage.s, playerInputMessage.d);

        // Compute the desync in order to display it
        desync.x = Vector3.Distance(playerCamera.transform.localPosition, positionBeforeCorrection);
        desync.y = Quaternion.Angle(playerCamera.transform.localRotation, rotationBeforeCorrection);
        desync.z = Vector3.Distance(playerCamera.transform.localScale, scaleBeforeCorrection);
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

        // Movement direction from input
        float movementRight = d - a;
        float movementForward = w - s;

        Vector3 right = new Vector3(target.right.x, 0, target.right.z).normalized;
        Vector3 forward = new Vector3(target.forward.x, 0, target.forward.z).normalized;
        Vector3 movementDirection = (movementRight * right + movementForward * forward).normalized;

        // Make sure that pressing e.g. W and D does not move the player faster than when only pressing W
        Vector3 movement = movementSpeed * movementRight * Mathf.Abs(Vector3.Dot(right, movementDirection)) * right;
        movement += movementSpeed * movementForward * Mathf.Abs(Vector3.Dot(forward, movementDirection)) * forward;

        RaycastHit raycastHit;
        const float maxStepSize = 1.0f;
        int collisionLayerMask = networkObject.onServer ? LayerMask.GetMask("Server/Default") : LayerMask.GetMask("Client/Default");

        // Raycast the floor to set the y-position correctly, only move player if there is a floor
        if (Physics.Raycast(target.position + movement + (1.5f - maxStepSize) * Vector3.down, Vector3.down, out raycastHit, 2 * maxStepSize, collisionLayerMask))
        {
            movement += raycastHit.point - (target.position + movement + 1.5f * Vector3.down);

            // Do deterministic collision detection by casting where the player would move and only moving as far as there is no penetration
            if (Physics.SphereCast(target.position, 0.5f, movementDirection, out raycastHit, movement.magnitude, collisionLayerMask))
            {
                Debug.DrawLine(target.position, target.position + (1 + movement.magnitude) * movementDirection, Color.red, Time.deltaTime);
                movement = Mathf.Max(0, raycastHit.distance - 0.1f) * movement.normalized;
            }

            // Move based on the collision result
            target.position += movement;
        }
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
