using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageFill;
    [SerializeField] private TMP_Text healthText;

    [Header("Team")]
    [SerializeField] private Image teamIndicator;

    [Header("Streak")]
    [SerializeField] private Image streakIndicator;
    [SerializeField] private TMP_Text streakText;

    [Header("Colors")]
    [SerializeField] private Color ownHealthColor = Color.green;
    [SerializeField] private Color allyHealthColor = Color.cyan;
    [SerializeField] private Color enemyHealthColor = Color.red;

    [SerializeField] private Color team1Color = Color.blue;
    [SerializeField] private Color team2Color = Color.red;
    [SerializeField] private Color team3Color = Color.yellow;

    [Header("Damage Animation")]
    [SerializeField] private float damageSpeed = 5f;

    private Health health;
    private Team team;
    private PlayerStreak streak;

    private float displayedDamageHealth;

    private void Awake()
    {
        health = GetComponentInParent<Health>();
        team = GetComponentInParent<Team>();
        streak = GetComponentInParent<PlayerStreak>();

        if (health == null)
            Debug.LogError("HealthBar: Health not found!");

        if (team == null)
            Debug.LogError("HealthBar: Team not found!");

        if (streak == null)
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
        }
    }

    private void OnDestroy()
    {
        if (health != null)
            health.HealthChanged -= OnHealthChanged;
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
            healthText.text =
                $"<color=#FF8A8A><b>{newHealth}</b></color>";
        }
    }

    private void Update()
    {
        UpdateHealth();
        UpdateTeam();
        UpdateStreak();
    }

    private Team FindLocalPlayerTeam()
    {
        PlayerNetwork[] players =
            FindObjectsByType<PlayerNetwork>();

        foreach (PlayerNetwork player in players)
        {
            if (player.IsOwner)
                return player.GetComponent<Team>();
        }

        return null;
    }

    private bool IsAlly()
    {
        PlayerNetwork player = GetComponentInParent<PlayerNetwork>();

        if (player == null)
            return false;

        if (player.IsOwner)
            return false;

        Team ownTeam = FindLocalPlayerTeam();

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
                damageSpeed * Time.deltaTime
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

        if (healthText != null)
        {
            healthText.text =
                $"<color=#ff5252><b>{health.CurrentHealth}</b></color>";
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

        if (streakText != null)
        {
            streakText.gameObject.SetActive(value > 0);

            if (value > 0)
                streakText.text = $"<color=#fc9025><b>{value}</b></color>";
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
        PlayerNetwork player = GetComponentInParent<PlayerNetwork>();

        if (player == null)
            return false;

        return player.IsOwner;
    }
}