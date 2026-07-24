using UnityEngine;
using UnityEngine.UI;

public enum AbilityUnlockKind
{
    DoubleJump,
    Dash
}

/// <summary>
/// A persistent, scene-authored reward orb. Its linked chest controls when it appears;
/// touching the revealed orb grants exactly one movement ability to the unified Hero.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public sealed class AbilityUnlockOrb2D : MonoBehaviour
{
    [SerializeField] private AbilityUnlockKind ability;
    [SerializeField] private TreasureChest2D sourceChest;
    [SerializeField] private Role player;
    [SerializeField] private SpriteRenderer orbRenderer;
    [SerializeField] private CircleCollider2D pickupTrigger;
    [SerializeField] private GameObject prompt;
    [SerializeField] private Text promptText;
    [SerializeField] private StoryDialogueController storyController;

    private bool revealed;
    private bool collected;

    public AbilityUnlockKind Ability => ability;
    public TreasureChest2D SourceChest => sourceChest;
    public Role Player => player;
    public bool IsRevealed => revealed;
    public bool IsCollected => collected;

    private void Awake()
    {
        orbRenderer ??= GetComponent<SpriteRenderer>();
        pickupTrigger ??= GetComponent<CircleCollider2D>();
        if (sourceChest == null || player == null || orbRenderer == null || pickupTrigger == null)
            throw new MissingReferenceException("AbilityUnlockOrb2D requires its scene-authored chest, Hero, renderer and trigger.");
        pickupTrigger.isTrigger = true;
        SetVisible(false);
        sourceChest.RegisterAbilityOrb(this);

        // The unlock outlives dying, so re-grant it to the freshly loaded Hero and stay collected —
        // this is also what lets the source chest know it has nothing left to give.
        if (RunProgress.IsUnlocked(ability))
        {
            collected = true;
            GrantAbility();
        }
    }

    private void GrantAbility()
    {
        if (ability == AbilityUnlockKind.DoubleJump)
            player.SetMaxJumpCount(2);
        else
            player.SetDashUnlocked(true);
    }

    private void Update()
    {
        if (!revealed && !collected && sourceChest.IsOpened)
            Reveal();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Role candidate = other != null ? other.GetComponentInParent<Role>() : null;
        if (candidate == player)
            TryGrant(candidate);
    }

    public void Reveal()
    {
        if (collected || revealed || !sourceChest.IsOpened)
            return;
        revealed = true;
        if (promptText != null)
            promptText.text = ability == AbilityUnlockKind.DoubleJump
                ? "Touch the orb to unlock DOUBLE JUMP"
                : "Touch the orb to unlock DASH";
        SetVisible(true);
    }

    public bool TryGrant(Role candidate)
    {
        if (!revealed || collected || candidate == null || candidate != player)
            return false;

        GrantAbility();
        RunProgress.Unlock(ability);

        collected = true;
        revealed = false;
        SetVisible(false);
        storyController?.ShowAbilityTutorial(ability);
        return true;
    }

    private void SetVisible(bool value)
    {
        orbRenderer.enabled = value;
        pickupTrigger.enabled = value;
        prompt?.SetActive(value);
    }
}
