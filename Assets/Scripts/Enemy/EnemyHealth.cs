using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Boss health using the same pool as mobs, with boss-only victory flow.</summary>
public sealed class EnemyHealth : CombatHealth
{
    [SerializeField] private GameObject victoryOverlay;
    [SerializeField] private string victoryReturnSceneName = "stage1_full";
    [SerializeField] private StoryDialogueController storyController;

    public override CombatFaction Faction => CombatFaction.Enemy;
    public string VictoryReturnSceneName => victoryReturnSceneName;

    protected override void Awake()
    {
        base.Awake();
        if (victoryOverlay == null)
            throw new MissingReferenceException("EnemyHealth requires the scene-authored Victory Overlay.");
        victoryOverlay.SetActive(false);
        stateMachine = GetComponent<BossStateMachine>();
        entityVFX = GetComponent<Entity_VFX>();
    }

    private BossStateMachine stateMachine;
    private Entity_VFX entityVFX;

    protected override void OnDamaged(float amount, Transform source)
    {
        stateMachine?.NotifyHurt();
        // Same white hit flash the mobs use. Entity_VFX swaps the material while BossSpriteAnimator
        // only swaps the sprite, so the flash and the animation do not fight over the renderer.
        entityVFX?.PlayOnDamageVfx();
    }

    private void Update()
    {
        if (isDead && victoryOverlay.activeSelf && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(victoryReturnSceneName);
        }
    }

    // The Boss deals no contact damage: touching it is safe, only its telegraphed attacks hurt.

    public bool TakeDamage(float amount = CombatBalance.PlayerDamagePerHit) => ApplyDamage(amount, transform);

    protected override void OnDefeated(Transform source)
    {
        GameManager.MarkMatchOver();
        stateMachine?.NotifyDead();
        if (storyController == null || !storyController.PlayBossVictory())
            victoryOverlay.SetActive(true);

        foreach (EnemyAttackPattern pattern in GetComponents<EnemyAttackPattern>())
            pattern.enabled = false;

        EnemyAttackController attacks = GetComponent<EnemyAttackController>();
        if (attacks != null)
        {
            attacks.StopAllCoroutines();
            attacks.enabled = false;
        }

        EnemyPlatformNavigator navigator = GetComponent<EnemyPlatformNavigator>();
        if (navigator != null)
            navigator.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.35f, 0.35f, 0.35f, 1f);
    }
}
