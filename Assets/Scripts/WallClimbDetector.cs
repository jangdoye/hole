using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class WallClimbDetector : MonoBehaviour
{
    public PlayerController player;
    public LayerMask climbableMask;

    [Header("Detection")]
    public float detectDistance = 0.9f;   // 0.7~1.0
    public float stickDistance = 0.30f;  // ≈ controller.radius + 0.02
    public float maxAlignAngle = 80f;    // 허용 각도
    public bool requireForwardInput = true;

    [Header("Mantle Settings")]
    public float topCheckHeight = 1.4f; // 위 공간 체크 높이
    public float topCheckForward = 0.6f; // 벽 바깥 검사
    public float mantleUpOffset = 0.9f; // 위로 이동량
    public float mantleForwardOffset = 0.4f; // 앞으로 이동량
    public float mantleDuration = 0.6f;

    [Header("Tuning")]
    public float alignLerp = 15f;
    public float extraPush = 0.01f;

    CharacterController cc;

    void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        cc = GetComponent<CharacterController>();
        if (stickDistance < cc.radius) stickDistance = cc.radius + 0.02f;
    }

    void Update()
    {
        //  맨틀 중이면 아무 것도 하지 않음 (Move, 감지 모두 중단)
        if (player.isMantling) return;

        // 클라임 유지/해제
        if (player.isClimbing)
        {
            if (SphereHitFront(out RaycastHit hit))
            {
                GlueToWall(hit);
                player.ReportWallHit(hit.normal);

                // 점프키로 맨틀 시도
                if (Input.GetButtonDown(player.jumpAxis))
                {
                    if (TryMantle(hit)) return;
                }
            }
            else
            {
                player.isClimbing = false;
            }
            return;
        }

        // 평소: 벽 감지 + (옵션) W 입력 → 클라임 시작
        if (SphereHitFront(out RaycastHit hitFront))
        {
            float angle = Vector3.Angle(-hitFront.normal, transform.forward);
            bool forwardInput = Input.GetAxisRaw("Vertical") > 0.1f;
            if (angle <= maxAlignAngle && (!requireForwardInput || forwardInput))
            {
                player.isClimbing = true;
                GlueToWall(hitFront);
                player.ReportWallHit(hitFront.normal);
            }
        }
    }

    bool SphereHitFront(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * (cc.height * 0.5f);
        float sphereRadius = cc.radius * 0.9f;
        Vector3 dir = transform.forward;
        return Physics.SphereCast(origin, sphereRadius, dir, out hit, detectDistance, climbableMask, QueryTriggerInteraction.Ignore);
    }

    void GlueToWall(in RaycastHit hit)
    {
        if (player.isMantling || !cc.enabled) return; //  Move 가드

        float need = hit.distance - stickDistance;
        if (need > -0.0005f)
        {
            Vector3 push = -hit.normal * (need + extraPush);
            cc.Move(push);                               // 안전
        }

        Quaternion look = Quaternion.LookRotation(-hit.normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, alignLerp * Time.deltaTime);
    }

    // 벽 위 공간이 비었으면 맨틀 시작
    bool TryMantle(in RaycastHit frontHit)
    {
        // 벽 위가 뚫려있는지 확인
        Vector3 topOrigin = frontHit.point + Vector3.up * topCheckHeight - frontHit.normal * 0.05f;
        bool blocked = Physics.Raycast(topOrigin, -frontHit.normal, topCheckForward, climbableMask, QueryTriggerInteraction.Ignore);
        if (blocked) return false;

        // 이동 델타 계산하고 플레이어에 요청
        Vector3 delta = Vector3.up * mantleUpOffset + (-frontHit.normal) * mantleForwardOffset;
        player.StartMantle(delta, mantleDuration);
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * (cc ? cc.height * 0.5f : 1f);
        float sphereRadius = cc ? cc.radius * 0.9f : 0.2f;
        Gizmos.DrawWireSphere(origin, sphereRadius);
        Gizmos.DrawLine(origin, origin + transform.forward * detectDistance);
        Gizmos.DrawWireSphere(origin + transform.forward * stickDistance, 0.03f);
    }
#endif
}
