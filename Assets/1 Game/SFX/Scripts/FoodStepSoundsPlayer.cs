using UnityEngine;

public class FoodStepSoundsPlayer : MonoBehaviour
{
    public AudioClip[] WoodClips;
    public AudioClip[] ConcreteClips;
    public AudioClip[] WaterClips;

    public LayerMask Environment;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0, 256)]
    public int priority = 128;

    public Animator Animator;
    private float _lastFootstep;

    private void OnValidate()
    {
        if (!Animator) Animator = GetComponent<Animator>();
    }

    private void Update()
    {
        var footstep = Animator.GetFloat("Footstep");
        if (Mathf.Abs(footstep) < .00001f) footstep = 0;

        if (_lastFootstep > 0 && footstep < 0 || _lastFootstep < 0 && footstep > 0)
        {
            var clips = GetClipsForSurface();
            var randomClip = clips[Random.Range(0, clips.Length - 1)];
            
            GameObject audioObj = new GameObject("FootstepAudio");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = randomClip;
            source.spatialBlend = 1f;
            source.volume = volume;
            source.priority = priority;
            source.Play();
            Destroy(audioObj, randomClip.length);
        }

        _lastFootstep = footstep;
    }

    private AudioClip[] GetClipsForSurface()
    {
        var isHit = Physics.Raycast(transform.position + Vector3.up * .01f, Vector3.down, out RaycastHit hit, .1f, Environment);

        if (isHit)
        {
            var surface = hit.collider.GetComponent<SurfaceDefinition>();
            if (surface)
            {
                if (surface.SurfaceType == SurfaceType.Concrete) return ConcreteClips;
                if (surface.SurfaceType == SurfaceType.Wood) return WoodClips;
                if (surface.SurfaceType == SurfaceType.Wood) return WaterClips;
            }
        }

        return WoodClips;
    }
}