using Unity.Cinemachine;
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
    // від неї рахується висота
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
    // з 9 трясло на кожному стрибку
    [SerializeField, Range(1f, 40f)]    private float hardLandingSpeed = 25f;
    // тряска через імпульс, корутина вже не працює
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField, Range(0.05f, 2f)] private float impulseForce = 0.3f;

    [Header("Звук")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] footsteps;   // мін 3
    [SerializeField, Range(0f, 0.3f)]   private float pitchVariance = 0.12f;
    [SerializeField, Range(0.1f, 0.8f)] private float stepInterval = 0.32f;

    [Header("Взаємодія")]
    [SerializeField] private Transform interactPoint;
    [SerializeField, Range(0.2f, 1.5f)] private float interactRadius = 0.6f;
    [SerializeField] private LayerMask interactableLayer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private const float InputDeadZone = 0.01f;
    private const float RunSoundMinSpeed = 0.5f;

    private Rigidbody2D body;
    private PlayerControls controls;
    private float moveInput;

    private bool isGrounded;
    private bool wasGrounded;
    private float fallSpeed;

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float stepTimer;
    private float stunnedUntil;

    private bool bouncing;

    // щоб відкидання було видно
    public void Stun(float duration) => stunnedUntil = Time.time + duration;

    // відскок від ворога, low jump його не гасить
    public void Bounce(float velocity)
    {
        bouncing = true;
        body.gravityScale = baseGravity;
        body.linearVelocity = new Vector2(body.linearVelocity.x, velocity);
    }

    private float JumpVelocity =>
        Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * baseGravity * jumpHeight);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = baseGravity;
        controls = new PlayerControls();

        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // без enable інпут мовчить
    private void OnEnable()  => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();
    private void OnDestroy() => controls.Dispose();

    private void Update()
    {
        isGrounded = CheckGround();

        // інакше подвійний стрибок
        bool canRefill = isGrounded && body.linearVelocity.y <= InputDeadZone;
        coyoteCounter = canRefill ? coyoteTime : coyoteCounter - Time.deltaTime;

        // буфер стрибка
        jumpBufferCounter = controls.Player.Jump.WasPressedThisFrame()
            ? jumpBufferTime
            : jumpBufferCounter - Time.deltaTime;

        // обидва таймери, обидва в нуль
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        moveInput = controls.Player.Move.ReadValue<Vector2>().x;

        if (controls.Player.Interact.WasPressedThisFrame())
            TryInteract();

        UpdateFootsteps();
        CheckLanding();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // гальмування швидше за розгін
        if (Time.time >= stunnedUntil)
        {
            float target = moveInput * maxSpeed;
            float rate = Mathf.Abs(target) > InputDeadZone ? acceleration : deceleration;
            float x = Mathf.MoveTowards(body.linearVelocity.x, target, rate * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(x, body.linearVelocity.y);
        }

        if (body.linearVelocity.y <= 0f) bouncing = false;

        // змінна висота через гравітацію
        if (body.linearVelocity.y < 0f)
            body.gravityScale = baseGravity * fallMultiplier;
        else if (body.linearVelocity.y > 0f && !controls.Player.Jump.IsPressed() && !bouncing)
            body.gravityScale = baseGravity * lowJumpMultiplier;   // відпустили - нижче
        else
            body.gravityScale = baseGravity;
    }

    // коло, не промінь
    private bool CheckGround() =>
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

    private void Jump()
    {
        // спершу гравітація
        body.gravityScale = baseGravity;
        body.linearVelocity = new Vector2(body.linearVelocity.x, JumpVelocity);
    }

    private void TryInteract()
    {
        if (interactPoint == null) return;

        Collider2D hit = Physics2D.OverlapCircle(
            interactPoint.position, interactRadius, interactableLayer);

        if (hit != null && hit.TryGetComponent(out IInteractable target))
            target.Interact();
    }

    private void CheckLanding()
    {
        if (!isGrounded)
            fallSpeed = Mathf.Max(fallSpeed, -body.linearVelocity.y);

        if (isGrounded && !wasGrounded)   // саме момент приземлення
            OnLanded(fallSpeed);

        if (isGrounded) fallSpeed = 0f;
        wasGrounded = isGrounded;
    }

    private void OnLanded(float speed)
    {
        // тільки жорстке
        if (speed < hardLandingSpeed) return;

        if (impulseSource != null) impulseSource.GenerateImpulseWithForce(impulseForce);
    }

    private void UpdateVisuals()
    {
        // по швидкості, не по клавіші
        if (animator != null)
            animator.SetFloat(SpeedHash, Mathf.Abs(body.linearVelocity.x));

        // flipX щоб groundcheck не їздив
        if (spriteRenderer != null && Mathf.Abs(moveInput) > InputDeadZone)
            spriteRenderer.flipX = moveInput < 0f;
    }

    private void UpdateFootsteps()
    {
        bool running = isGrounded && Mathf.Abs(body.linearVelocity.x) > RunSoundMinSpeed;

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

        // рандомний кліп
        AudioClip clip = footsteps[Random.Range(0, footsteps.Length)];

        // і трохи тон
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (interactPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(interactPoint.position, interactRadius);
        }
    }
}
