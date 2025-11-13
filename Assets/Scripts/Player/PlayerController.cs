using Photon.Pun;
using Photon.Realtime;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviourPun
{
    public static event Action<GameObject, Player> OnSpawn;
    public static event Action<GameObject, Player> OnDeath;
    [SerializeField] GameObject cameraHolder;  // Reference to the camera holder GameObject.
    [SerializeField] float mouseSensitivity, sprintSpeed, walkSpeed, jumpForce, smoothTime;  // Player movement and camera control parameters.
    [SerializeField] Item[] items;

    int itemIndex;
    int previousItemIndex = -1;
    
    // microman: Just a tip for future, don't leave comments like this. If something is confusing, explain it-
    // -but generally, variable names are be self-explanatory. That's why they have names.
    
    public Animator playerAnimator;  // Animator for player animations.
    float verticalLookRotation;  // Vertical camera rotation value.
    [SerializeField] bool grounded;  // Indicates if the player is grounded.
    Vector3 smoothMoveVelocity;  // Velocity used for smoothing player movement.
    Vector3 moveAmount;  // Total movement amount.
    Rigidbody rb;  // Player's Rigidbody component.
    PhotonView PV;  // PhotonView component for network synchronization.

    void Awake()
    {
        rb = GetComponent<Rigidbody>();   // Get the player's Rigidbody component.
        PV = GetComponent<PhotonView>();  // Get the PhotonView component for network synchronization.

        OnSpawn?.Invoke(gameObject, PV.Owner);
    }

    void Start()
    {
        if (PV.IsMine)
        {
            EquipItem(0); // Assign ra
        }
        else
        {
            // If this GameObject doesn't belong to the local player, destroy the camera and Rigidbody.
            Destroy(GetComponentInChildren<Camera>().gameObject);
            Destroy(rb);
        }
    }

    void Update()
    {
        if (!PV.IsMine)
            return;

        Look();  // Handle camera look/rotation.
        ProcessMovementInput();  // Handle player movement.
        Jump();  // Handle player jumping.

        for (int i = 0; i < items.Length; i++)
        {

            if (Input.GetKeyDown((i + 1).ToString()))
            {
                EquipItem(i);
                break;
            }
        }

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            items[itemIndex].Use();
        }
    }

    void Look()
    {
        // Horizontal rotation using Rigidbody
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        Quaternion newRot = Quaternion.Euler(0f, mouseX, 0f);
        rb.MoveRotation(rb.rotation * newRot);

        // Vertical rotation
        verticalLookRotation += Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -10f, 10f);

        cameraHolder.transform.localEulerAngles = Vector3.left * verticalLookRotation;
    }

    void Jump()
    {
        // Check for jumping input and if the player is grounded.
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            rb.AddForce(transform.up * jumpForce);  // Apply an upward force for jumping.
        }
    }

    void ProcessMovementInput()
    {
        // Read input for movement
        Vector3 moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;

        // Smooth the movement direction
        moveAmount = Vector3.SmoothDamp(
            moveAmount,
            moveDir * (Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed),
            ref smoothMoveVelocity,
            smoothTime
        );

        // Animation
        bool isWalking = moveDir.magnitude > 0;
        playerAnimator.SetBool("walk", isWalking);
    }

    void EquipItem(int _index)
    {

        if (_index == previousItemIndex)
            return;

        itemIndex = _index;
        items[itemIndex].itemGameObject.SetActive(true);

        if(previousItemIndex != -1)
        {
            items[previousItemIndex].itemGameObject.SetActive(false);
        }

        previousItemIndex = itemIndex;
    }

    public void SetGroundedState(bool _grounded)
    {
        grounded = _grounded;  // Set the grounded state based on collision detection.
    }

    void FixedUpdate()
    {
        // Check if this is the locally controlled player's object (IsMine).
        if (!PV.IsMine)
            return;

        // Move the Rigidbody's position based on the current player's input (moveAmount) and time step.
        rb.MovePosition(rb.position + transform.TransformDirection(moveAmount) * Time.fixedDeltaTime);
    }

    public PhotonView GetPhotonView ()
    {
        return photonView;
    }

    public void Die ()
    {
        PV.RPC(nameof(RPC_Die), RpcTarget.All);

        PhotonNetwork.Destroy(gameObject);
        //SceneManager.LoadScene("Menu"); // OBVIOUSLY NOT RIGHT
    }

    [PunRPC]
    void RPC_Die ()
    {
        OnDeath?.Invoke(gameObject, PV.Owner);
    }

    public Item[] GetItems ()
    {
        return items;
    }
}
