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

        // Si este es el objeto del jugador local (Input Authority)
        if (HasInputAuthority)
        {
            // Le enviamos un RPC al servidor/host para que actualice la variable de red
            RpcSetNickname(SessionLauncher.LocalNickname);
        }

        // Inicializamos el texto al aparecer (útil si un jugador se une tarde)
        if (text != null)
        {
            text.text = SincronizedNickname.ToString();
        }
    }

    // El RPC lo envía el cliente dueño de este objeto y lo ejecuta el Host/Server
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcSetNickname(string nickname)
    {
        SincronizedNickname = nickname;
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
