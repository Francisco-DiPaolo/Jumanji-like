using Fusion;
using TMPro;
using UnityEngine;

public class Usermane : NetworkBehaviour
{
    // Se usa NetworkString para sincronizar strings por la red en Fusion
    [Networked] public NetworkString<_32> SincronizedNickname { get; set; }
    
    [SerializeField] TMP_Text text;
    ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Si este es mi jugador local, le envío mi nombre local a la variable de red
        if (HasStateAuthority)
        {
            SincronizedNickname = SessionLauncher.LocalNickname;
        }

        // Inicializamos el texto al aparecer (útil si un jugador se une tarde)
        if (text != null)
        {
            text.text = SincronizedNickname.ToString();
        }
    }

    public override void Render()
    {
        // Detectar si el nombre de red cambió
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(SincronizedNickname):
                    if (text != null)
                        text.text = SincronizedNickname.ToString();
                    break;
            }
        }

        // Hacer que el texto siempre mire a la cámara del jugador local (Billboard)
        if (Camera.main != null && text != null)
        {
            // Igualar la rotación de la cámara hace que el texto mire hacia ella de frente
            text.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
