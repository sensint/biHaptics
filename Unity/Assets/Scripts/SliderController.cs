using UnityEngine;
using UnityEngine.UI;
using TMPro; // for TextMeshPro

public class SliderController : MonoBehaviour
{
    public Exp2_v1 exp2; // Reference to your big script

    [System.Serializable]
    public class SliderWithLabels
    {
        public Slider slider;
        public TextMeshProUGUI minLabel;
        public TextMeshProUGUI maxLabel;
        public TextMeshProUGUI currentValueLabel;
    }

    [Header("Sliders with Labels")]
    public SliderWithLabels amplitude;
    public SliderWithLabels delay;
    public SliderWithLabels grain;
    public SliderWithLabels frequency;

    private void Start()
    {
        // Setup ranges + labels
        SetupSlider(amplitude, 0f, 100f, exp2.amplitude);
        SetupSlider(delay, 0f, 500f, exp2.delayMs);
        SetupSlider(grain, 0f, 100f, exp2.grains);
        SetupSlider(frequency, 20f, 300f, exp2.vibrationFrequency);

        // Add listeners
        amplitude.slider.onValueChanged.AddListener(UpdateAmplitude);
        delay.slider.onValueChanged.AddListener(UpdateDelay);
        grain.slider.onValueChanged.AddListener(UpdateGrain);
        frequency.slider.onValueChanged.AddListener(UpdateFrequency);
    }

    private void SetupSlider(SliderWithLabels s, float min, float max, float initial)
    {
        s.slider.minValue = min;
        s.slider.maxValue = max;
        s.slider.value = initial;

        s.minLabel.text = min.ToString("F0");
        s.maxLabel.text = max.ToString("F0");
        s.currentValueLabel.text = initial.ToString("F2");
        s.currentValueLabel.color = Color.red;
    }

    private void UpdateAmplitude(float value)
    {
        exp2.amplitude = value;
        exp2.currentCrosstalkType = Exp2_v1.CrosstalkType.Amplitude;
        amplitude.currentValueLabel.text = value.ToString("F2");
    }

    private void UpdateDelay(float value)
    {
        exp2.delayMs = value;
        exp2.currentCrosstalkType = Exp2_v1.CrosstalkType.Delay;
        delay.currentValueLabel.text = value.ToString("F0") + " ms";
    }

    private void UpdateGrain(float value)
    {
        int grainVal = Mathf.RoundToInt(value);
        exp2.grains = grainVal;
        exp2.currentCrosstalkType = Exp2_v1.CrosstalkType.Grain;
        grain.currentValueLabel.text = grainVal.ToString();
    }

    private void UpdateFrequency(float value)
    {
        exp2.vibrationFrequency = value;
        exp2.currentCrosstalkType = Exp2_v1.CrosstalkType.Frequency;
        frequency.currentValueLabel.text = value.ToString("F1") + " Hz";
    }
}
