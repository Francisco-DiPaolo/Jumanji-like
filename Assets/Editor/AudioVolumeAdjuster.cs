using UnityEngine;
using UnityEditor;

public class AudioVolumeAdjuster : ScriptableWizard
{
    [Tooltip("Cantidad a sumar (o restar si usas un número negativo) al volumen actual de todos los AudioSources.")]
    public float cantidadAjuste = 0.2f;

    [MenuItem("Tools/Audio/Ajustar Volumen de Todos...")]
    public static void MostrarVentana()
    {
        ScriptableWizard.DisplayWizard<AudioVolumeAdjuster>("Ajustar Volumen General", "Aplicar Ajuste");
    }

    private void OnWizardCreate()
    {
        // Encuentra todos los AudioSources de la escena (incluso los desactivados)
        AudioSource[] todosLosAudios = Object.FindObjectsOfType<AudioSource>(true);
        int contador = 0;

        foreach (AudioSource audio in todosLosAudios)
        {
            // Registra el cambio para poder usar "Control + Z" si nos arrepentimos
            Undo.RecordObject(audio, "Ajustar volumen AudioSource");
            
            // Le suma (o resta) el valor ingresado al volumen actual
            audio.volume = Mathf.Clamp01(audio.volume + cantidadAjuste);
            
            // Si el objeto es un prefab, marca que hubo un cambio para guardarlo
            PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
            
            contador++;
        }

        Debug.Log($"¡Éxito! Se ajustó el volumen en {cantidadAjuste} a {contador} AudioSources en la escena.");
    }
}
