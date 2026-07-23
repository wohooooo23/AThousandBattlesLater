using System;
using UnityEngine;

public enum MobAnimationState
{
    Idle,
    Move,
    Hurt,
    Dead,
    AttackOne,
    AttackTwo
}

[Serializable]
public sealed class MobAnimationFrames
{
    public Sprite[] frames = Array.Empty<Sprite>();
    [Min(1f)] public float framesPerSecond = 10f;
    public bool loop = true;
}

/// <summary>
/// Lightweight sprite-sheet animator shared by the four package mobs.
/// Attack clips are imported and ready for later combat design, but the AI does not enter them yet.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MobSpriteAnimator : MonoBehaviour
{
    public MobAnimationFrames idle = new();
    public MobAnimationFrames move = new();
    public MobAnimationFrames hurt = new();
    public MobAnimationFrames dead = new();
    public MobAnimationFrames attackOne = new();
    public MobAnimationFrames attackTwo = new();

    private SpriteRenderer spriteRenderer;
    private MobAnimationFrames activeClip;
    private MobAnimationState activeState;
    private float elapsed;
    private int frameIndex;

    public MobAnimationState ActiveState => activeState;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Play(MobAnimationState.Idle, true);
    }

    private void OnEnable()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        Play(MobAnimationState.Idle, true);
    }

    private void Update()
    {
        if (activeClip == null || activeClip.frames == null || activeClip.frames.Length <= 1)
            return;

        elapsed += Time.deltaTime;
        float secondsPerFrame = 1f / Mathf.Max(1f, activeClip.framesPerSecond);
        while (elapsed >= secondsPerFrame)
        {
            elapsed -= secondsPerFrame;
            int next = frameIndex + 1;
            if (next >= activeClip.frames.Length)
                next = activeClip.loop ? 0 : activeClip.frames.Length - 1;
            SetFrame(next);
        }
    }

    public void Play(MobAnimationState state, bool restart = false)
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (!restart && activeClip != null && activeState == state)
            return;

        activeState = state;
        activeClip = GetClip(state);
        elapsed = 0f;
        SetFrame(0);
    }

    /// <summary>Play length of a clip in seconds; 0 when the clip has no frames.</summary>
    public float GetDuration(MobAnimationState state)
    {
        MobAnimationFrames clip = GetClip(state);
        if (clip?.frames == null || clip.frames.Length == 0)
            return 0f;
        return clip.frames.Length / Mathf.Max(1f, clip.framesPerSecond);
    }

    public void Face(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) > 0.01f)
            spriteRenderer.flipX = horizontalDirection < 0f;
    }

    private MobAnimationFrames GetClip(MobAnimationState state)
    {
        return state switch
        {
            MobAnimationState.Move => move,
            MobAnimationState.Hurt => hurt,
            MobAnimationState.Dead => dead,
            MobAnimationState.AttackOne => attackOne,
            MobAnimationState.AttackTwo => attackTwo,
            _ => idle
        };
    }

    private void SetFrame(int index)
    {
        frameIndex = index;
        if (activeClip?.frames == null || activeClip.frames.Length == 0)
            return;
        spriteRenderer.sprite = activeClip.frames[Mathf.Clamp(index, 0, activeClip.frames.Length - 1)];
    }
}
