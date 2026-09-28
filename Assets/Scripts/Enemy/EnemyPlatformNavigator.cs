using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Samples arena landing surfaces and moves a dynamic Boss between them with
/// collision-checked parabolic jumps. Combat decisions and attack timing stay elsewhere.
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
    [Tooltip("Small separation retained between the boss collider and the landing surface.")]
    [SerializeField, Min(0f)] private float landingSkin = 0.03f;
    [Tooltip("How often to detect spawned or removed arena colliders without an explicit notification.")]
    [SerializeField, Min(0.1f)] private float geometryPollInterval = 0.5f;

    private readonly List<Vector2> path = new List<Vector2>();
    private BossLandingGraph graph;
    private Bounds arenaBounds;
    private float geometryPollRemaining;
    private bool geometryDirty;
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

    public int LandingSpotCount => graph != null ? graph.SpotCount : 0;
    public bool IsHopping => hopping;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        attackController = GetComponent<EnemyAttackController>();
        ownerCollider = GetComponent<Collider2D>();
        ConfigureFallingBody();
        graph = new BossLandingGraph(ownerCollider, body, groundMask, landingSkin,
            maximumLinkDistance, maximumVerticalLink, IsArcClear);
    }

    private void Start()
    {
        IDamageable player = CombatTargets.FindClosest(transform.position, CombatFaction.Player);
        hero = player != null ? player.transform : null;
        ResolveArenaBounds();
        RefreshSurfaces();
        if (!hopping && !explicitHopActive)
            BeginLanding();
        RebuildPath();
    }

    private void FixedUpdate()
    {
        if (hero == null || body == null)
            return;

        geometryPollRemaining -= Time.fixedDeltaTime;
        if (geometryDirty || geometryPollRemaining <= 0f)
        {
            geometryPollRemaining = geometryPollInterval;
            int signature = graph.ReadGeometrySignature(arenaBounds);
            if (geometryDirty || signature != graph.GeometrySignature)
            {
                if (hopping || explicitHopActive)
                    CancelHop();
                RefreshSurfaces();
                repathRemaining = 0f;
            }
            geometryDirty = false;
        }

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
            // A coroutine timeout may release the attack controller, but pursuit
            // must never turn a missing platform into another jump in mid-air.
            if (!IsGrounded())
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
            if (!BeginHop(path[pathIndex]))
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

    private void ResolveArenaBounds()
    {
        foreach (BossArenaController arena in FindObjectsByType<BossArenaController>(
                     FindObjectsInactive.Include))
        {
            if (arena.BossRoot != gameObject)
                continue;
            arenaBounds = new Bounds((arena.ArenaMin + arena.ArenaMax) * 0.5f,
                arena.ArenaMax - arena.ArenaMin);
            return;
        }
        // Legacy standalone scenes and isolated tests have no BossArenaController.
        arenaBounds = new Bounds(transform.position, new Vector3(200f, 120f, 1f));
    }

    public void RefreshSurfaces()
    {
        if (graph == null)
            return;
        if (arenaBounds.size.x <= 0f)
            ResolveArenaBounds();
        graph.Rebuild(arenaBounds);
        path.Clear();
        pathIndex = 0;
    }

    /// <summary>Call after spawning or removing a platform to replan on the next physics step.</summary>
    public void NotifyGeometryChanged(Bounds changedArea)
    {
        if (arenaBounds.Intersects(changedArea))
            geometryDirty = true;
    }

    public bool TryGetBlinkDestination(out Vector2 destination)
    {
        destination = default;
        if (graph == null)
            return false;
        RefreshSurfaces();
        return graph.TryBlink(body.position, hero != null ? (Vector2)hero.position : body.position,
            out destination);
    }

    public void ResetNavigation()
    {
        CancelHop();
        if (body != null)
            body.position = transform.position;
        RefreshSurfaces();
        RebuildPath();
    }

    /// <summary>One reachable jump away from the Hero, used by the King's attack relocation.</summary>
    public IEnumerator RetreatHopRoutine(float speedMultiplier)
    {
        if (!PrepareExplicitHop())
            yield break;

        List<Vector2> retreatPath = new List<Vector2>();
        if (!graph.TryRetreat(body.position, hero.position, retreatPath))
            yield break;

        int next = CanHopTo(body.position, retreatPath[1]) ? 1 : 0;
        yield return HopToNodeRoutine(retreatPath[next], speedMultiplier);
    }

    private bool PrepareExplicitHop()
    {
        if (body == null)
            return false;
        if (hero == null)
        {
            IDamageable player = CombatTargets.FindClosest(transform.position, CombatFaction.Player);
            hero = player != null ? player.transform : null;
        }
        if (hero == null)
            return false;

        CancelHop();
        RefreshSurfaces();
        return LandingSpotCount > 1;
    }

    private IEnumerator HopToNodeRoutine(Vector2 target, float speedMultiplier)
    {
        Vector2 start = body.position;
        if (!CanHopTo(start, target))
            yield break;
        explicitHopActive = true;
        BeginScriptedMotion();
        float multiplier = Mathf.Max(0.01f, speedMultiplier);
        float duration = GetHopDuration(start, target, multiplier);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            if (!explicitHopActive)
                yield break;
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
        if (LandingSpotCount == 0 || hero == null)
            return;
        graph.TryPath(body.position, hero.position, path);
        // Skip the sampled anchor when the real body can safely reach the next step.
        pathIndex = path.Count > 1 && CanHopTo(body.position, path[1]) ? 1 : 0;
    }

    private bool BeginHop(Vector2 target)
    {
        if (!CanHopTo(body.position, target))
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

    private bool CanHopTo(Vector2 start, Vector2 target)
    {
        Vector2 delta = target - start;
        return Mathf.Abs(delta.x) <= maximumLinkDistance && Mathf.Abs(delta.y) <= maximumVerticalLink &&
               delta.sqrMagnitude <= maximumLinkDistance * maximumLinkDistance && IsArcClear(start, target);
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
