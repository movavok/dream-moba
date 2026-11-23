using UnityEngine;

public class BuildingSortingTrigger : MonoBehaviour
{
    private void CheckSorting(Collider2D other, bool behind)
    {
        ItemSorting item = other.GetComponentInParent<ItemSorting>();
        if (item != null)
        {
            item.SetBehind(behind);
            return;
        }

        if (other.CompareTag("HeroSorting"))
        {
            HeroSorting hero = other.GetComponentInParent<HeroSorting>();

            if (hero != null)
            {
                BoarAI boar = other.GetComponentInParent<BoarAI>();
                if (boar != null) boar.SetBehind(behind);
                else hero.SetBehind(behind);
            }

            return;
        }

        if (other.CompareTag("ProjectileSorting"))
        {
            ProjectileSorting projectile =
                other.GetComponentInParent<ProjectileSorting>();

            if (projectile != null)
                projectile.SetBehind(behind);

            return;
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckSorting(other, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        CheckSorting(other, false);
    }
}
