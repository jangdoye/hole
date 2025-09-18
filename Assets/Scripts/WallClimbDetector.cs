using UnityEngine;

public class WallClimbDetector : MonoBehaviour
{
    public PlayerController player;          // �÷��̾� ��Ʈ�ѷ�
    public LayerMask climbableMask;          // Climbable ���̾�
    [Header("Detection")]
    public float detectDistance = 0.9f;      // ���� ���� �Ÿ�(0.7~1.0)
    public float stickDistance = 0.30f;     // ���� ������ ���� ( radius+0.02)
    public float maxAlignAngle = 80f;       // ���� ���� ��� ����
    public bool requireForwardInput = true;  // W �Է� ��� �ٴ��� �׽�Ʈ��

    [Header("Tuning")]
    public float alignLerp = 15f;            // �� �ٶ󺸴� ���� �ӵ�
    public float extraPush = 0.01f;          // �߰� ���� ����

    CharacterController cc;

    void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        cc = GetComponent<CharacterController>();
        // stickDistance�� �ڵ� ���� (ó�� 1ȸ)
        if (stickDistance < cc.radius) stickDistance = cc.radius + 0.02f;
    }

    void Update()
    {
        // Ŭ���� ���̸� ���� ���� or ����
        if (player.isClimbing)
        {
            if (IsWallInFront(out RaycastHit hit))
                GlueToWall(hit);
            else
                player.isClimbing = false; // �� ����
            return;
        }

        // ����: ���濡 ���� �ְ�, (�ɼ�) W �Է��̸� Ŭ���� ����
        if (IsWallInFront(out RaycastHit hitFront))
        {
            float angle = Vector3.Angle(-hitFront.normal, transform.forward);
            bool forwardInput = Input.GetAxisRaw("Vertical") > 0.1f;
            if (angle <= maxAlignAngle && (!requireForwardInput || forwardInput))
            {
                player.isClimbing = true;
                GlueToWall(hitFront); // ���� ��� ����
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

    // �ٽ�: Move�� ���� ���� + ���� ����
    void GlueToWall(in RaycastHit hit)
    {
        // ���� �Ÿ��� ��ǥ �Ÿ��� ���̸� �� ���� �������� ����
        float need = hit.distance - stickDistance; // ���=�ִ�
        if (need > -0.0005f)
        {
            Vector3 push = -hit.normal * (need + extraPush);
            cc.Move(push); // Position ��� Move�� �� ������
        }

        // ���� �ٶ󺸰� ���� (Pitch/Roll ����)
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
