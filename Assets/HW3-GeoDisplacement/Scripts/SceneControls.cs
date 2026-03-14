using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SceneControls : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private int startSeed = 123456;
    [SerializeField] private bool playOnStart = true;

    [Header("References")]
    [SerializeField] private Slider TimeSlider;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private TextMeshProUGUI playPauseButtonText;
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private Button randomizeSeedButton;
    [SerializeField] private Toggle loopToggle;

    public event System.Action<float, int> OnTimeOrSeedChanged;

    private bool isPlaying = false;
    private bool loop = true;
    private float elapsedTime = 0f;
    private int currentSeed;

    private bool _suppressSliderCallback = false;
    private float _lastFiredTime = float.NaN;
    private int _lastFiredSeed;

    private void Start()
    {
        currentSeed = startSeed;
        seedInputField.text = currentSeed.ToString();
        TimeSlider.value = 0f;
        loopToggle.isOn = loop;
        FireIfChanged();

        if (playOnStart)
        {
            TogglePlayPause();
        }
    }

    private void OnEnable()
    {
        TimeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        playPauseButton.onClick.AddListener(TogglePlayPause);
        seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
        randomizeSeedButton.onClick.AddListener(RandomizeSeed);
        loopToggle.onValueChanged.AddListener(OnLoopToggleChanged);
    }

    private void OnDisable()
    {
        TimeSlider.onValueChanged.RemoveListener(OnTimeSliderChanged);
        playPauseButton.onClick.RemoveListener(TogglePlayPause);
        seedInputField.onEndEdit.RemoveListener(OnSeedInputChanged);
        randomizeSeedButton.onClick.RemoveListener(RandomizeSeed);
        loopToggle.onValueChanged.RemoveListener(OnLoopToggleChanged);
    }

    private void Update()
    {
        if (isPlaying)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime > duration)
            {
                if (loop)
                {
                    elapsedTime = 0f;
                }
                else
                {
                    elapsedTime = duration;
                    isPlaying = false;
                    playPauseButtonText.text = "Play";
                }
            }
            _suppressSliderCallback = true;
            TimeSlider.value = elapsedTime / duration;
            _suppressSliderCallback = false;
            FireIfChanged();
        }
    }

    private void OnLoopToggleChanged(bool value)
    {
        loop = value;
    }

    private void OnTimeSliderChanged(float value)
    {
        elapsedTime = value * duration;
        if (!_suppressSliderCallback)
            FireIfChanged();
    }

    private void TogglePlayPause()
    {
        isPlaying = !isPlaying;
        playPauseButtonText.text = isPlaying ? "||" : ">";
    }

    private void OnSeedInputChanged(string input)
    {
        if (int.TryParse(input, out int newSeed))
        {
            currentSeed = newSeed;
            FireIfChanged();
        }
        else
        {
            seedInputField.text = currentSeed.ToString();
        }
    }

    private void RandomizeSeed()
    {
        currentSeed = Random.Range(int.MinValue, int.MaxValue);
        seedInputField.text = currentSeed.ToString();
        FireIfChanged();
    }

    private void FireIfChanged()
    {
        if (elapsedTime != _lastFiredTime || currentSeed != _lastFiredSeed)
        {
            _lastFiredTime = elapsedTime;
            _lastFiredSeed = currentSeed;
            OnTimeOrSeedChanged?.Invoke(elapsedTime, currentSeed);
        }
    }
}
