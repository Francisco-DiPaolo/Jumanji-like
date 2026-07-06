using UnityEngine;
using Fusion;

/// <summary>
/// Disparador que arranca el CentralClockManager cuando cualquier jugador
/// (host o cliente) entra en el trigger.
/// </summary>
public class ClockStarterTrigger : MonoBehaviour
{
    [Tooltip("El reloj central que se va a iniciar al tocar este trigger.")]
    public CentralClockManager centralClock;

    [Tooltip("Si es true, este trigger se destruirá o desactivará después de usarse una vez.")]
    public bool triggerOnlyOnce = true;

    private bool _hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;
        if (centralClock == null || centralClock.IsRunning) return;

        // Comprobamos que lo que entra es el jugador (puedes ajustar el tag o componente según tu proyecto)
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            _hasTriggered = true;
            
            // Le pedimos al servidor que inicie el reloj de manera sincronizada para todos
            centralClock.Rpc_RequestStartClock();

            // Opcional: apagar el trigger para que no siga detectando colisiones
            if (triggerOnlyOnce)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
