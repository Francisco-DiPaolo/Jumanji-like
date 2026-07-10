using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("El panel central del menú de pausa que se activará/desactivará.")]
    public GameObject pausePanel;
    
    [Tooltip("El slider que controla el volumen maestro.")]
    public Slider volumeSlider;

    [Tooltip("El slider que controla el volumen de la voz (Voice).")]
    public Slider voiceSlider;

    [Tooltip("El slider que controla la sensibilidad del ratón.")]
    public Slider sensitivitySlider;

    [Header("Audio Settings")]
    [Tooltip("El AudioMixer principal que contiene los grupos de audio.")]
    public AudioMixer mainAudioMixer;

    
    [Tooltip("Nombre del parámetro expuesto en el AudioMixer para Voice.")]
    public string voiceExposedParam = "VoiceVolume";

    [Tooltip("Boost máximo en decibelios (dB). Por ejemplo, 10 permite que el volumen suba por encima del estándar.")]
    public float maxVolumeBoost = 10f; 

    private bool isPaused = false;
    private const string MasterVolumePrefKey = "MasterVolumePref";
    private const string VoiceVolumePrefKey = "VoiceVolumePref";
    private const string SensitivityPrefKey = "MouseSensitivityPref";

    private void Start()
    {
        // Asegurarse de que el menú empiece desactivado
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // Cargar los volúmenes y ajustes guardados o establecer un valor por defecto
        float savedMasterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, 0.75f);
        float savedVoiceVolume = PlayerPrefs.GetFloat(VoiceVolumePrefKey, 0.75f);
        float savedSensitivity = PlayerPrefs.GetFloat(SensitivityPrefKey, 1.0f);

        // Inicializar Slider Master
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0.0001f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = savedMasterVolume;
            volumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        // Inicializar Slider Voice
        if (voiceSlider != null)
        {
            voiceSlider.minValue = 0.0001f;
            voiceSlider.maxValue = 1f;
            voiceSlider.value = savedVoiceVolume;
            voiceSlider.onValueChanged.AddListener(SetVoiceVolume);
        }

        // Inicializar Slider Sensibilidad
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 5f; // Un rango de 0.1 a 5 veces la sensibilidad base
            sensitivitySlider.value = savedSensitivity;
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        }

        // Aplicar los ajustes iniciales
        SetMasterVolume(savedMasterVolume);
        SetVoiceVolume(savedVoiceVolume);
        SetSensitivity(savedSensitivity);
    }

    private void Update()
    {
        // Detectar si se presiona la tecla F12
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        // Pausa local: Detiene el tiempo del juego en esta instancia (físicas, Update, etc.)
        Time.timeScale = 0f;

        // Liberar y mostrar el cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // Reanudar el tiempo del juego
        Time.timeScale = 1f;

        // Bloquear y ocultar el cursor nuevamente
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetMasterVolume(float sliderValue)
    {
        sliderValue = Mathf.Max(sliderValue, 0.0001f);
        float dB = Mathf.Log10(sliderValue) * 20f + maxVolumeBoost;
        float linearVolume = Mathf.Pow(10f, dB / 20f);

        AudioListener.volume = linearVolume;

        PlayerPrefs.SetFloat(MasterVolumePrefKey, sliderValue);
        PlayerPrefs.Save();
    }

    public void SetVoiceVolume(float sliderValue)
    {
        sliderValue = Mathf.Max(sliderValue, 0.0001f);
        float dB = Mathf.Log10(sliderValue) * 20f + maxVolumeBoost;

        if (mainAudioMixer != null)
        {
            mainAudioMixer.SetFloat(voiceExposedParam, dB);
        }

        PlayerPrefs.SetFloat(VoiceVolumePrefKey, sliderValue);
        PlayerPrefs.Save();
    }

    public void SetSensitivity(float sliderValue)
    {
        // Aplicarlo directamente al jugador local si existe
        if (PlayerMovement.Local != null)
        {
            PlayerMovement.Local.mouseSensitivityX = sliderValue;
            PlayerMovement.Local.mouseSensitivityY = sliderValue;
        }

        // Guardarlo para futuras partidas o cuando el jugador respawnee
        PlayerPrefs.SetFloat(SensitivityPrefKey, sliderValue);
        PlayerPrefs.Save();
    }
}
