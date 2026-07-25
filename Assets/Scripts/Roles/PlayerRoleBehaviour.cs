public abstract class PlayerRoleBehaviour
{
    public abstract PlayerRole RoleId { get; }
    public bool IsKiller { get; set; }
    protected NetworkPlayerEntity Owner { get; private set; }
    public virtual void Subscribe(NetworkPlayerEntity owner)
    {
        Owner = owner;
    }
    public virtual void Unsubscribe() { }
    public virtual void RoleAbility(NetworkPlayerEntity self) { }
}