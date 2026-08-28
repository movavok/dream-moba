using UnityEngine;
using UnityEngine.UI;

public class VolumeUI : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Animator animator;

    [SerializeField] private UnityEngine.UI.Image volumeIcon;

    [SerializeField] private Sprite volumeOnSprite;
    [SerializeField] private Sprite volumeMutedSprite;

    private float previousVolume = 0.5f;

    private PlayerNetwork playerNetwork;

    private static readonly int Open = Animator.StringToHash("Open");

    private bool isOpen;

    private void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (AudioListener.volume > 0f)
            previousVolume = AudioListener.volume;

        UpdateVolumeIcon();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (PlayerNetwork.LocalPlayer != null)
        {
            playerNetwork = PlayerNetwork.LocalPlayer;
            playerNetwork.VolumePressed += Toggle;
        }

        PlayerNetwork.LocalPlayerSpawned += OnLocalPlayerSpawned;
    }

    private void OnLocalPlayerSpawned(PlayerNetwork player)
    {
        playerNetwork = player;
        playerNetwork.VolumePressed += Toggle;
    }

    private void Toggle()
    {
        isOpen = !isOpen;

        if (animator != null)
            animator.SetBool(Open, isOpen);
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;

        if (value > 0f)
            previousVolume = value;

        UpdateVolumeIcon();
    }

    public void ToggleMute()
    {
        if (AudioListener.volume > 0f)
        {
            previousVolume = AudioListener.volume;

            AudioListener.volume = 0f;
            volumeSlider.value = 0f;
        }
        else
        {
            AudioListener.volume = previousVolume;

            volumeSlider.value = previousVolume;
        }

        UpdateVolumeIcon();
    }

    private void UpdateVolumeIcon()
    {
        if (volumeIcon == null)
            return;

        volumeIcon.sprite =
            AudioListener.volume <= 0f
                ? volumeMutedSprite
                : volumeOnSprite;
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(SetVolume);

        if (playerNetwork != null)
            playerNetwork.VolumePressed -= Toggle;

        PlayerNetwork.LocalPlayerSpawned -= OnLocalPlayerSpawned;
    }
}