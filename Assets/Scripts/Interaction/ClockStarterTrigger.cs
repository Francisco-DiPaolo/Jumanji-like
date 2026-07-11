using UnityEngine;
using Fusion;

/// <summary>
/// [DEPRECATED] Este trigger ya no es necesario para iniciar el CentralClockManager.
/// El reloj ahora se inicia automáticamente al completar la Fase 1, desde
/// Challenge4Manager.Rpc_CompletePhase1(). Este script puede desactivarse o eliminarse de la escena.
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
