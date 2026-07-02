using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneLoadingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SceneObjectActivator objectActivator;
    [SerializeField] GameObject loadingPanel;
    [SerializeField] GameObject loginPanel;
    [SerializeField] Slider progressBar;
    [SerializeField] TextMeshProUGUI loadingLabel;

    [Header("Settings")]
    [SerializeField] string loadingText = "Cargando...";
    [SerializeField] string doneText = "¡Listo!";

    void Awake()
    {
        loginPanel.SetActive(false);
        loadingPanel.SetActive(true);

        if (loadingLabel != null)
            loadingLabel.text = loadingText;

        if (progressBar != null)
            progressBar.value = 0f;
    }

    void Start()
    {
        objectActivator.OnAllObjectsActivated += OnLoadingComplete;
    }

    void Update()
    {
        if (progressBar != null && !objectActivator.IsComplete)
            progressBar.value = objectActivator.Progress;
    }

    void OnLoadingComplete()
    {
        if (progressBar != null)
            progressBar.value = 1f;

        if (loadingLabel != null)
            loadingLabel.text = doneText;

        loadingPanel.SetActive(false);
        loginPanel.SetActive(true);
    }

    void OnDestroy()
    {
        if (objectActivator != null)
            objectActivator.OnAllObjectsActivated -= OnLoadingComplete;
    }
}
