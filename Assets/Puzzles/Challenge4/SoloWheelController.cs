using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class SoloWheelController : NetworkBehaviour
{
    [Header("Rings (3 concentric rings)")]
    [SerializeField] private Transform ring0;
    [SerializeField] private Transform ring1;
    [SerializeField] private Transform ring2;

    [Header("Ring Step Settings")]
    [SerializeField] private float stepAngle = 30f;
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private LeanTweenType rotationEase = LeanTweenType.easeOutBack;

    [Header("Symbol Renderers")]
    [SerializeField] private Renderer[] ring0SymbolRenderers;
    [SerializeField] private Renderer[] ring1SymbolRenderers;
    [SerializeField] private Renderer[] ring2SymbolRenderers;

    [Header("Materials")]
    [SerializeField] private Material symbolDefaultMaterial;
    [SerializeField] private Material symbolSelectedMaterial;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotateClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private float shakeStrength = 0.08f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Header("Events")]
    public UnityEvent OnSelectionChanged;
    public UnityEvent OnResolved;

    [Networked] private int Ring0Steps { get; set; }
    [Networked] private int Ring1Steps { get; set; }
    [Networked] private int Ring2Steps { get; set; }
    [Networked] public NetworkBool IsResolved { get; private set; }

    private ChangeDetector _changeDetector;
    private bool _isAnimating;
    private int _previousSelected0 = -1;
    private int _previousSelected1 = -1;
    private int _previousSelected2 = -1;
    private bool _playerIsPresent;

    public int SelectedSymbolIndex0 => ComputeSelected(Ring0Steps, ring0SymbolRenderers);
    public int SelectedSymbolIndex1 => ComputeSelected(Ring1Steps, ring1SymbolRenderers);
    public int SelectedSymbolIndex2 => ComputeSelected(Ring2Steps, ring2SymbolRenderers);
    public bool PlayerIsPresent => _playerIsPresent;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        bool anyChange = false;
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            anyChange = true;
            if (change == nameof(Ring0Steps)) ApplyRotation(ring0, Ring0Steps);
            else if (change == nameof(Ring1Steps)) ApplyRotation(ring1, Ring1Steps);
            else if (change == nameof(Ring2Steps)) ApplyRotation(ring2, Ring2Steps);
            
            if (change == nameof(IsResolved) && IsResolved) OnResolved?.Invoke();
        }

        if (!anyChange) return;

        RefreshMaterials();

        int s0 = SelectedSymbolIndex0;
        int s1 = SelectedSymbolIndex1;
        int s2 = SelectedSymbolIndex2;

        if (s0 != _previousSelected0 || s1 != _previousSelected1 || s2 != _previousSelected2)
        {
            _previousSelected0 = s0;
            _previousSelected1 = s1;
            _previousSelected2 = s2;
            OnSelectionChanged?.Invoke();
        }
    }

    public void RotateRing(int ringIndex, int direction)
    {
        if (_isAnimating) return;
        Rpc_RequestRotate(ringIndex, direction);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestRotate(int ringIndex, int direction)
    {
        switch (ringIndex)
        {
            case 0: Ring0Steps += direction; break;
            case 1: Ring1Steps += direction; break;
            case 2: Ring2Steps += direction; break;
        }
        Rpc_PlayRotateSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRotateSound()
    {
        if (audioSource != null && rotateClip != null)
            audioSource.PlayOneShot(rotateClip);
    }

    public void MarkResolved()
    {
        if (!HasStateAuthority) return;
        IsResolved = true;
    }

    private void ApplyRotation(Transform ringTransform, int steps)
    {
        if (ringTransform == null) return;

        float targetAngle = steps * stepAngle;
        float startAngle = ringTransform.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        _isAnimating = true;
        LeanTween.cancel(ringTransform.gameObject);

        LeanTween.value(ringTransform.gameObject, startAngle, targetAngle, rotationDuration)
            .setEase(rotationEase)
            .setOnUpdate((float angle) => ringTransform.localRotation = Quaternion.Euler(0f, 0f, angle))
            .setOnComplete(() => _isAnimating = false);
    }

    private int ComputeSelected(int steps, Renderer[] renderers)
    {
        int count = renderers != null ? renderers.Length : 1;
        return ((steps % count) + count) % count;
    }

    private void RefreshMaterials()
    {
        ApplySelectionToRenderers(ring0SymbolRenderers, SelectedSymbolIndex0);
        ApplySelectionToRenderers(ring1SymbolRenderers, SelectedSymbolIndex1);
        ApplySelectionToRenderers(ring2SymbolRenderers, SelectedSymbolIndex2);
    }

    private void ApplySelectionToRenderers(Renderer[] renderers, int selectedIndex)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].material = i == selectedIndex ? symbolSelectedMaterial : symbolDefaultMaterial;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            _playerIsPresent = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            _playerIsPresent = false;
        }
    }

    public void TriggerErrorFeedback()
    {
        if (audioSource != null && errorClip != null)
            audioSource.PlayOneShot(errorClip);

        Vector3 originalPos = transform.localPosition;
        LeanTween.cancel(gameObject);
        LeanTween.value(gameObject, 0f, 1f, shakeDuration)
            .setOnUpdate(t =>
            {
                float offset = Mathf.Sin(t * Mathf.PI * 8f) * shakeStrength * (1f - t);
                transform.localPosition = originalPos + new Vector3(offset, 0f, 0f);
            })
            .setOnComplete(() => transform.localPosition = originalPos);
    }

    public void TriggerSuccessFeedback()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 1.05f, 0.1f)
            .setEase(LeanTweenType.easeOutBack)
            .setLoopPingPong(1);
    }
}
