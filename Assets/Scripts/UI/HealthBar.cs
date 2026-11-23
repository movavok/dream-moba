using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageFill;
    [SerializeField] private TMP_Text healthText;

    [SerializeField] private Image border;
    [SerializeField] private Sprite normalBorder;
    [SerializeField] private Sprite spawnProtectionBorder;

    [Header("Team")]
    [SerializeField] private Image teamIndicator;

    [Header("Streak")]
    [SerializeField] private Image streakIndicator;
    [SerializeField] private TMP_Text streakText;

    [Header("Colors")]
    [SerializeField] private Color ownHealthColor = Color.green;
    [SerializeField] private Color allyHealthColor = Color.cyan;
    [SerializeField] private Color enemyHealthColor = Color.red;

    [Header("Damage Animation")]
    [SerializeField] private float damageSpeed = 5f;

    private Health health;
    private Team team;
    private PlayerStreak streak;
    private PlayerNetwork player;
    private int displayedStreak = int.MinValue;

    private float displayedDamageHealth;

    private void Awake()
    {
        health = GetComponentInParent<Health>();
        team = GetComponentInParent<Team>();
        streak = GetComponentInParent<PlayerStreak>();
        player = GetComponentInParent<PlayerNetwork>();

        if (health == null)
            Debug.LogError("HealthBar: Health not found!");

        if (team == null)
            Debug.LogError("HealthBar: Team not found!");

        if (streak == null && (streakIndicator != null || streakText != null))
            Debug.LogError("HealthBar: PlayerStreak not found!");
    }

    private void Start()
    {
        if (health != null)
        {
            displayedDamageHealth = health.CurrentHealth;

            health.HealthChanged += OnHealthChanged;

            OnHealthChanged(
                health.CurrentHealth,
                health.MaxHealth
            );

            health.SpawnProtectionChanged += OnSpawnProtectionChanged;

            OnSpawnProtectionChanged(
                health.IsSpawnProtected
            );
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.SpawnProtectionChanged -= OnSpawnProtectionChanged;
        }
    }

    private void OnSpawnProtectionChanged(bool protectedState)
    {
        if (border == null)
            return;

        border.sprite =
            protectedState
                ? spawnProtectionBorder
                : normalBorder;
    }

    private void OnHealthChanged(int newHealth, int maxHealth)
    {
        if (maxHealth <= 0)
            return;

        float healthPercent =
            Mathf.Clamp01((float)newHealth / maxHealth);

        if (healthFill != null)
        {
            healthFill.fillAmount = healthPercent;

            healthFill.color = GetHealthColor();
        }

        if (healthText != null)
        {
            healthText.SetText("<color=#ff5252><b>{0}</b></color>", newHealth);
        }
    }

    private void Update()
    {
        UpdateHealth();
        UpdateTeam();
        UpdateStreak();
    }

    private bool IsAlly()
    {
        if (player == null)
            return false;

        if (player.IsOwner)
            return false;

        Team ownTeam = PlayerNetwork.LocalPlayer != null
            ? PlayerNetwork.LocalPlayer.GetComponent<Team>() : null;

        if (ownTeam == null || team == null)
            return false;

        return ownTeam.TeamId.Value == team.TeamId.Value;
    }

    private Color GetHealthColor()
    {
        if (IsOwnPlayer())
            return ownHealthColor;

        if (IsAlly())
            return allyHealthColor;

        return enemyHealthColor;
    }

    private void UpdateHealth()
    {
        if (health == null)
            return;

        float maxHealth = health.MaxHealth;

        if (maxHealth <= 0f)
            return;

        float currentHealth = health.CurrentHealth;

        float healthPercent =
            Mathf.Clamp01(currentHealth / maxHealth);

        if (healthFill != null)
        {
            healthFill.fillAmount = healthPercent;

            healthFill.color = GetHealthColor();
        }

        if (displayedDamageHealth > currentHealth)
        {
            displayedDamageHealth = Mathf.MoveTowards(
                displayedDamageHealth,
                currentHealth,
                maxHealth * (damageSpeed / 100f) * Time.deltaTime
            );
        }
        else
        {
            displayedDamageHealth = currentHealth;
        }

        if (damageFill != null)
        {
            damageFill.fillAmount =
                Mathf.Clamp01(displayedDamageHealth / maxHealth);
        }
    }

    private void UpdateTeam()
    {
        if (team == null || teamIndicator == null)
            return;

        teamIndicator.color = team.TeamColor;
    }

    private void UpdateStreak()
    {
        if (streak == null)
            return;

        int value = streak.Streak;

        if (streakText != null && displayedStreak != value)
        {
            displayedStreak = value;
            streakText.gameObject.SetActive(value > 0);

            if (value > 0)
                streakText.SetText("<color=#fc9025><b>{0}</b></color>", value);
        }

        if (streakIndicator != null)
        {
            if (value > 0)
            {
                float timerMax = streak.StreakTimerMax;

                if (timerMax > 0f)
                {
                    streakIndicator.fillAmount =
                        Mathf.Clamp01(streak.StreakTimer / timerMax);
                }
            }
            else
            {
                streakIndicator.fillAmount = 0f;
            }
        }
    }

    private bool IsOwnPlayer()
    {
        if (player == null)
            return false;

        return player.IsOwner;
    }
}
