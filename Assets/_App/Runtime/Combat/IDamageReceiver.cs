namespace _App
{
    public interface IDamageReceiver
    {
        bool IsAlive { get; }
        bool TryReceiveDamage(DamageInfo damage);
    }
}
