using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a lightweight graph from scene navigation nodes, runs A*, and moves
/// the kinematic enemy between waypoints using visible parabolic jumps.
///
/// Referenced interfaces:
///   Enemy/EnemyNavigationNode.Position        — graph landing points collected from the scene
///   Enemy/EnemyAttackController.IsAttacking    — pauses navigation while an attack charges
/// Exposes: NavigationNodeCount, ResetNavigation() (used by PlayMode tests).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyAttackController))]
public sealed class EnemyPlatformNavigator : MonoBehaviour
{
    [SerializeField] private float navigationSpeed = 25f;
    [SerializeField] private float jumpHeight = 8f;
    [SerializeField] private float minimumHopDuration = 0.38f;
    [SerializeField] private float maximumLinkDistance = 58f;
    [SerializeField] private float maximumVerticalLink = 28f;
    [SerializeField] private float repathInterval = 0.35f;
    [Header("Landing Physics")]
    [Tooltip("Gravity restored after a scripted hop so the boss settles onto the platform collider.")]
    [SerializeField, Min(0.1f)] private float fallGravityScale = 6f;
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField, Min(0.1f)] private float landingTimeout = 2f;

    private readonly List<EnemyNavigationNode> nodes = new List<EnemyNavigationNode>();
    private readonly List<EnemyNavigationNode> path = new List<EnemyNavigationNode>();
    private Rigidbody2D body;
    private EnemyAttackController attackController;
    private Collider2D ownerCollider;
    private Transform hero;
    private int pathIndex;
    private float repathRemaining;
    private bool hopping;
    private Vector2 hopStart;
    private Vector2 hopTarget;
    private float hopElapsed;
    private float hopDuration;
    private bool explicitHopActive;

    public int NavigationNodeCount => nodes.Count;
    public bool IsHopping => hopping;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        attackController = GetComponent<EnemyAttackController>();
        ownerCollider = GetComponent<Collider2D>();
        ConfigureFallingBody();
    }

    private void Start()
    {
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player);
        hero = player != null ? player.transform : null;
        RefreshNodes();
        RebuildPath();
    }

    private void FixedUpdate()
    {
        if (hero == null || body == null)
            return;

        if (attackController.IsAttacking)
        {
            // A combo-triggered relocation deliberately runs while the attack
            // controller owns the boss. Keep that scripted arc in control;
            // ordinary pursuit hops are still cancelled during attacks.
            if (!explicitHopActive)
                CancelHop();
            return;
        }

        repathRemaining -= Time.fixedDeltaTime;
        if (!hopping && (repathRemaining <= 0f || pathIndex >= path.Count))
            RebuildPath();

        if (!hopping)
        {
            if (pathIndex >= path.Count)
                return;
            BeginHop(path[pathIndex].Position);
        }

        hopElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(hopElapsed / hopDuration);
        body.MovePosition(EvaluateHopPosition(hopStart, hopTarget, progress));

        if (progress >= 1f)
        {
            hopping = false;
            pathIndex++;
            RestoreGravity();
        }
    }

    public void RefreshNodes()
    {
        nodes.Clear();
        nodes.AddRange(FindObjectsByType<EnemyNavigationNode>(FindObjectsSortMode.None));
    }

    public void ResetNavigation()
    {
        CancelHop();
        if (body != null)
            body.position = transform.position;
        RefreshNodes();
        RebuildPath();
    }

    /// <summary>
    /// Uses the same navigation graph and hop motion as pursuit, but routes toward the reachable node
    /// farthest from the Hero and performs only the first A* step. Used by the King after its attack
    /// counter fires so relocation creates breathing room without crossing walls or skipping nodes.
    /// </summary>
    public IEnumerator RetreatHopRoutine(float speedMultiplier)
    {
        if (!PrepareExplicitHop())
            yield break;

        EnemyNavigationNode start = FindClosestNode(body.position);
        if (start == null)
            yield break;

        List<EnemyNavigationNode> retreatPath = BuildRetreatPath(start, hero.position);
        if (retreatPath.Count < 2)
            yield break;

        yield return HopToNodeRoutine(retreatPath[1].Position, speedMultiplier);
    }

    private bool PrepareExplicitHop()
    {
        if (body == null)
            return false;
        if (hero == null)
        {
            CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player);
            hero = player != null ? player.transform : null;
        }
        if (hero == null)
            return false;

        CancelHop();
        RefreshNodes();
        return nodes.Count > 1;
    }

    private IEnumerator HopToNodeRoutine(Vector2 target, float speedMultiplier)
    {
        Vector2 start = body.position;
        explicitHopActive = true;
        BeginScriptedMotion();
        float multiplier = Mathf.Max(0.01f, speedMultiplier);
        float duration = GetHopDuration(start, target, multiplier);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            body.MovePosition(EvaluateHopPosition(start, target, progress));
        }

        transform.position = new Vector3(target.x, target.y, transform.position.z);
        body.position = target;
        explicitHopActive = false;
        RestoreGravity();
        Physics2D.SyncTransforms();
        yield return WaitForLandingRoutine();
        ResetNavigation();
    }

    private void RebuildPath()
    {
        repathRemaining = repathInterval;
        path.Clear();
        pathIndex = 0;
        if (nodes.Count == 0 || hero == null)
            return;

        EnemyNavigationNode start = FindClosestNode(body.position);
        EnemyNavigationNode goal = FindClosestNode(hero.position);
        if (start == null || goal == null || start == goal)
            return;

        FindPathAStar(start, goal, path);
        // A* includes the graph start as element zero. It is an anchor, not a
        // destination; visiting it first made the boss initially move away.
        pathIndex = path.Count > 1 ? 1 : path.Count;
    }

    private void FindPathAStar(EnemyNavigationNode start, EnemyNavigationNode goal, List<EnemyNavigationNode> result)
    {
        List<EnemyNavigationNode> open = new List<EnemyNavigationNode> { start };
        Dictionary<EnemyNavigationNode, EnemyNavigationNode> cameFrom = new Dictionary<EnemyNavigationNode, EnemyNavigationNode>();
        Dictionary<EnemyNavigationNode, float> cost = new Dictionary<EnemyNavigationNode, float> { [start] = 0f };

        while (open.Count > 0)
        {
            EnemyNavigationNode current = open[0];
            float bestScore = cost[current] + Vector2.Distance(current.Position, goal.Position);
            for (int i = 1; i < open.Count; i++)
            {
                float score = cost[open[i]] + Vector2.Distance(open[i].Position, goal.Position);
                if (score < bestScore)
                {
                    current = open[i];
                    bestScore = score;
                }
            }

            if (current == goal)
            {
                result.Add(current);
                while (cameFrom.TryGetValue(current, out EnemyNavigationNode previous))
                {
                    current = previous;
                    result.Add(current);
                }
                result.Reverse();
                return;
            }

            open.Remove(current);
            foreach (EnemyNavigationNode neighbour in nodes)
            {
                if (neighbour == current || !CanLink(current, neighbour))
                    continue;

                float nextCost = cost[current] + Vector2.Distance(current.Position, neighbour.Position);
                if (!cost.TryGetValue(neighbour, out float knownCost) || nextCost < knownCost)
                {
                    cameFrom[neighbour] = current;
                    cost[neighbour] = nextCost;
                    if (!open.Contains(neighbour))
                        open.Add(neighbour);
                }
            }
        }
    }

    private bool CanLink(EnemyNavigationNode a, EnemyNavigationNode b)
    {
        Vector2 delta = b.Position - a.Position;
        return Mathf.Abs(delta.x) <= maximumLinkDistance &&
               Mathf.Abs(delta.y) <= maximumVerticalLink &&
               delta.sqrMagnitude <= maximumLinkDistance * maximumLinkDistance;
    }

    private EnemyNavigationNode FindClosestNode(Vector2 point)
    {
        EnemyNavigationNode closest = null;
        float closestDistance = float.MaxValue;
        foreach (EnemyNavigationNode node in nodes)
        {
            float distance = ((Vector2)node.transform.position - point).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = node;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private List<EnemyNavigationNode> BuildRetreatPath(EnemyNavigationNode start, Vector2 threatPosition)
    {
        List<EnemyNavigationNode> bestPath = new List<EnemyNavigationNode>();
        float farthestDistance = float.NegativeInfinity;
        foreach (EnemyNavigationNode candidate in nodes)
        {
            if (candidate == start)
                continue;

            List<EnemyNavigationNode> candidatePath = new List<EnemyNavigationNode>();
            FindPathAStar(start, candidate, candidatePath);
            if (candidatePath.Count < 2)
                continue;

            float distance = (candidate.Position - threatPosition).sqrMagnitude;
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                bestPath = candidatePath;
            }
        }
        return bestPath;
    }

    private void BeginHop(Vector2 target)
    {
        BeginScriptedMotion();
        hopStart = body.position;
        hopTarget = target;
        hopElapsed = 0f;
        hopDuration = GetHopDuration(hopStart, hopTarget, 1f);
        hopping = true;
    }

    private float GetHopDuration(Vector2 start, Vector2 target, float speedMultiplier)
    {
        float multiplier = Mathf.Max(0.01f, speedMultiplier);
        return Mathf.Max(minimumHopDuration / multiplier,
            Vector2.Distance(start, target) / (Mathf.Max(0.01f, navigationSpeed) * multiplier));
    }

    private Vector2 EvaluateHopPosition(Vector2 start, Vector2 target, float progress)
    {
        Vector2 position = Vector2.Lerp(start, target, progress);
        position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
        return position;
    }

    private void CancelHop()
    {
        hopping = false;
        explicitHopActive = false;
        path.Clear();
        pathIndex = 0;
        repathRemaining = 0f;
        RestoreGravity();
    }

    private void ConfigureFallingBody()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0.1f, fallGravityScale);
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void BeginScriptedMotion()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
    }

    private void RestoreGravity()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0.1f, fallGravityScale);
    }

    private IEnumerator WaitForLandingRoutine()
    {
        float remaining = landingTimeout;
        while (remaining > 0f && !IsGrounded())
        {
            yield return new WaitForFixedUpdate();
            remaining -= Time.fixedDeltaTime;
        }
        if (body != null)
            body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Min(0f, body.linearVelocity.y));
    }

    private bool IsGrounded() => ownerCollider != null && ownerCollider.IsTouchingLayers(groundMask);
}
