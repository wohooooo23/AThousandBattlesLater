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

    private readonly List<EnemyNavigationNode> nodes = new List<EnemyNavigationNode>();
    private readonly List<EnemyNavigationNode> path = new List<EnemyNavigationNode>();
    private Rigidbody2D body;
    private EnemyAttackController attackController;
    private Transform hero;
    private int pathIndex;
    private float repathRemaining;
    private bool hopping;
    private Vector2 hopStart;
    private Vector2 hopTarget;
    private float hopElapsed;
    private float hopDuration;

    public int NavigationNodeCount => nodes.Count;
    public bool IsHopping => hopping;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        attackController = GetComponent<EnemyAttackController>();
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
        Vector2 position = Vector2.Lerp(hopStart, hopTarget, progress);
        position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
        body.MovePosition(position);

        if (progress >= 1f)
        {
            hopping = false;
            pathIndex++;
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

    private void BeginHop(Vector2 target)
    {
        hopStart = body.position;
        hopTarget = target;
        hopElapsed = 0f;
        hopDuration = Mathf.Max(minimumHopDuration, Vector2.Distance(hopStart, hopTarget) / navigationSpeed);
        hopping = true;
    }

    private void CancelHop()
    {
        hopping = false;
        path.Clear();
        pathIndex = 0;
        repathRemaining = 0f;
    }
}
