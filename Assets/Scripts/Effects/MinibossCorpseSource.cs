using UnityEngine;

// The ID identifies which Sprite Library the shared corpse prefab will display.
public class MinibossCorpseSource : MonoBehaviour
{
    [SerializeField] private string corpseVisualId = "scarecrow";

    public string CorpseVisualId => corpseVisualId;
}
