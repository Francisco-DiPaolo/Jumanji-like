using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SymbolIdentity : MonoBehaviour
{
    [Tooltip("ID único del símbolo lógico (ej: 0 = sol, 1 = luna, 2 = estrella...). " +
             "Debe ser el mismo número en el reloj y en las wheels para el mismo símbolo visual.")]
    [SerializeField] private String symbolId;

    public String SymbolId => symbolId;
}