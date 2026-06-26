using Fusion;
using UnityEngine;

public class RingInteractable : NetworkBasicInteraction
{
    [SerializeField] private int ringIndex;

    private WheelRingController _wheel;
    private SoloWheelController _soloWheel;

    private void Awake()
    {
        _wheel = GetComponentInParent<WheelRingController>();
        _soloWheel = GetComponentInParent<SoloWheelController>();
    }

    public override void Select()
    {
        base.Select();

        if (_wheel != null && _wheel.PlayerIsPresent)
        {
            _wheel.RotateRing(ringIndex, 1);
        }
        else if (_soloWheel != null && _soloWheel.PlayerIsPresent)
        {
            _soloWheel.RotateRing(ringIndex, 1);
        }
    }
}
