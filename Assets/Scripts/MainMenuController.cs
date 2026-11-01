using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public Slider volumeSlider;
    public string gameSceneName = "MainGame";

    const string VolumeKey = "MasterVolume";

    void Start()
    {
        float v = PlayerPrefs.GetFloat(VolumeKey, 1f);
        AudioListener.volume = v;
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = v;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        ShowMain();
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ShowMain()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }
}
