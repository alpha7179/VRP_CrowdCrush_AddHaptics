using UnityEngine;
using System.Collections;

public class WatchOutCrowdAnim : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Animator 안에 등록한 State 이름들")]
    public string[] clipNames =
        {"Breathing Idle_Anim",
        "Scary Clown Idle_Anim",
        "Look Around_Anim",
        "Nervously Look Around_Anim",
        "Scary Clown Idle_Anim",
        "BackReaction_Anim"

    };

    private int lastIndex = -1;

    void Start()
    {
        if (!animator) animator = GetComponent<Animator>();

        animator.speed = Random.Range(0.7f, 1.2f);
        PlayRandomAnim();
        StartCoroutine(ChangeAnimOccasionally());
    }

    void PlayRandomAnim()
    {
        int newIndex = GetNextClipIndex();
        lastIndex = newIndex;

        string clip = clipNames[newIndex];
        Debug.Log($"{name} Play: {clip}"); // 어떤 애니가 선택됐는지 로그
        animator.CrossFadeInFixedTime(clip, 0.25f, 0, Random.value);
    }

    // 직전과 같은 index 방지
    int GetNextClipIndex()
    {
        if (clipNames.Length <= 1) return 0;

        int index;
        do
        {
            index = Random.Range(0, clipNames.Length);
        }
        while (index == lastIndex);

        return index;
    }

    IEnumerator ChangeAnimOccasionally()
    {
        while (true)
        {
            // 전환 주기 짧게 조절
            float wait = Random.Range(3f, 7f);
            yield return new WaitForSeconds(wait);

            PlayRandomAnim();
        }
    }
}