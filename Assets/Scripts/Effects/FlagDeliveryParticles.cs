using UnityEngine;

public class FlagDeliveryParticles : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;

    private void Awake()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
    }

    public void Play(Vector3 worldPosition, Color teamColor)
    {
        if (particles == null)
            return;

        transform.position = worldPosition;

        var main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(
            CreateColorGradient(teamColor)
        );

        particles.Clear();
        particles.Play();
    }

    private static Gradient CreateColorGradient(Color teamColor)
    {
        Color.RGBToHSV(teamColor, out float hue, out float saturation, out float value);

        float hueOffset = Random.Range(-0.025f, 0.025f);
        float saturationOffset = Random.Range(-0.08f, 0.08f);
        float valueOffset = Random.Range(-0.05f, 0.08f);
        Color variation = Color.HSVToRGB(
            Mathf.Repeat(hue + hueOffset, 1f),
            Mathf.Clamp01(saturation + saturationOffset),
            Mathf.Clamp01(value + valueOffset)
        );

        Color brightTeamColor = Color.Lerp(teamColor, Color.white, 0.2f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(brightTeamColor, 0.35f),
                new GradientColorKey(variation, 0.75f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.35f),
                new GradientAlphaKey(0.9f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }
}
