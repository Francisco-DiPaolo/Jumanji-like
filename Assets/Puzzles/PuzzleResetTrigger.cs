using UnityEngine;

/// <summary>
/// Coloca este componente en un GameObject con un Collider marcado como Trigger.
/// NO necesita NetworkObject propio: delega el RPC al PuzzleGlobalController.
///
/// Configuración en el Inspector:
///   - globalController: arrastrar el PuzzleGlobalController de la escena.
///   - roomIndex: índice de la sala a resetear ("a"/"1", "b"/"2" o "c"/"3").
///   - playerTag: tag de los objetos de jugador (por defecto "Player").
///   - cooldown: segundos mínimos entre resets consecutivos.
/// </summary>
public class PuzzleResetTrigger : MonoBehaviour
{
    [Tooltip("El PuzzleGlobalController de la escena (ya tiene NetworkObject propio).")]
    [SerializeField] private PuzzleGlobalController globalController;

    [Tooltip("Índice de la sala que resetea este trigger: 'a'/'1', 'b'/'2' o 'c'/'3'.")]
    [SerializeField] private string roomIndex = "a";

    [Tooltip("Tag del GameObject que debe activar el reset (normalmente 'Player').")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Segundos mínimos entre activaciones consecutivas del trigger.")]
    [SerializeField] private float cooldown = 2f;

    private float lastResetTime = -999f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (Time.time - lastResetTime < cooldown) return;

        if (globalController == null)
        {
            Debug.LogWarning("[puzle]: PuzzleResetTrigger no tiene un PuzzleGlobalController asignado.");
            return;
        }

        lastResetTime = Time.time;

        Debug.Log("[puzle]: PuzzleResetTrigger activado por " + other.name +
                  ". Solicitando reset de sala '" + roomIndex + "'.");

        globalController.RequestResetRoom(roomIndex);
    }
}
