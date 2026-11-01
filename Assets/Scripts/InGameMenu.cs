using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameMenu : MonoBehaviour
{
    public CanvasGroup panelGroup;
    public Slider volumeSlider;
    public string mainMenuSceneName = "MainMenu";

    const string VolumeKey = "MasterVolume";
    bool isOpen;
    CursorLockMode savedLockState = CursorLockMode.Locked;
    bool savedCursorVisible = false;

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
        SetOpen(false, false);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            Toggle();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
#endif
    }

    public void Toggle() { SetOpen(!isOpen, true); }
    public void Open() { SetOpen(true, true); }
    public void Close() { SetOpen(false, true); }

    void SetOpen(bool show, bool changeTime)
    {
        if (show && !isOpen)
        {
            savedLockState = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
        }
        isOpen = show;
        if (panelGroup != null)
        {
            panelGroup.alpha = show ? 1f : 0f;
            panelGroup.interactable = show;
            panelGroup.blocksRaycasts = show;
        }
        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (changeTime)
        {
            Cursor.lockState = savedLockState;
            Cursor.visible = savedCursorVisible;
        }
        if (changeTime) Time.timeScale = show ? 0f : 1f;
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }
}
