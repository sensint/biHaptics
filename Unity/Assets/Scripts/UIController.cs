using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connects UI Sliders to control the parameters of the HapticInteractionManager script.
/// </summary>
public class UIController : MonoBehaviour
{
    public HapticInteractionManager hapticManager;

    [System.Serializable]
    public class SliderBinding
    {
        public Slider slider;
        public TextMeshProUGUI currentValueLabel;
    }

    [Header("UI Bindings")]
    public SliderBinding dominantAmplitude;
    public SliderBinding dominantFrequency;
    public SliderBinding grains;
    public SliderBinding amplitudeMultiplier;
    public SliderBinding frequencyMultiplier;
    public SliderBinding grainMultiplier; // NEW
    public SliderBinding delay;

    void Start()
    {
        if (hapticManager == null)
        {
            Debug.LogError("HapticInteractionManager reference not set in UIController!");
            return;
        }

        // Initialize sliders with updated ranges and values
        SetupSlider(dominantAmplitude, 0f, 1f, hapticManager.dominantAmplitude, "{0:F2}");
        SetupSlider(dominantFrequency, 80f, 200f, hapticManager.dominantFrequency, "{0:F0} Hz");
        SetupSlider(grains, 0f, 400f, hapticManager.grains, "{0:F0}");
        SetupSlider(amplitudeMultiplier, 0f, 1f, hapticManager.amplitudeMultiplier, "x{0:F2}");
        SetupSlider(frequencyMultiplier, 0f, 1f, hapticManager.frequencyMultiplier, "x{0:F2}");
        SetupSlider(grainMultiplier, 0f, 1f, hapticManager.grainMultiplier, "x{0:F2}"); // NEW
        SetupSlider(delay, 0f, 500f, hapticManager.delayMs, "{0:F0} ms");

        // Add listeners to update the haptic manager
        dominantAmplitude.slider.onValueChanged.AddListener(UpdateDominantAmplitude);
        dominantFrequency.slider.onValueChanged.AddListener(UpdateDominantFrequency);
        grains.slider.onValueChanged.AddListener(UpdateGrains);
        amplitudeMultiplier.slider.onValueChanged.AddListener(UpdateAmplitudeMultiplier);
        frequencyMultiplier.slider.onValueChanged.AddListener(UpdateFrequencyMultiplier);
        grainMultiplier.slider.onValueChanged.AddListener(UpdateGrainMultiplier); // NEW
        delay.slider.onValueChanged.AddListener(UpdateDelay);
    }

    private void SetupSlider(SliderBinding binding, float min, float max, float initialValue, string format)
    {
        binding.slider.minValue = min;
        binding.slider.maxValue = max;
        binding.slider.value = initialValue;
        binding.currentValueLabel.text = string.Format(format, initialValue);
    }

    // --- Public methods to be called by slider events ---
    public void UpdateDominantAmplitude(float value)
    {
        hapticManager.dominantAmplitude = value;
        dominantAmplitude.currentValueLabel.text = string.Format("{0:F2}", value);
    }

    public void UpdateDominantFrequency(float value)
    {
        hapticManager.dominantFrequency = value;
        dominantFrequency.currentValueLabel.text = string.Format("{0:F0} Hz", value);
    }

    public void UpdateGrains(float value)
    {
        int intValue = Mathf.RoundToInt(value);
        hapticManager.grains = intValue;
        grains.currentValueLabel.text = string.Format("{0:F0}", intValue);
    }

    public void UpdateAmplitudeMultiplier(float value)
    {
        hapticManager.amplitudeMultiplier = value;
        amplitudeMultiplier.currentValueLabel.text = string.Format("x{0:F2}", value);
    }

    public void UpdateFrequencyMultiplier(float value)
    {
        hapticManager.frequencyMultiplier = value;
        frequencyMultiplier.currentValueLabel.text = string.Format("x{0:F2}", value);
    }

    public void UpdateGrainMultiplier(float value)
    {
        hapticManager.grainMultiplier = value;
        grainMultiplier.currentValueLabel.text = string.Format("x{0:F2}", value);
    }

    public void UpdateDelay(float value)
    {
        hapticManager.delayMs = value;
        delay.currentValueLabel.text = string.Format("{0:F0} ms", value);
    }
}