using UnityEngine;

public class BuildingSortingTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("HeroSorting"))
            return;

        HeroSorting hero = other.GetComponentInParent<HeroSorting>();

        if (hero == null)
            return;

        hero.SetBehind(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("HeroSorting"))
            return;

        HeroSorting hero = other.GetComponentInParent<HeroSorting>();

        if (hero == null)
            return;

        hero.SetBehind(false);
    }
}