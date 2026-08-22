using UnityEngine;

public class HeroDatabase : MonoBehaviour
{
    public static HeroDatabase Instance;

    [SerializeField] private HeroDefinition[] heroes;

    private void Awake()
    {
        Instance = this;
    }

    public HeroDefinition GetHero(string id)
    {
        foreach (HeroDefinition hero in heroes)
        {
            if (hero != null && hero.id == id)
                return hero;
        }

        Debug.LogError(
            $"HeroDatabase: Hero with id '{id}' not found!"
        );

        return null;
    }
}