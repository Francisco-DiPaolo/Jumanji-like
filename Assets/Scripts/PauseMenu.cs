using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("El panel central del menú de pausa que se activará/desactivará.")]
    public GameObject pausePanel;
    
    [Tooltip("El slider que controla el volumen maestro. Su valor debería estar entre 0.0001 y 1.")]
    public Slider volumeSlider;

    [Header("Audio Settings")]
    [Tooltip("Boost máximo en decibelios (dB). Por ejemplo, 10 permite que el volumen suba por encima del estándar.")]
    public float maxVolumeBoost = 10f; 

    private bool isPaused = false;
    private const string VolumePrefKey = "MasterVolumePref";

    private void Start()
    {
        // Asegurarse de que el menú empiece desactivado
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // Cargar el volumen guardado o establecer un valor por defecto
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 0.75f);

        if (volumeSlider != null)
        {
            // Forzamos el mínimo a un valor muy pequeño para evitar logaritmo de 0
            volumeSlider.minValue = 0.0001f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = savedVolume;

            // Añadir el listener para cuando el jugador mueva el slider
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        // Aplicar el volumen inicial
        SetVolume(savedVolume);
    }

    private void Update()
    {
        // Detectar si se presiona la tecla P
        if (Input.GetKeyDown(KeyCode.P))
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

    public void SetVolume(float sliderValue)
    {
        // Asegurarse de que el valor nunca sea 0 o menor
        sliderValue = Mathf.Max(sliderValue, 0.0001f);

        // Calcular los decibelios deseados basados en el slider
        float dB = Mathf.Log10(sliderValue) * 20f + maxVolumeBoost;

        // Convertir de decibelios a un multiplicador lineal (amplitud)
        // La fórmula es: multiplicador = 10 ^ (dB / 20)
        float linearVolume = Mathf.Pow(10f, dB / 20f);

        // Aplicar al AudioListener: Esto afecta a TODOS los AudioSources de la escena, 
        // pasen o no por el AudioMixer, e incluso a los creados dinámicamente.
        AudioListener.volume = linearVolume;

        // Guardar la preferencia localmente
        PlayerPrefs.SetFloat(VolumePrefKey, sliderValue);
        PlayerPrefs.Save();
    }
}
