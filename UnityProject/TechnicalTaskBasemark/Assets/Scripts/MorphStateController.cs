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
    [Tooltip("0-1: Tri->Hex (43-113), 1-2: Hex->Sphere (181-225), 2-3: Sphere->Square (231-249)")]
    public float morphState = 0f;

    private Animator animator;
    private const float FPS = 24f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator != null) animator.speed = 0;
    }

    void Update()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        float clipLength = animator.runtimeAnimatorController.animationClips[0].length;
        if (clipLength <= 0) return;

        float targetFrame = 1f;

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

            targetFrame = currentCSVFrame;
            SyncMorphStateFromFrame(currentCSVFrame);
        }
        else
        {
            if (morphState <= 1.0f)
                targetFrame = Mathf.Lerp(43f, 113f, morphState);
            else if (morphState <= 2.0f)
                targetFrame = Mathf.Lerp(181f, 225f, morphState - 1.0f);
            else if (morphState <= 3.0f)
                targetFrame = Mathf.Lerp(231f, 249f, morphState - 2.0f);
        }

        // Convert Frame to Normalized Time
        float timeInSeconds = (targetFrame - 1f) / FPS;
        float normalizedTime = Mathf.Clamp01(timeInSeconds / clipLength);

        // Drive the animator
        animator.Play(0, 0, normalizedTime);
        // Using animator.Update(0) in Edit Mode is fine, but we must ensure we don't access .material in scripts
        animator.Update(0);
    }

    private void SyncMorphStateFromFrame(int frame)
    {
        if (frame <= 43) morphState = 0f;
        else if (frame <= 113) morphState = (frame - 43f) / (113f - 43f);
        else if (frame < 181) morphState = 1.0f;
        else if (frame <= 225) morphState = 1.0f + (frame - 181f) / (225f - 181f);
        else if (frame < 231) morphState = 2.0f;
        else if (frame <= 249) morphState = 2.0f + (frame - 231f) / (249f - 231f);
        else morphState = 3.0f;
    }
}
