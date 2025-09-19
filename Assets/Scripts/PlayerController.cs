using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Camera (1st Person)")]
    public Camera playerCamera;
    public float mouseSensitivity = 120f;
    public float pitchClamp = 85f;

    [Header("Movement")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float jumpHeight = 2.0f;
    public float gravity = -9.81f;

    [Header("Climb")]
    public bool isClimbing = false;
    public float climbSpeed = 2.0f;
    [Tooltip("벽 감지가 잠깐 끊겨도 유지되는 시간")]
    public float detachGraceTime = 0.2f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.28f;
    public LayerMask groundMask = ~0;

    [Header("Jump Assist")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Input")]
    public KeyCode runKey = KeyCode.LeftShift;
    public string jumpAxis = "Jump";

    [Header("View")]
    public bool stabilizeYawPitch = true;

    // ── internal ──
    CharacterController controller;
    Animator anim;
    Vector3 velocity;
    float yaw, pitch;
    Vector2 moveInput;
    const float DEADZONE = 0.05f;

    float lastGroundedTime;
    float lastJumpPressedTime;

    // climb grace / facing
    float lastWallSeenTime = -999f;
    Vector3 lastWallNormal = Vector3.forward;

    // Animator hashes
    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashIsFalling = Animator.StringToHash("isFalling");
    static readonly int HashIsClimbing = Animator.StringToHash("isClimbing");
    static readonly int HashJump = Animator.StringToHash("Jump");
    static readonly int HashClimbPlayRate = Animator.StringToHash("ClimbPlayRate");
    static readonly int HashMantle = Animator.StringToHash("Mantle");

    float climbPlayRate = 1f;

    public bool isMantling { get; private set; } = false;
    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
        if (playerCamera) pitch = playerCamera.transform.localEulerAngles.x;
        // Animator.ApplyRootMotion = OFF 권장
    }

    void Update()
    {
        HandleLook();
        ReadInputs();

        // 맨틀 중엔 아무 이동/중력도 처리하지 않음
        if (!isMantling)
            HandleMoveJumpClimb();

        if (!isMantling)
            HandleAnimator();

        if (stabilizeYawPitch) StabilizeRotationsLate();
    }

    // ── Mantle: 외부(Detector)나 내부에서 호출 ──
    public void StartMantle(Vector3 delta, float duration = 0.6f)
    {
        if (isMantling) return;

        anim.ResetTrigger(HashJump);
        anim.SetTrigger(HashMantle);

        isClimbing = false;
        isMantling = true;                 //  맨틀 중 표시

        StopAllCoroutines();
        StartCoroutine(MantleMove(delta, duration));
    }

    IEnumerator MantleMove(Vector3 delta, float dur)
    {
        // 컨트롤러 끄기 전에 살짝 위로 올려서 바닥 클리핑 방지(선택)
        // transform.position += Vector3.up * 0.01f;

        controller.enabled = false;        // Move 호출 금지 상태
        Vector3 start = transform.position;
        Vector3 target = start + delta;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.position = Vector3.Lerp(start, target, u);
            yield return null;
        }
        controller.enabled = true;         // 다시 켜기
        isMantling = false;                // 맨틀 종료
    }

    // Detector에서 매 프레임 벽 법선 보고
    public void ReportWallHit(Vector3 wallNormal)
    {
        lastWallNormal = wallNormal;
        lastWallSeenTime = Time.time;
    }

    bool IsClimbingOrGrace() =>
        isClimbing || (Time.time - lastWallSeenTime) <= detachGraceTime;

    void HandleLook()
    {
        if (!playerCamera) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        if (IsClimbingOrGrace())
        {
            // 클라임 중엔 yaw 고정, pitch만
            pitch -= my;
            pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            yaw += mx;
            pitch -= my;
            pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
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

        // ── CLIMBING(or grace) ──
        if (IsClimbingOrGrace())
        {
            float v = Input.GetAxisRaw("Vertical");
            float h = Input.GetAxisRaw("Horizontal");
            const float DZ = 0.05f;
            if (Mathf.Abs(v) < DZ) v = 0f;
            if (Mathf.Abs(h) < DZ) h = 0f;

            // 실제 이동
            Vector3 climbMove =
                Vector3.up * v * climbSpeed +
                transform.right * h * (climbSpeed * 0.7f);
            controller.Move(climbMove * Time.deltaTime);
            velocity.y = 0f;

            // 입력 없으면 클라임 애니 정지
            float targetRate = (Mathf.Abs(v) + Mathf.Abs(h)) > 0 ? 1f : 0f;
            climbPlayRate = Mathf.MoveTowards(climbPlayRate, targetRate, 6f * Time.deltaTime);
            anim.SetFloat(HashClimbPlayRate, climbPlayRate);

            // 벽 정면으로 yaw 유지
            if (lastWallNormal.sqrMagnitude > 0.001f)
            {
                Vector3 lookFwd = -lastWallNormal; lookFwd.y = 0f;
                if (lookFwd.sqrMagnitude > 0.0001f)
                    yaw = Quaternion.LookRotation(lookFwd, Vector3.up).eulerAngles.y;
            }

            return; // 일반 이동/중력 스킵
        }

        // 여기 오면 클라임/그레이스 아님
        if (isClimbing) isClimbing = false;
        if (anim.GetFloat(HashClimbPlayRate) < 0.99f)
        {
            climbPlayRate = 1f;
            anim.SetFloat(HashClimbPlayRate, 1f);
        }

        // 평상시 이동
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;
        Vector3 moveDir = (right * moveInput.x + forward * moveInput.y).normalized;
        bool running = Input.GetKey(runKey);
        float targetSpeed = running ? runSpeed : walkSpeed;
        controller.Move(moveDir * targetSpeed * Time.deltaTime);

        // 점프
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool jumpQueued = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (jumpQueued && canCoyote)
        {
            lastJumpPressedTime = -999f;
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isClimbing = false;
            anim.ResetTrigger(HashJump);
            anim.SetTrigger(HashJump);
        }

        // 중력
        velocity.y += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }

    void HandleAnimator()
    {
        bool running = Input.GetKey(runKey);
        float speed01 = moveInput.magnitude * (running ? 1f : 0.5f);
        anim.SetFloat(HashSpeed, speed01, 0.1f, Time.deltaTime);

        bool falling = !IsGrounded() && velocity.y < -0.1f && !IsClimbingOrGrace();
        anim.SetBool(HashIsFalling, falling);
        anim.SetBool(HashIsClimbing, IsClimbingOrGrace());
    }

    void StabilizeRotationsLate()
    {
        if (IsClimbingOrGrace() && lastWallNormal.sqrMagnitude > 0.001f)
        {
            Vector3 fwd = -lastWallNormal; fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y, 0f);
        }
        else
        {
            var e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        }

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
