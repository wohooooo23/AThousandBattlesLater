using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene-authored victory gate for a Boss encounter with required companion enemies.
/// The Boss can die first, but the final story/victory flow starts only after every required
/// CombatHealth has been defeated.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossEncounterObjective : MonoBehaviour
{
    [SerializeField] private EnemyHealth boss;
    [SerializeField] private CombatHealth[] requiredEnemies;

    private bool completed;

    public EnemyHealth Boss => boss;
    public IReadOnlyList<CombatHealth> RequiredEnemies => requiredEnemies;
    public int RequiredEnemyCount => requiredEnemies != null ? requiredEnemies.Length : 0;
    public bool IsComplete => completed;
    public bool AllRequiredEnemiesDefeated => requiredEnemies != null && requiredEnemies.Length > 0 &&
                                               requiredEnemies.All(enemy => enemy != null && enemy.IsDead);

    private void Awake()
    {
        ValidateConfiguration();
        foreach (CombatHealth enemy in requiredEnemies)
            enemy.Defeated += OnRequiredEnemyDefeated;
    }

    private void OnDestroy()
    {
        if (requiredEnemies == null)
            return;
        foreach (CombatHealth enemy in requiredEnemies)
            if (enemy != null)
                enemy.Defeated -= OnRequiredEnemyDefeated;
    }

    private void OnRequiredEnemyDefeated(CombatHealth defeated)
    {
        if (completed || !AllRequiredEnemiesDefeated)
            return;
        completed = true;
        boss.CompleteVictoryFromObjective();
    }

    private void ValidateConfiguration()
    {
        if (boss == null)
            throw new MissingReferenceException(name + " requires its scene-authored Boss health.");
        if (requiredEnemies == null || requiredEnemies.Length < 2 ||
            requiredEnemies.Any(enemy => enemy == null) || !requiredEnemies.Contains(boss) ||
            requiredEnemies.Distinct().Count() != requiredEnemies.Length)
            throw new MissingReferenceException(name +
                " requires a unique enemy list containing the Boss and at least one companion.");
    }
}
