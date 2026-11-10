using Unity.Netcode;
using UnityEngine;

public class TrainingDummy : NetworkBehaviour
{
    [SerializeField] private int healAmount = 10;
    [SerializeField] private float healInterval = 0.1f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite hitSprite;
    [SerializeField] private float hitSpriteDuration = 0.04f;

    private Health health;
    private float healTimer;
    private int previousHealth;
    private Coroutine hitSpriteCoroutine;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && defaultSprite != null)
            spriteRenderer.sprite = defaultSprite;
    }

    private void Start()
    {
        if (health == null)
            return;

        previousHealth = health.CurrentHealth;
        health.HealthChanged += OnHealthChanged;
    }

    private new void OnDestroy()
    {
        if (health != null)
            health.HealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        if (!IsServer || health == null || healInterval <= 0f)
            return;

        healTimer += Time.deltaTime;

        if (healTimer < healInterval)
            return;

        healTimer -= healInterval;
        health.Heal(healAmount, true);
    }

    private void OnHealthChanged(int newHealth, int maxHealth)
    {
        if (newHealth < previousHealth)
            ShowHitSprite();

        previousHealth = newHealth;
    }

    private void ShowHitSprite()
    {
        if (spriteRenderer == null || hitSprite == null)
            return;

        if (hitSpriteCoroutine != null)
            StopCoroutine(hitSpriteCoroutine);

        hitSpriteCoroutine = StartCoroutine(HitSpriteRoutine());
    }

    private System.Collections.IEnumerator HitSpriteRoutine()
    {
        Sprite previousSprite = spriteRenderer.sprite;
        spriteRenderer.sprite = hitSprite;

        yield return new WaitForSeconds(hitSpriteDuration);

        spriteRenderer.sprite =
            defaultSprite != null ? defaultSprite : previousSprite;

        hitSpriteCoroutine = null;
    }
}
