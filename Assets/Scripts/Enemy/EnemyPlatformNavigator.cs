using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a collision-checked graph from scene navigation nodes and moves a dynamic
/// boss between platforms with visible parabolic jumps.
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
    [Tooltip("How far above an authored node the ground probe begins.")]
    [SerializeField, Min(0.05f)] private float nodeProbeRise = 1f;
    [Tooltip("Maximum distance below an authored node in which a landing surface is accepted.")]
    [SerializeField, Min(0.5f)] private float nodeProbeDepth = 10f;
    [Tooltip("Small separation retained between the boss collider and the landing surface.")]
    [SerializeField, Min(0f)] private float landingSkin = 0.03f;

    private readonly List<EnemyNavigationNode> nodes = new List<EnemyNavigationNode>();
    private readonly List<EnemyNavigationNode> path = new List<EnemyNavigationNode>();
    private readonly Dictionary<(EnemyNavigationNode, EnemyNavigationNode), bool> linkClearance =
        new Dictionary<(EnemyNavigationNode, EnemyNavigationNode), bool>();
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
    private bool awaitingLanding;
    private float landingRemaining;

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
            if (hopping && !explicitHopActive)
                CancelHop();
            else if (!explicitHopActive)
                HoldHorizontalPosition();
            return;
        }

        if (awaitingLanding)
        {
            HoldHorizontalPosition();
            landingRemaining -= Time.fixedDeltaTime;
            if (!IsGrounded() && landingRemaining > 0f)
                return;
            awaitingLanding = false;
            repathRemaining = 0f;
        }

        repathRemaining -= Time.fixedDeltaTime;
        if (!hopping && (repathRemaining <= 0f || pathIndex >= path.Count))
            RebuildPath();

        if (!hopping)
        {
            if (pathIndex >= path.Count)
                return;
            if (!BeginHop(path[pathIndex].Position))
                return;
        }

        hopElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(hopElapsed / hopDuration);
        Vector2 desired = EvaluateHopPosition(hopStart, hopTarget, progress);
        if (IsBlocked(body.position, desired))
        {
            CancelHop();
            return;
        }
        body.MovePosition(desired);

        if (progress >= 1f)
        {
            body.position = hopTarget;
            hopping = false;
            pathIndex++;
            BeginLanding();
        }
    }

    public void RefreshNodes()
    {
        nodes.Clear();
        linkClearance.Clear();
        nodes.AddRange(FindObjectsByType<EnemyNavigationNode>(FindObjectsSortMode.None));
        SnapNavigationNodesToGround();
    }

    /// <summary>
    /// Normalizes authored graph points to the current boss collider. The same points are shared by
    /// differently sized boss models, so storing a fixed world-space Y offset in the scene made the King
    /// hover while a lower value buried the Wizard. Raycast to the platform, then place the root exactly
    /// one collider-bottom clearance above it; the scripted hop and gravity now agree on the landing pose.
    /// </summary>
    public int SnapNavigationNodesToGround()
    {
        if (ownerCollider == null)
            ownerCollider = GetComponent<Collider2D>();
        if (ownerCollider == null || nodes.Count == 0)
            return 0;

        Physics2D.SyncTransforms();
        float bottomClearance = transform.position.y - ownerCollider.bounds.min.y;
        if (!float.IsFinite(bottomClearance) || bottomClearance <= 0.001f)
            bottomClearance = Mathf.Max(0.01f, ownerCollider.bounds.extents.y);

        int snapped = 0;
        float rise = Mathf.Max(0.05f, nodeProbeRise);
        float distance = rise + Mathf.Max(0.5f, nodeProbeDepth);
        foreach (EnemyNavigationNode node in nodes)
        {
            if (node == null)
                continue;
            Vector2 authored = node.Position;
            RaycastHit2D ground = Physics2D.Raycast(authored + Vector2.up * rise,
                Vector2.down, distance, groundMask);
            if (ground.collider == null)
                continue;

            node.transform.position = new Vector3(authored.x,
                ground.point.y + bottomClearance + landingSkin, node.transform.position.z);
            snapped++;
        }
        Physics2D.SyncTransforms();
        return snapped;
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

        EnemyNavigationNode start = FindClosestNode(body.position, true);
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
            Vector2 desired = EvaluateHopPosition(start, target, progress);
            if (IsBlocked(body.position, desired))
            {
                CancelHop();
                yield break;
            }
            body.MovePosition(desired);
        }

        transform.position = new Vector3(target.x, target.y, transform.position.z);
        body.position = target;
        explicitHopActive = false;
        BeginLanding();
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

        EnemyNavigationNode start = FindClosestNode(body.position, true);
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
        if (Mathf.Abs(delta.x) > maximumLinkDistance ||
            Mathf.Abs(delta.y) > maximumVerticalLink ||
            delta.sqrMagnitude > maximumLinkDistance * maximumLinkDistance)
            return false;

        var key = (a, b);
        if (!linkClearance.TryGetValue(key, out bool clear))
        {
            clear = IsArcClear(a.Position, b.Position);
            linkClearance[key] = clear;
        }
        return clear;
    }

    private EnemyNavigationNode FindClosestNode(Vector2 point, bool requireClear = false)
    {
        EnemyNavigationNode closest = null;
        float closestDistance = float.MaxValue;
        foreach (EnemyNavigationNode node in nodes)
        {
            if (requireClear && !IsArcClear(point, node.Position))
                continue;
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

    private bool BeginHop(Vector2 target)
    {
        if (!IsArcClear(body.position, target))
        {
            path.Clear();
            pathIndex = 0;
            repathRemaining = repathInterval;
            HoldHorizontalPosition();
            return false;
        }
        BeginScriptedMotion();
        hopStart = body.position;
        hopTarget = target;
        hopElapsed = 0f;
        hopDuration = GetHopDuration(hopStart, hopTarget, 1f);
        hopping = true;
        return true;
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
        awaitingLanding = false;
        path.Clear();
        pathIndex = 0;
        repathRemaining = 0f;
        BeginLanding();
    }

    private void ConfigureFallingBody()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0.1f, fallGravityScale);
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        HoldHorizontalPosition();
    }

    private void BeginScriptedMotion()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        awaitingLanding = false;
    }

    private void RestoreGravity()
    {
        if (body == null)
            return;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0.1f, fallGravityScale);
        HoldHorizontalPosition();
    }

    private void BeginLanding()
    {
        RestoreGravity();
        awaitingLanding = !IsGrounded();
        landingRemaining = landingTimeout;
    }

    private void HoldHorizontalPosition()
    {
        if (body == null)
            return;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
    }

    private bool IsArcClear(Vector2 start, Vector2 target)
    {
        if (ownerCollider == null)
            return true;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, target) /
            Mathf.Max(1f, ownerCollider.bounds.size.x)), 4, 32);
        Vector2 previous = start;
        for (int step = 1; step <= steps; step++)
        {
            Vector2 next = EvaluateHopPosition(start, target, step / (float)steps);
            if (IsBlocked(previous, next))
                return false;
            previous = next;
        }
        return true;
    }

    private bool IsBlocked(Vector2 start, Vector2 target)
    {
        if (ownerCollider == null)
            return false;
        Vector2 displacement = target - start;
        if (displacement.sqrMagnitude < 0.0001f)
            return false;
        Vector2 offset = (Vector2)ownerCollider.bounds.center - body.position;
        Vector2 size = (Vector2)ownerCollider.bounds.size * 0.85f;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(start + offset, size, 0f,
            displacement.normalized, displacement.magnitude, groundMask);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D obstacle = hit.collider;
            if (obstacle == null || obstacle == ownerCollider || obstacle.isTrigger)
                continue;
            PlatformEffector2D oneWay = obstacle.GetComponent<PlatformEffector2D>();
            if (oneWay != null && oneWay.useOneWay)
                continue;
            if (Mathf.Abs(hit.normal.x) > 0.4f || hit.normal.y < -0.4f)
                return true;
        }
        return false;
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

    private bool IsGrounded()
    {
        if (ownerCollider == null)
            return false;
        Bounds bounds = ownerCollider.bounds;
        RaycastHit2D[] hits = Physics2D.RaycastAll(bounds.center, Vector2.down,
            bounds.extents.y + landingSkin + 0.08f, groundMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider != ownerCollider &&
                !hit.collider.isTrigger && hit.normal.y > 0.5f)
                return true;
        }
        return false;
    }
}
