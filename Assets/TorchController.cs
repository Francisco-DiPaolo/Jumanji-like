using Fusion;
using UnityEngine;

public class TorchController : NetworkBehaviour
{
    [SerializeField] bool isGreenTorch;

    [Networked] public NetworkBool IsLit { get; set; }

    ChangeDetector _changeDetector;
    GameObject _fireVfx;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _fireVfx = transform.Find("FireVFX")?.gameObject;
        ApplyVisuals();
    }

    public void Light()
    {
        if (Object.HasStateAuthority)
            IsLit = true;
    }

    public void Extinguish()
    {
        if (Object.HasStateAuthority)
            IsLit = false;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsLit))
                ApplyVisuals();
        }
    }

    void ApplyVisuals()
    {
        if (_fireVfx != null)
            _fireVfx.SetActive(IsLit);
    }
}
