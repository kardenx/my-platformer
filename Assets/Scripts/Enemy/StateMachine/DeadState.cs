public class DeadState : IState
{
    private readonly EnemyController ctx;
    public DeadState(EnemyController ctx) => this.ctx = ctx;

    public void Enter()
    {
        ctx.Stop();
        ctx.Animator.SetTrigger(EnemyController.DieHash);
    }

    public void Tick() { }   // кінець
    public void Exit() { }
}
