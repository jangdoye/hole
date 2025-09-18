using UnityEngine;

public class WallClimbDetector : MonoBehaviour
{
    public PlayerController player;          // 플레이어 컨트롤러
    public LayerMask climbableMask;          // Climbable 레이어
    [Header("Detection")]
    public float detectDistance = 0.9f;      // 전방 감지 거리(0.7~1.0)
    public float stickDistance = 0.30f;     // 벽과 유지할 간격 ( radius+0.02)
    public float maxAlignAngle = 80f;       // 정면 기준 허용 각도
    public bool requireForwardInput = true;  // W 입력 없어도 붙는지 테스트용

    [Header("Tuning")]
    public float alignLerp = 15f;            // 벽 바라보는 보간 속도
    public float extraPush = 0.01f;          // 추가 밀착 여유

    CharacterController cc;

    void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        cc = GetComponent<CharacterController>();
        // stickDistance를 자동 보정 (처음 1회)
        if (stickDistance < cc.radius) stickDistance = cc.radius + 0.02f;
    }

    void Update()
    {
        // 클라임 중이면 접착 유지 or 해제
        if (player.isClimbing)
        {
            if (IsWallInFront(out RaycastHit hit))
                GlueToWall(hit);
            else
                player.isClimbing = false; // 벽 잃음
            return;
        }

        // 평상시: 전방에 벽이 있고, (옵션) W 입력이면 클라임 시작
        if (IsWallInFront(out RaycastHit hitFront))
        {
            float angle = Vector3.Angle(-hitFront.normal, transform.forward);
            bool forwardInput = Input.GetAxisRaw("Vertical") > 0.1f;
            if (angle <= maxAlignAngle && (!requireForwardInput || forwardInput))
            {
                player.isClimbing = true;
                GlueToWall(hitFront); // 진입 즉시 밀착
            }
        }
    }

    bool IsWallInFront(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * (cc.height * 0.5f);
        Vector3 dir = transform.forward;
        bool ok = Physics.Raycast(origin, dir, out hit, detectDistance, climbableMask, QueryTriggerInteraction.Ignore);
        return ok;
    }

    // 핵심: Move로 벽에 밀착 + 정면 정렬
    void GlueToWall(in RaycastHit hit)
    {
        // 현재 거리와 목표 거리의 차이를 벽 법선 방향으로 보정
        float need = hit.distance - stickDistance; // 양수=멀다
        if (need > -0.0005f)
        {
            Vector3 push = -hit.normal * (need + extraPush);
            cc.Move(push); // Position 대신 Move를 매 프레임
        }

        // 벽을 바라보게 정렬 (Pitch/Roll 제거)
        Quaternion look = Quaternion.LookRotation(-hit.normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, alignLerp * Time.deltaTime);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * (cc ? cc.height * 0.5f : 1f);
        Gizmos.DrawLine(origin, origin + transform.forward * detectDistance);
        Gizmos.DrawWireSphere(origin + transform.forward * stickDistance, 0.025f);
    }
#endif
}
