using UnityEngine;
using UnityEngine.Pool;

// Контекст ворога (ctx): тіло, аніматор, датчики, ціль. Стани користуються лише ним
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Дані")]
    [SerializeField] private EnemyStatsSO stats;

    [Header("Точки")]
    [SerializeField] private Transform groundCheck;    // під ступнею
    [SerializeField] private Transform edgeCheck;      // перед носком, НИЖЧЕ ступні
    [SerializeField] private Transform wallCheck;      // перед носком, на рівні тулуба
    [SerializeField] private Transform attackPoint;    // звідки йде удар

    [Header("Шари")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask damageableLayers;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;

    [Header("Стрільба")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField, Range(1, 50)] private int poolCapacity = 20;

    // Імена параметрів аніматора - в одному місці, щоб не помилитися в літері
    public static readonly int SpeedHash     = Animator.StringToHash("Speed");
    public static readonly int TelegraphHash = Animator.StringToHash("Telegraph");
    public static readonly int HurtHash      = Animator.StringToHash("Hurt");
    public static readonly int DieHash       = Animator.StringToHash("Die");

    public EnemyStatsSO Stats => stats;
    public Rigidbody2D  Body     { get; private set; }
    public Animator     Animator { get; private set; }
    public StateMachine Machine  { get; private set; }
    public Transform    Target   { get; private set; }

    public PatrolState PatrolState { get; private set; }
    public ChaseState  ChaseState  { get; private set; }
    public HurtState   HurtState   { get; private set; }
    public DeadState   DeadState   { get; private set; }
    public IState      AttackState { get; private set; }

    public int Facing { get; private set; } = 1;

    // Структурні величини: їх не крутить дизайнер, тому вони не в ассеті
    private const float FacingDeadZone = 0.1f;
    private const float ProbeRadius    = 0.1f;

    private ObjectPool<Projectile> pool;

    private void Awake()
    {
        Body     = GetComponent<Rigidbody2D>();
        Animator = GetComponentInChildren<Animator>();
        Machine  = new StateMachine();

        // Префаб не може зберігати посилання на обʼєкт сцени - гравця шукаємо в коді
        Target = GameObject.FindGameObjectWithTag("Player").transform;

        // Максимум здоровʼя приходить з ассета
        GetComponent<Health>().Configure(stats.maxHealth);

        // Вигляд теж з ассета - тому інший тип ворога не потребує нових спрайтів
        GetComponentInChildren<SpriteRenderer>().color = stats.tint;
        ApplyScale();

        PatrolState = new PatrolState(this);
        ChaseState  = new ChaseState(this);
        HurtState   = new HurtState(this);
        DeadState   = new DeadState(this);

        AttackState = stats.ranged
            ? (IState)new RangedAttackState(this)
            : new MeleeAttackState(this);

        if (projectilePrefab != null)
        {
            pool = new ObjectPool<Projectile>(
                createFunc:      () => Instantiate(projectilePrefab),
                actionOnGet:     p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                defaultCapacity: poolCapacity);
        }
    }

    private void Start()  => Machine.ChangeState(PatrolState);
    private void Update() => Machine.Tick();

    private void OnDestroy() => pool?.Clear();

    // Відстань по горизонталі: висоту окремо перевіряє TargetOnSameLevel
    public float DistanceToTarget =>
        Mathf.Abs(Target.position.x - transform.position.x);

    // Рівень порівнюємо по ступнях, а не по початках координат
    public bool TargetOnSameLevel =>
        Mathf.Abs(Target.position.y - groundCheck.position.y) <= stats.verticalTolerance;

    public void Move(float speed)
    {
        Body.linearVelocity = new Vector2(Facing * speed, Body.linearVelocity.y);
        Animator.SetFloat(SpeedHash, Mathf.Abs(Body.linearVelocity.x));
    }

    public void Stop()
    {
        Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
        Animator.SetFloat(SpeedHash, 0f);
    }

    public void Flip()
    {
        Facing = -Facing;
        ApplyScale();
    }

    // Масштаб і дзеркалення разом: інакше розворот скидав би розмір з ассета
    private void ApplyScale() =>
        transform.localScale = new Vector3(stats.sizeScale * Facing, stats.sizeScale, 1f);

    public void FaceTarget()
    {
        float dx = Target.position.x - transform.position.x;

        // Без мертвої зони ворог під гравцем розвертався б щокадру
        if (Mathf.Abs(dx) < FacingDeadZone) return;

        int dir = dx > 0f ? 1 : -1;
        if (dir != Facing) Flip();
    }

    public bool IsGrounded =>
        Physics2D.OverlapCircle(groundCheck.position, ProbeRadius, groundLayer);

    // Під носком НЕМАЄ землі - попереду край платформи
    public bool AtEdge() =>
        !Physics2D.OverlapCircle(edgeCheck.position, ProbeRadius, groundLayer);

    // Перед носком Є перешкода - попереду стіна
    public bool AtWall() =>
        Physics2D.OverlapCircle(wallCheck.position, ProbeRadius, groundLayer);

    public void PlayTelegraph()
    {
        if (stats.telegraphSfx != null && audioSource != null)
            audioSource.PlayOneShot(stats.telegraphSfx);
    }

    // Удар через інтерфейс: ворог не знає, кого саме б'є
    public void DealDamage()
    {
        Collider2D hit = Physics2D.OverlapCircle(
            attackPoint.position, stats.attackRadius, damageableLayers);

        if (hit != null && hit.TryGetComponent(out IDamageable target))
            target.TakeDamage(stats.damage, transform.position);
    }

    public void Shoot()
    {
        if (pool == null) return;
        Projectile p = pool.Get();

        // Стан скидаємо ЯВНО: перевикористаний снаряд памʼятає старий
        p.transform.position = attackPoint.position;
        p.OnFinished = Release;
        p.Launch(new Vector2(Facing, 0f), stats.damage);
    }

    private void Release(Projectile p)
    {
        p.OnFinished = null;
        pool.Release(p);
    }

    // Радіуси й датчики у вікні Scene
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (attackPoint != null)
            Gizmos.DrawWireSphere(attackPoint.position, stats != null ? stats.attackRadius : 0.5f);

        Gizmos.color = Color.yellow;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, ProbeRadius);
        if (edgeCheck != null)   Gizmos.DrawWireSphere(edgeCheck.position, ProbeRadius);
        if (wallCheck != null)   Gizmos.DrawWireSphere(wallCheck.position, ProbeRadius);
    }
}
