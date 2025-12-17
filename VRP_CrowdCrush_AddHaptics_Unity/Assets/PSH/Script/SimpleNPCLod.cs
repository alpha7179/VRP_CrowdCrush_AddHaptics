using UnityEngine;

public class SimpleNPCLod : MonoBehaviour
{
    public Transform player;
    public float fullAnimDistance = 6f;   // 이 안에서는 정상
    public float onlyRendererDistance = 12f; // 이 밖이면 렌더만 / 완전 끄기 등

    Animator anim;
    SkinnedMeshRenderer[] skins;

    void Awake()
    {
        anim = GetComponent<Animator>();
        skins = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (player == null && Camera.main != null)
            player = Camera.main.transform;
    }

    void Update()
    {
        if (player == null) return;

        float d = Vector3.Distance(player.position, transform.position);

        if (d < fullAnimDistance)
        {
            if (!anim.enabled) anim.enabled = true;
            foreach (var s in skins) if (!s.enabled) s.enabled = true;
        }
        else if (d > onlyRendererDistance)
        {
            // 아주 멀면 통째로 꺼버려도 OK
            if (anim.enabled) anim.enabled = false;
            foreach (var s in skins) if (s.enabled) s.enabled = false;
        }
        else
        {
            // 중간 거리: 애니메이션만 끔 (포즈 고정)
            if (anim.enabled) anim.enabled = false;
            foreach (var s in skins) if (!s.enabled) s.enabled = true;
        }
    }
}
