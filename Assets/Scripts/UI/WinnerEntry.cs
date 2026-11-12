using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinnerEntry : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI statValueText;

    public void Fill(PlayerNetwork player, int statValue, float height)
    {
        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

        float portraitSize = Mathf.Min(50f, height - 8f);
        portrait.rectTransform.sizeDelta = new Vector2(
            portraitSize,
            portraitSize
        );

        portrait.sprite = player.HeroData != null
            ? player.HeroData.portrait
            : null;

        nicknameText.text = string.IsNullOrWhiteSpace(player.PlayerName)
            ? "Player " + (player.OwnerClientId + 1)
            : player.PlayerName;

        statValueText.text = statValue.ToString();
    }
}
