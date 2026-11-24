using UnityEngine;

// Атака стрільця: та сама структура, що й у ближньої, але замість удару - постріл
public class RangedAttackState : IState
{
    private readonly EnemyController ctx;
    private float timer;
    private bool shot;

    public RangedAttackState(EnemyController ctx) => this.ctx = ctx;

    public void Enter()
    {
        ctx.Stop();
        ctx.FaceTarget();
        shot  = false;
        timer = ctx.Stats.telegraphTime;

        ctx.Animator.SetTrigger(EnemyController.TelegraphHash);
        ctx.PlayTelegraph();
    }

    public void Tick()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        if (!shot)
        {
            ctx.Shoot();
            shot  = true;
            timer = ctx.Stats.recoveryTime;
            return;
        }

        ctx.Machine.ChangeState(ctx.ChaseState);
    }

    public void Exit() => ctx.Animator.ResetTrigger(EnemyController.TelegraphHash);
}
