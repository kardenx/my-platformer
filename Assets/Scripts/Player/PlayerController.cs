using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Рух")]
    [SerializeField, Range(1f, 20f)]  private float maxSpeed = 8f;
    [SerializeField, Range(5f, 200f)] private float acceleration = 70f;
    [SerializeField, Range(5f, 200f)] private float deceleration = 90f;

    [Header("Земля")]
    [SerializeField] private Transform groundCheck;
    [SerializeField, Range(0.05f, 0.5f)] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Стрибок")]
    [SerializeField, Range(0.5f, 8f)] private float jumpHeight = 3f;
    // Базовий масштаб гравітації: на ньому тримається розрахунок висоти стрибка
    [SerializeField, Range(0.5f, 8f)] private float baseGravity = 3f;
    [SerializeField, Range(1f, 6f)]   private float fallMultiplier = 2.2f;
    [SerializeField, Range(1f, 8f)]   private float lowJumpMultiplier = 3.5f;

    [Header("Прийоми тюнінгу")]
    [SerializeField, Range(0f, 0.4f)] private float coyoteTime = 0.12f;
    [SerializeField, Range(0f, 0.4f)] private float jumpBufferTime = 0.15f;

    [Header("Вигляд")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Зворотний звʼязок")]
    // Через fallMultiplier звичайний стрибок приземляється на ~20 од/с,
    // тому поріг вищий, ніж у методичці (9): трясе лише падіння з висоти
    [SerializeField, Range(1f, 40f)]    private float hardLandingSpeed = 25f;
    [SerializeField] private CameraShake cameraShake;
    [SerializeField, Range(0.05f, 0.3f)] private float shakeDuration = 0.12f;
    [SerializeField, Range(0.02f, 0.3f)] private float shakeStrength = 0.12f;

    [Header("Звук")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] footsteps;   // щонайменше ТРИ кліпи
    [SerializeField, Range(0f, 0.3f)]   private float pitchVariance = 0.12f;
    [SerializeField, Range(0.1f, 0.8f)] private float stepInterval = 0.32f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Пороги "майже нуль", щоб не порівнювати float з нулем напряму
    private const float InputDeadZone = 0.01f;
    private const float RunSoundMinSpeed = 0.5f;

    private Rigidbody2D body;
    private PlayerControls controls;
    private float moveInput;

    private bool isGrounded;
    private bool wasGrounded;
    private float fallSpeed;          // найбільша швидкість падіння перед дотиком

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float stepTimer;

    private float JumpVelocity =>
        Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * baseGravity * jumpHeight);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = baseGravity;
        controls = new PlayerControls();

        // Префаб не може зберігати посилання на обʼєкт сцени, тож шукаємо самі
        if (cameraShake == null) cameraShake = FindAnyObjectByType<CameraShake>();
    }

    // Без Enable введення не приходить, і помилки при цьому не буде
    private void OnEnable()  => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();
    private void OnDestroy() => controls.Dispose();

    private void Update()
    {
        isGrounded = CheckGround();

        // Поповнюємо coyote лише коли стоїмо, а не летимо вгору (інакше подвійний стрибок)
        bool canRefill = isGrounded && body.linearVelocity.y <= InputDeadZone;
        coyoteCounter = canRefill ? coyoteTime : coyoteCounter - Time.deltaTime;

        // Натиск не виконуємо одразу, а запамʼятовуємо в таймер
        jumpBufferCounter = controls.Player.Jump.WasPressedThisFrame()
            ? jumpBufferTime
            : jumpBufferCounter - Time.deltaTime;

        // Стрибок, коли активні ОБИДВА таймери; обнуляємо обидва
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        // Введення читаємо в Update: FixedUpdate може пропустити короткий натиск
        moveInput = controls.Player.Move.ReadValue<Vector2>().x;

        UpdateFootsteps();
        CheckLanding();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // Горизонталь: гальмуємо швидше, ніж розганяємось
        float target = moveInput * maxSpeed;
        float rate = Mathf.Abs(target) > InputDeadZone ? acceleration : deceleration;
        float x = Mathf.MoveTowards(body.linearVelocity.x, target, rate * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(x, body.linearVelocity.y);

        // Змінна висота стрибка через гравітацію, а не обрізання швидкості
        if (body.linearVelocity.y < 0f)
            body.gravityScale = baseGravity * fallMultiplier;          // падаємо швидше
        else if (body.linearVelocity.y > 0f && !controls.Player.Jump.IsPressed())
            body.gravityScale = baseGravity * lowJumpMultiplier;       // відпустили - короткий стрибок
        else
            body.gravityScale = baseGravity;
    }

    // Коло перекриває всю ширину ніг, а не одну точку
    private bool CheckGround() =>
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

    private void Jump()
    {
        // Фіксуємо масштаб ДО імпульсу, інакше злетимо з чужою гравітацією
        body.gravityScale = baseGravity;
        body.linearVelocity = new Vector2(body.linearVelocity.x, JumpVelocity);
    }

    private void CheckLanding()
    {
        if (!isGrounded)
            fallSpeed = Mathf.Max(fallSpeed, -body.linearVelocity.y);

        if (isGrounded && !wasGrounded)   // саме ПЕРЕХІД false → true, а не стан
            OnLanded(fallSpeed);

        if (isGrounded) fallSpeed = 0f;
        wasGrounded = isGrounded;
    }

    private void OnLanded(float speed)
    {
        // Тільки жорстке приземлення. Зіскок зі сходинки ефекту не дає
        if (speed < hardLandingSpeed) return;

        if (cameraShake != null) cameraShake.Shake(shakeDuration, shakeStrength);
    }

    private void UpdateVisuals()
    {
        // Джерело істини - фактична швидкість тіла, а не натиснута клавіша
        if (animator != null)
            animator.SetFloat(SpeedHash, Mathf.Abs(body.linearVelocity.x));

        // flipX, а не мінусовий масштаб: інакше дочірній GroundCheck переїде
        if (spriteRenderer != null && Mathf.Abs(moveInput) > InputDeadZone)
            spriteRenderer.flipX = moveInput < 0f;
    }

    private void UpdateFootsteps()
    {
        bool running = isGrounded && Mathf.Abs(body.linearVelocity.x) > RunSoundMinSpeed;

        // Не біжимо - скидаємо таймер, щоб перший крок після старту був одразу
        if (!running) { stepTimer = 0f; return; }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = stepInterval;
        }
    }

    private void PlayFootstep()
    {
        if (source == null || footsteps == null || footsteps.Length == 0) return;

        // 1) різні кліпи прибирають впізнаваність звуку
        AudioClip clip = footsteps[Random.Range(0, footsteps.Length)];

        // 2) розкид висоти тону прибирає механічність повторення
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.PlayOneShot(clip);
    }

    // Без візуалізації датчик налагоджувати неможливо
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
