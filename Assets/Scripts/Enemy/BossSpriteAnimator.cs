using System;
using UnityEngine;

/// <summary>
/// Lightweight, code-driven sprite-sheet player for the boss. No AnimatorController: each named
/// clip is a Sprite[] played at a fixed fps. The BossWizardBuilder fills the frame arrays from the
/// Evil Wizard sheets. Lives on a "WizardVisual" child so flipping for facing never touches colliders.
///
/// Cast timing: an attack clip is split at <see cref="Clip.releaseFrame"/>. During a skill's windup
/// the wizard is scrubbed frame 0 -> releaseFrame by the charge progress (BeginCast + SetCastProgress),
/// so the staff reaches its apex exactly as the skill fires; ReleaseCast then plays the follow-through
/// frames once. This keeps the animation locked to each skill's "windup -> fire" timeline.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossSpriteAnimator : MonoBehaviour
{
    [Serializable]
    public sealed class Clip
    {
        public string name;
        public Sprite[] frames;
        public float fps = 10f;
        public bool loop = true;
        [Tooltip("Attack clips only: the frame where the projectile launches (windup ends).")]
        public int releaseFrame;
    }

    private enum PlayMode { Normal, Windup, FollowThrough }

    [Tooltip("Art faces right by default; used to flip toward the hero.")]
    public bool defaultFacesRight = true;

    public Clip idle = new Clip { name = "Idle", fps = 8f, loop = true };
    public Clip run = new Clip { name = "Run", fps = 12f, loop = true };
    public Clip attack1 = new Clip { name = "Attack1", fps = 12f, loop = false, releaseFrame = 5 };
    public Clip attack2 = new Clip { name = "Attack2", fps = 12f, loop = false, releaseFrame = 5 };
    public Clip takeHit = new Clip { name = "TakeHit", fps = 12f, loop = false };
    public Clip death = new Clip { name = "Death", fps = 10f, loop = false };

    private SpriteRenderer spriteRenderer;
    private Clip current;
    private PlayMode mode;
    private float frameTimer;
    private int frameIndex;
    private int releaseFrame;
    private bool finished;
    private Action onComplete;

    public bool IsPlaying(string clipName) => current != null && current.name == clipName;
    public bool CurrentFinished => finished;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Safety: show idle frame 0 immediately so the boss is never blank before Play is called.
        if (spriteRenderer != null && spriteRenderer.sprite == null &&
            idle != null && idle.frames != null && idle.frames.Length > 0)
            spriteRenderer.sprite = idle.frames[0];
    }

    /// <summary>Normal looping/one-shot playback. Restarting a looping clip is a no-op.</summary>
    public void Play(string clipName, Action complete = null)
    {
        Clip clip = Resolve(clipName);
        if (!Valid(clip))
            return;
        if (current == clip && clip.loop && mode == PlayMode.Normal)
            return;
        current = clip;
        mode = PlayMode.Normal;
        Restart(complete);
    }

    /// <summary>Begins a cast: freezes on frame 0 until SetCastProgress scrubs toward the release frame.</summary>
    public void BeginCast(string clipName)
    {
        Clip clip = Resolve(clipName);
        if (!Valid(clip))
            return;
        current = clip;
        mode = PlayMode.Windup;
        releaseFrame = Mathf.Clamp(clip.releaseFrame, 0, clip.frames.Length - 1);
        Restart(null);
    }

    /// <summary>Scrubs the windup frames [0..releaseFrame] by the skill's charge progress (0..1).</summary>
    public void SetCastProgress(float progress)
    {
        if (mode != PlayMode.Windup || !Valid(current))
            return;
        int frame = Mathf.RoundToInt(Mathf.Lerp(0f, releaseFrame, Mathf.Clamp01(progress)));
        Show(Mathf.Clamp(frame, 0, current.frames.Length - 1));
    }

    /// <summary>The skill fired: play the follow-through frames [releaseFrame..end] once.</summary>
    public void ReleaseCast()
    {
        if (!Valid(current))
            return;
        mode = PlayMode.FollowThrough;
        frameIndex = Mathf.Clamp(releaseFrame, 0, current.frames.Length - 1);
        frameTimer = 0f;
        finished = false;
        Show(frameIndex);
    }

    public void SetFacing(bool towardRight)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = towardRight != defaultFacesRight;
    }

    private void Update()
    {
        if (mode == PlayMode.Windup || !Valid(current))
            return;   // windup frames are driven externally by SetCastProgress

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;
            if (frameIndex >= current.frames.Length)
            {
                bool looping = mode == PlayMode.Normal && current.loop;
                if (looping)
                {
                    frameIndex = 0;
                }
                else
                {
                    frameIndex = current.frames.Length - 1;
                    if (!finished)
                    {
                        finished = true;
                        onComplete?.Invoke();
                    }
                }
            }
            Show(frameIndex);
        }
    }

    private void Restart(Action complete)
    {
        frameTimer = 0f;
        frameIndex = 0;
        finished = false;
        onComplete = complete;
        Show(0);
    }

    private void Show(int index)
    {
        frameIndex = index;
        if (spriteRenderer != null && current != null && current.frames != null && index < current.frames.Length)
            spriteRenderer.sprite = current.frames[index];
    }

    private static bool Valid(Clip clip) => clip != null && clip.frames != null && clip.frames.Length > 0;

    private Clip Resolve(string clipName)
    {
        switch (clipName)
        {
            case "Idle": return idle;
            case "Run": return run;
            case "Attack1": return attack1;
            case "Attack2": return attack2;
            case "TakeHit": return takeHit;
            case "Death": return death;
            default: return null;
        }
    }
}
