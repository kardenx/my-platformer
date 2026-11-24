using UnityEngine;

public class ChaseState : IState
{
    private readonly EnemyController ctx;
    private float lostTimer;

    public ChaseState(EnemyController ctx) => this.ctx = ctx;

    public void Enter() => lostTimer = 0f;

    public void Tick()
    {
        float d = ctx.DistanceToTarget;

        // загубили - далеко або не на тому рівні
        // гістерезис
        bool lost = d > ctx.Stats.chaseRange * ctx.Stats.chaseExitFactor
                    || !ctx.TargetOnSameLevel;

        if (lost)
        {
            // трохи ще біжимо
            lostTimer += Time.deltaTime;

            if (lostTimer >= ctx.Stats.loseTargetTime)
            {
                ctx.Machine.ChangeState(ctx.PatrolState);
                return;
            }
        }
        else
        {
            lostTimer = 0f;
        }

        ctx.FaceTarget();

        // не впритул і не в стіну
        bool canAdvance = d > ctx.Stats.stopDistance && !ctx.AtWall() && !ctx.AtEdge();

        if (canAdvance) ctx.Move(ctx.Stats.chaseSpeed);
        else            ctx.Stop();

        if (d <= ctx.Stats.attackRange && ctx.TargetOnSameLevel)
            ctx.Machine.ChangeState(ctx.AttackState);
    }

    public void Exit() => ctx.Stop();
}
