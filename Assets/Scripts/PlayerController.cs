using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Camera (1st Person)")]
    public Camera playerCamera;
    public float mouseSensitivity = 120f;
    public float pitchClamp = 85f;

    [Header("Movement")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float jumpHeight = 2.0f;     // meters
    public float gravity = -9.81f;

    [Header("Climb")]
    public bool isClimbing = false;     // Detector가 on/off
    public float climbSpeed = 2.0f;     // up/down speed while climbing

    [Header("Ground Check")]
    public Transform groundCheck;       // 발밑
    public float groundRadius = 0.28f;
    public LayerMask groundMask = ~0;

    [Header("Jump Assist")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Input")]
    public KeyCode runKey = KeyCode.LeftShift;
    public string jumpAxis = "Jump";

    [Header("View Stabilize")]
    public bool stabilizeYawPitch = true;

    CharacterController controller;
    Animator anim;
    Vector3 velocity;
    float yaw, pitch;
    Vector2 moveInput;
    const float DEADZONE = 0.05f;

    float lastGroundedTime;
    float lastJumpPressedTime;

    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashIsFalling = Animator.StringToHash("isFalling");
    static readonly int HashIsClimbing = Animator.StringToHash("isClimbing");
    static readonly int HashJump = Animator.StringToHash("Jump");

    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
        if (playerCamera) pitch = playerCamera.transform.localEulerAngles.x;
        // Animator → Apply Root Motion = OFF 권장
    }

    void Update()
    {
        HandleLook();
        ReadInputs();
        HandleMoveJumpClimb();
        HandleAnimator();
        if (stabilizeYawPitch) StabilizeRotationsLate();
    }

    void HandleLook()
    {
        if (!playerCamera) return;
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void ReadInputs()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 raw = new Vector2(h, v);
        if (raw.magnitude < DEADZONE) raw = Vector2.zero;
        moveInput = Vector2.ClampMagnitude(raw, 1f);

        if (Input.GetButtonDown(jumpAxis))
            lastJumpPressedTime = Time.time;
    }

    void HandleMoveJumpClimb()
    {
        bool groundedNow = IsGrounded();
        if (groundedNow) lastGroundedTime = Time.time;

        if (groundedNow && velocity.y < 0f) velocity.y = -2f;

        // ── CLIMBING ─────────────────────────────────────────
        if (isClimbing)
        {
            // 위/아래 + 좌우 벽타기
            float climb = Input.GetAxisRaw("Vertical");
            controller.Move(Vector3.up * climb * climbSpeed * Time.deltaTime);

            float strafe = Input.GetAxisRaw("Horizontal");
            controller.Move(transform.right * strafe * (climbSpeed * 0.7f) * Time.deltaTime);

            velocity.y = 0f;   // 중력 무시
            return;            // ← 수평 이동/중력 로직은 실행하지 않음
        }
        // ─────────────────────────────────────────────────────

        // 평상시 이동
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;
        Vector3 moveDir = (right * moveInput.x + forward * moveInput.y).normalized;
        bool running = Input.GetKey(runKey);
        float targetSpeed = running ? runSpeed : walkSpeed;
        controller.Move(moveDir * targetSpeed * Time.deltaTime);

        // 점프 (코요테/버퍼)
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool jumpQueued = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (jumpQueued && canCoyote)
        {
            lastJumpPressedTime = -999f;
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            anim.SetTrigger(HashJump);
        }

        // 중력
        velocity.y += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }

    void HandleAnimator()
    {
        bool running = Input.GetKey(runKey);
        float speed01 = moveInput.magnitude * (running ? 1f : 0.5f); // Idle=0, Walk≈0.5, Run=1
        anim.SetFloat(HashSpeed, speed01, 0.1f, Time.deltaTime);

        bool falling = !IsGrounded() && velocity.y < -0.1f && !isClimbing;
        anim.SetBool(HashIsFalling, falling);
        anim.SetBool(HashIsClimbing, isClimbing);
    }

    void StabilizeRotationsLate()
    {
        var e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        if (playerCamera)
        {
            var ce = playerCamera.transform.localEulerAngles;
            playerCamera.transform.localRotation = Quaternion.Euler(ce.x, 0f, 0f);
        }
    }

    bool IsGrounded()
    {
        Vector3 origin = groundCheck
            ? groundCheck.position
            : (transform.position + Vector3.down * (controller.height * 0.5f - controller.radius + 0.02f));
        bool sphere = Physics.CheckSphere(origin, groundRadius, groundMask, QueryTriggerInteraction.Ignore);
        return sphere || controller.isGrounded;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ClimbZone")) isClimbing = true;
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ClimbZone")) isClimbing = false;
    }
}
