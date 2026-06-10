using UnityEngine;

public class FoodStepSoundsPlayer : MonoBehaviour
{
    public AudioClip[] Clips;
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
            var randomClip = Clips[Random.Range(0, Clips.Length - 1)];
            AudioSource.PlayClipAtPoint(randomClip, transform.position);
        }

        _lastFootstep = footstep;
    }

    /*public void FoodStepSounds()
    {
        //var randomClip = Clips[Random.Range(0, Clips.Length - 1)];
        //AudioSource.PlayClipAtPoint(randomClip, transform.position);
    }*/
}