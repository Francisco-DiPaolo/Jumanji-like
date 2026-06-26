using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class WheelRingController : NetworkBehaviour
{
    [Header("Ring GameObjects (2 concentric rings per wheel)")]
    [SerializeField] private Transform ring0;
    [SerializeField] private Transform ring1;

    [Header("Ring Step Settings")]
    [SerializeField] private float stepAngle = 30f;
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private LeanTweenType rotationEase = LeanTweenType.easeOutBack;

    [Header("Symbol Renderers")]
    [SerializeField] private Renderer[] ring0SymbolRenderers;
    [SerializeField] private Renderer[] ring1SymbolRenderers;

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

    [Networked] private int Ring0Steps { get; set; }
    [Networked] private int Ring1Steps { get; set; }

    private ChangeDetector _changeDetector;
    private bool _isAnimating;
    private int _previousSelected = -1;
    private bool _playerIsPresent;

    public int SelectedSymbolIndex => ComputeSelectedSymbol();
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
            if (change == nameof(Ring0Steps)) ApplyRingRotation(ring0, Ring0Steps);
            else if (change == nameof(Ring1Steps)) ApplyRingRotation(ring1, Ring1Steps);
        }

        if (!anyChange) return;

        int currentSelected = ComputeSelectedSymbol();
        RefreshSelectedMaterials();

        if (currentSelected != _previousSelected)
        {
            _previousSelected = currentSelected;
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
        }

        Rpc_PlayRotateSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRotateSound()
    {
        if (audioSource != null && rotateClip != null)
            audioSource.PlayOneShot(rotateClip);
    }

    private void ApplyRingRotation(Transform ring, int steps)
    {
        if (ring == null) return;

        float targetAngle = steps * stepAngle;

        float startAngle = ring.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        _isAnimating = true;
        LeanTween.cancel(ring.gameObject);

        LeanTween.value(ring.gameObject, startAngle, targetAngle, rotationDuration)
            .setEase(rotationEase)
            .setOnUpdate((float angle) =>
            {
                ring.localRotation = Quaternion.Euler(0f, 0f, angle);
            })
            .setOnComplete(() => _isAnimating = false);
    }

    private int ComputeSelected(int steps, Renderer[] renderers)
    {
        int count = renderers != null ? renderers.Length : 1;
        return ((steps % count) + count) % count;
    }

    private int ComputeSelectedSymbol()
    {
        int count = ring0SymbolRenderers != null ? ring0SymbolRenderers.Length : 1;
        int sel0 = ((Ring0Steps % count) + count) % count;
        int sel1 = ((Ring1Steps % count) + count) % count;
        return sel0 == sel1 ? sel0 : -1;
    }

    private void RefreshSelectedMaterials()
    {
        int sel0 = ComputeSelected(Ring0Steps, ring0SymbolRenderers);
        int sel1 = ComputeSelected(Ring1Steps, ring1SymbolRenderers);
        ApplySelectionToRenderers(ring0SymbolRenderers, sel0);
        ApplySelectionToRenderers(ring1SymbolRenderers, sel1);
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
