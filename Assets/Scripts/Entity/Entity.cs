using UnityEngine;

/// <summary>Common animated 2D actor foundation used by the player and Orc.</summary>
public class Entity : MonoBehaviour
{
    public StateMachine stateMachine { get; private set; }
    public Animator animator { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public int facingside = 1;

    [SerializeField] protected LayerMask groundLayer = ~0;
    [SerializeField, Min(0.01f)] private float grounddistance = 0.18f;
    [SerializeField, Min(0.01f)] private float walldistance = 0.2f;
    [SerializeField]
    private Transform wallcheck1; //用于检测角色是否在墙上的空物体
    [SerializeField]
    private Transform wallcheck2; //用于检测角色是否在墙上的空物体
    [SerializeField]
    private Transform groundcheck;//用于检测地面

    public bool isgrounded { get; private set; }
    public bool iswall { get; private set; }

    private Collider2D entityCollider;

    protected virtual void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        entityCollider = GetComponent<Collider2D>();
        stateMachine = new StateMachine();
    }

    protected virtual void Start() { }

    protected virtual void Update()
    {
        HandleCollision();
        stateMachine.currentState?.Update();
    }

    public void Change_Vec(float xVelocity, float yVelocity)
    {
        if (rb == null)
            return;
        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        TurnSide(xVelocity);
    }

    public void Flip()
    {
        transform.Rotate(0f, 180f, 0f);
        facingside = -facingside;
    }

    public void TurnSide(float xVelocity)
    {
        if ((xVelocity > 0f && facingside == -1) || (xVelocity < 0f && facingside == 1))
            Flip();
    }

    /// <summary>
    /// Probe distances are authored for an unscaled actor, but every actor in the scenes is scaled
    /// up (Hero and mobs 5x, Boss 6.25x). A raw world-space distance shrinks to nothing relative to
    /// the body, so scale it with the actor.
    /// </summary>
    private float ProbeScale => Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));

/*
    private void HandleCollision()//检测角色与地面、墙的碰撞
    {
        // The ground probe comes from the collider, not from the serialized groundcheck Transform:
        // on Hero.prefab that field points at the ROOT, so the ray started at the body centre and
        // never reached the feet. It only ever reported "grounded" because Queries Start In
        // Colliders is on and groundLayer used to be ~0, so the ray hit the actor's own collider
        // every frame. Once actors got their own physics layer and were excluded from the mask,
        // that false positive vanished and landings began to fail: the fall state waits on
        // isgrounded and jump counts only reset when the ground state is entered, so one miss
        // froze the falling pose and blocked jumping.
        //
        // Casting a box across most of the foot width, starting just below the collider, also
        // removes single-ray misses on tile seams and platform edges.
        if (entityCollider != null)
        {
            Bounds bounds = entityCollider.bounds;
            Vector2 groundSize = new Vector2(bounds.size.x * 0.78f, Mathf.Max(0.02f, bounds.size.y * 0.08f));
            Vector2 groundOrigin = new Vector2(bounds.center.x, bounds.min.y - groundSize.y * 0.5f - 0.02f);
            isgrounded = Physics2D.BoxCast(groundOrigin, groundSize, 0f, Vector2.down,
                grounddistance * ProbeScale, groundLayer);
        }
        else if (groundcheck != null)
        {
            isgrounded = Physics2D.Raycast(groundcheck.position, Vector2.down,
                grounddistance * ProbeScale, groundLayer);
        }

        float wallReach = walldistance * ProbeScale;
        if (wallcheck2 == null)
        {
            iswall=Physics2D.Raycast(wallcheck1.position,Vector2.right*facingside,wallReach,groundLayer);
        }
        else
        {
            iswall=Physics2D.Raycast(wallcheck1.position,Vector2.right*facingside,wallReach,groundLayer)
        && Physics2D.Raycast(wallcheck2.position,Vector2.right*facingside,wallReach,groundLayer);
        }
    }
    protected virtual void OnDrawGizmos()//绘制射线，用于调试角色与地面、墙的碰撞检测
    {
        // Mirror HandleCollision exactly, otherwise the gizmo lies about what is actually probed.
        Gizmos.color = Color.red;
        Collider2D gizmoCollider = entityCollider != null ? entityCollider : GetComponent<Collider2D>();
        if (gizmoCollider != null)
        {
            Bounds bounds = gizmoCollider.bounds;
            Vector2 groundSize = new Vector2(bounds.size.x * 0.78f, Mathf.Max(0.02f, bounds.size.y * 0.08f));
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y - groundSize.y * 0.5f - 0.02f, 0f);
            Gizmos.DrawWireCube(origin, new Vector3(groundSize.x, groundSize.y, 0f));
            Gizmos.DrawWireCube(origin + Vector3.down * grounddistance * ProbeScale,
                new Vector3(groundSize.x, groundSize.y, 0f));
        }

        float wallReach = walldistance * ProbeScale;
        if (wallcheck1 != null)
            Gizmos.DrawLine(wallcheck1.position, wallcheck1.position + new Vector3(wallReach * facingside, 0f));
        if (wallcheck2 != null)
            Gizmos.DrawLine(wallcheck2.position, wallcheck2.position + new Vector3(wallReach * facingside, 0f));
    }

    public void AnimationTrigger() => stateMachine.currentState?.AnimationTrigger();
    public virtual void EntityDeath() { }
}
*/

private void HandleCollision()//检测角色与地面、墙的碰撞
    {
        isgrounded=Physics2D.Raycast(groundcheck.position,Vector2.down,grounddistance,groundLayer);

        if (wallcheck2 == null)
        {
            iswall=Physics2D.Raycast(wallcheck1.position,Vector2.right*facingside,walldistance,groundLayer);
        }
        else
        {
            iswall=Physics2D.Raycast(wallcheck1.position,Vector2.right*facingside,walldistance,groundLayer)
        && Physics2D.Raycast(wallcheck2.position,Vector2.right*facingside,walldistance,groundLayer);
        }
        
    }
    protected virtual void OnDrawGizmos()//绘制射线，用于调试角色与地面、墙的碰撞检测
    {
        Gizmos.color=Color.red;
        Gizmos.DrawLine(groundcheck.position,groundcheck.position+new Vector3(0,-grounddistance));
        Gizmos.DrawLine(wallcheck1.position,wallcheck1.position+new Vector3(walldistance*facingside,0));

        if(wallcheck2!=null){
        Gizmos.DrawLine(wallcheck2.position,wallcheck2.position+new Vector3(walldistance*facingside,0));
        }
    }

    public void AnimationTrigger() => stateMachine.currentState?.AnimationTrigger();
    public virtual void EntityDeath() { }
}