using System;
using UnityEngine;

[Serializable]
public class FlagBlinkSettings
{
    [SerializeField] private float blinkSpeed = 2f;
    [SerializeField] private float blinkFrequencyCoefficient = 24f;
    [SerializeField] private float minimumAlpha = 0.15f;
    [SerializeField] private float maximumAlpha = 1f;

    public float BlinkSpeed
    {
        get => blinkSpeed;
        set => blinkSpeed = value;
    }

    public float BlinkFrequencyCoefficient
    {
        get => blinkFrequencyCoefficient;
        set => blinkFrequencyCoefficient = value;
    }

    public float MinimumAlpha
    {
        get => minimumAlpha;
        set => minimumAlpha = value;
    }

    public float MaximumAlpha
    {
        get => maximumAlpha;
        set => maximumAlpha = value;
    }

    public float CalculateBlinkSpeed(float distance)
    {
        float speed = blinkFrequencyCoefficient / Mathf.Max(distance, 0.1f);
        return Mathf.Max(speed, blinkSpeed);
    }

    public float UpdatePhase(float phase, float currentSpeed, float deltaTime)
    {
        return Mathf.Repeat(phase + currentSpeed * deltaTime, 1f);
    }

    public float CalculateAlpha(float phase)
    {
        float pulse = (Mathf.Sin(phase * Mathf.PI * 2f) + 1f) * 0.5f;
        return Mathf.Lerp(minimumAlpha, maximumAlpha, pulse);
    }
}
