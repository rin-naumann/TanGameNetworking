using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("SFX Clips")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip dashClip;
    [SerializeField] private AudioClip runClip;
    [SerializeField] private AudioClip grappleClip;
    [SerializeField] private AudioClip grappleShotClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip doubleJumpClip;
    [SerializeField] private AudioClip airLoopClip;

    [Header("Volume")]
    [SerializeField] private float masterVolume = 1f;
    private AudioSource _effectSource;
    private AudioSource _footstepSource;
    private AudioSource _airSource;
    private AudioSource _grappleSource;
    private AudioSource _jumpSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _effectSource   = AddSource(loop: false);
        _footstepSource = AddSource(loop: true);
        _airSource      = AddSource(loop: true);
        _grappleSource  = AddSource(loop: true);
        _jumpSource     = AddSource(loop: false);

        if (grappleClip != null) _grappleSource.clip = grappleClip;
        if (airLoopClip != null) _airSource.clip     = airLoopClip;
    }

    // Creates and returns a configured AudioSource on this GameObject.
    private AudioSource AddSource(bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.loop        = loop;
        src.playOnAwake = false;
        src.volume      = masterVolume;
        return src;
    }

    // Plays a one-shot on the effects source. Never interrupts jump or looping sources.
    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        _effectSource.PlayOneShot(clip, masterVolume);
    }

    // Starts a looping source only if the clip or pitch has changed, or it stopped unexpectedly.
    // Avoids the stutter caused by calling Play() every frame.
    private void StartLoop(AudioSource source, AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        if (source.clip == clip && source.isPlaying && Mathf.Approximately(source.pitch, pitch)) return;
        source.clip   = clip;
        source.pitch  = pitch;
        source.volume = masterVolume;
        // Use timeSamples = 0 and Play to ensure no gap from a previous stop.
        source.timeSamples = 0;
        source.Play();
    }

    private void StopLoop(AudioSource source)
    {
        if (source.isPlaying) source.Stop();
        source.clip = null;
    }

    // Public API.
    public void PlayShoot()  => PlayOneShot(shootClip);
    public void PlayDash()   => PlayOneShot(dashClip);
    public void PlayReload() => PlayOneShot(reloadClip);

    // Fires the grapple launch one-shot.
    public void PlayGrapple() => PlayOneShot(grappleShotClip);

    // Jump uses its own dedicated source so it never cuts off shoot/dash/reload.
    public void PlayJump()
    {
        if (jumpClip == null) return;
        _jumpSource.PlayOneShot(jumpClip, masterVolume);
    }

    public void PlayDoubleJump()
    {
        if (doubleJumpClip == null) return;
        _jumpSource.PlayOneShot(doubleJumpClip, masterVolume);
    }

    // Starts the grapple pull loop when the hook lands.
    public void StartGrappleLoop() => StartLoop(_grappleSource, grappleClip);

    // Stops the grapple pull loop on release.
    public void StopGrappleLoop()  => StopLoop(_grappleSource);

    // Called every frame by PlayerController.
    // Handles footstep loop, air loop, and their mutual exclusion.
    public void UpdateFootsteps(bool isMoving, bool isSprinting, bool isGrounded)
    {
        if (isGrounded)
        {
            // Stop air loop the moment we land.
            StopLoop(_airSource);

            if (isMoving)
                // Walk is run clip at lower pitch to avoid needing a separate asset.
                StartLoop(_footstepSource, runClip, isSprinting ? 1f : 0.75f);
            else
                StopLoop(_footstepSource);
        }
        else
        {
            // Stop footsteps the moment we leave the ground.
            StopLoop(_footstepSource);

            // Play air loop while airborne regardless of movement input.
            StartLoop(_airSource, airLoopClip);
        }
    }
}