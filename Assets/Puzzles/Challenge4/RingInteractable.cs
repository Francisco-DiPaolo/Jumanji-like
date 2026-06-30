using UnityEngine;

public class RingInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private int ringIndex;

    private WheelRingController _wheel;
    private SoloWheelController _soloWheel;

    private void Awake()
    {
        _wheel = GetComponentInParent<WheelRingController>();
        _soloWheel = GetComponentInParent<SoloWheelController>();
    }

    public void Hover() { }
    public void UnHover() { }

    public void Select()
    {
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
