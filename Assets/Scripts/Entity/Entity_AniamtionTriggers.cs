using UnityEngine;

/// <summary>Routes model animation events to the owning player or enemy.</summary>
public class Entity_AniamtionTriggers : MonoBehaviour
{
    private Entity entity;
    private Entity_Combat enemyCombat;
    private Role role;
    private FlyingEyeController flyingEye;

    private void Awake()
    {
        entity = GetComponentInParent<Entity>();
        role = entity as Role;
        flyingEye = entity as FlyingEyeController;
        if (role == null)
            enemyCombat = GetComponentInParent<Entity_Combat>();
    }

    public void CurrentStateTrigger() => entity?.AnimationTrigger();

    private void AttackTrigger()
    {
        if (role != null)
            role.Attack();
        else
            enemyCombat?.Attack();
    }

    private void ThrowTrigger() => role?.FireKunai();
    private void FlyingEyeShotRelease() => flyingEye?.ReleaseShot();
    private void FlyingEyeStateComplete(string state) => flyingEye?.CompleteAnimation(state);
    private void GroundMobStateComplete(string state) => (entity as GroundMobController)?.CompleteAnimation(state);
}
