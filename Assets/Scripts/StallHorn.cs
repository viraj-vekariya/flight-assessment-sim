using UnityEngine;

/// <summary>
/// The stall-warning horn: a procedurally-generated intermittent beep (no audio
/// asset) that sounds while the aircraft is within a few degrees of the stall in
/// the air — like a real aircraft's stall warning. 2D audio so it's always heard.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StallHorn : MonoBehaviour
{
    public CessnaPhysics phys;
    AudioSource src;

    void Start()
    {
        src = GetComponent<AudioSource>();
        const int sr = 44100, len = sr / 2;        // 0.5 s loop
        var data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)sr;
            float gate = Mathf.Sin(2f * Mathf.PI * 6f * t) > 0f ? 1f : 0f;   // ~6 Hz on/off beep
            data[i] = Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.25f * gate;
        }
        var clip = AudioClip.Create("stall_horn", len, 1, sr, false);
        clip.SetData(data, 0);
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;       // 2D — always audible
        src.playOnAwake = false;
        src.volume = 0.5f;
    }

    void Update()
    {
        if (phys == null || src == null) return;
        bool warn = phys.StallWarning && !phys.Grounded && !phys.Crashed;
        if (warn && !src.isPlaying) src.Play();
        else if (!warn && src.isPlaying) src.Stop();
    }
}
