using UnityEngine;
using System.Collections.Generic;
using System.IO;

[ExecuteAlways]
public class MorphStateController : MonoBehaviour
{
    [Range(1, 5000)]
    public int currentCSVFrame = 1;
    public bool useCSVPlayback = true;
    public bool followGlobalManager = false;

    [Header("Manual Morph Control")]
    [Range(0f, 3f)]
    [Tooltip("0-1: Tri->Hex (44-113), 1-2: Hex->Circle (181-225), 2-3: Circle->Square (231-249)")]
    public float morphState = 0f;

    private Animator animator;
    private const float FPS = 24f;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;

        if (useCSVPlayback)
        {
            if (followGlobalManager)
            {
                MorphDataManager manager = Object.FindAnyObjectByType<MorphDataManager>();
                if (manager != null)
                {
                    currentCSVFrame = manager.currentFrame;
                }
            }
            SyncMorphStateFromFrame(currentCSVFrame);
        }

        // Drive the animator by normalized time based on currentCSVFrame
        if (animator.runtimeAnimatorController != null)
        {
            float clipLength = animator.runtimeAnimatorController.animationClips[0].length;
            if (clipLength > 0)
            {
                float timeInSeconds = (currentCSVFrame - 1f) / FPS;
                float normalizedTime = Mathf.Clamp01(timeInSeconds / clipLength);
                animator.Play(0, 0, normalizedTime);
                animator.speed = 0;
                animator.Update(0);
            }
        }
    }

    private void SyncMorphStateFromFrame(int frame)
    {
        // Schedule:
        // State 1: 1-43 (Triangle)
        // State 2: 44-180 (Hexagon)
        // State 3: 181-230 (Circle)
        // State 4: 231-250 (Square)
        
        if (frame <= 43) morphState = 0f;
        else if (frame <= 113) morphState = (frame - 43f) / (113f - 43f);
        else if (frame < 181) morphState = 1.0f;
        else if (frame <= 225) morphState = 1.0f + (frame - 181f) / (225f - 181f);
        else if (frame < 231) morphState = 2.0f;
        else if (frame <= 249) morphState = 2.0f + (frame - 231f) / (249f - 231f);
        else morphState = 3.0f;
    }
}
