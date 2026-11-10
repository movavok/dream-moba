using TMPro;
using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Vector3 worldOffset = new(0f, 0.9f, 0f);

    private static InteractionPrompt instance;
    private Transform promptTarget;

    private void Awake()
    {
        instance = this;

        if (promptText == null)
            throw new MissingReferenceException(
                "InteractionPrompt requires a text object in its prefab."
            );

        promptText.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (promptText == null || promptTarget == null)
            return;

        promptText.transform.position =
            promptTarget.position + worldOffset;
    }

    public static void Show(
        IInteractable interactable,
        string binding)
    {
        EnsureInstance();

        if (instance == null || instance.promptText == null)
            return;

        instance.promptTarget = interactable.InteractionPromptTarget;
        if (instance.promptTarget == null)
            return;

        instance.promptText.text =
            $"[{binding}] - {interactable.InteractionVerb} " +
            interactable.InteractionName;
        instance.promptText.transform.position =
            instance.promptTarget.position + instance.worldOffset;
        instance.promptText.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (instance == null || instance.promptText == null)
            return;

        instance.promptTarget = null;
        instance.promptText.gameObject.SetActive(false);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        InteractionPrompt existing =
            FindAnyObjectByType<InteractionPrompt>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        InteractionPrompt prefab =
            Resources.Load<InteractionPrompt>("InteractionPrompt");

        if (prefab != null)
        {
            instance = Instantiate(prefab);
            return;
        }

        Debug.LogError(
            "InteractionPrompt prefab was not found in Resources."
        );
    }
}
