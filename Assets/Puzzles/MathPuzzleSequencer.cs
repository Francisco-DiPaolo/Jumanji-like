using UnityEngine;

public class MathPuzzleSequencer : MonoBehaviour
{
    [SerializeField] private GameObject[] cartelesFases;
    
    private int currentIndex = 0;

    private void Start()
    {
        InitializePosters();
    }

    private void InitializePosters()
    {
        if (cartelesFases == null || cartelesFases.Length == 0)
        {
            Debug.LogWarning("[MathPuzzleSequencer]: El array cartelesFases está vacío o no asignado en " + gameObject.name);
            return;
        }

        for (int i = 0; i < cartelesFases.Length; i++)
        {
            if (cartelesFases[i] != null)
            {
                cartelesFases[i].SetActive(i == 0);
            }
        }
        currentIndex = 0;
    }

    public void AvanzarSiguienteCartel()
    {
        if (cartelesFases == null || cartelesFases.Length == 0) return;

        // Apagar el cartel actual
        if (currentIndex >= 0 && currentIndex < cartelesFases.Length)
        {
            if (cartelesFases[currentIndex] != null)
            {
                cartelesFases[currentIndex].SetActive(false);
            }
        }

        currentIndex++;

        // Encender el siguiente cartel
        if (currentIndex < cartelesFases.Length)
        {
            if (cartelesFases[currentIndex] != null)
            {
                cartelesFases[currentIndex].SetActive(true);
            }
        }
        else
        {
            Debug.Log("[MathPuzzleSequencer]: Se alcanzó el final de la secuencia de carteles en " + gameObject.name);
        }
    }
}
