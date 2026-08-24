using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Spoken voice callouts, redesigned as three priority tiers so it sounds like a real
/// pilot's headset — a few crisp transmissions with SILENCE in between, not a non-stop
/// announcer. Clips are real speech pre-generated with macOS text-to-speech into
/// StreamingAssets/Voice/*.wav, loaded at runtime (byte-parsed, so a space in the project
/// path can't break a file:// URI).
///
///   Tier 0 GPWS    (stall / sink rate / low airspeed / altitude floor 500·100·50·10) —
///                  always fires; interrupts a Tier 1/2 clip and plays immediately.
///   Tier 1 Cockpit (rotate / positive rate / mission complete) — queues normally.
///   Tier 2 ATC     (turn / climb / descend / weather / faults / traffic / waypoints /
///                  cleared to land) — 8-second radio lockout; a call within the lockout
///                  is DISCARDED (the HUD still shows the text), so ATC never stacks.
///
/// Call sites choose the tier via SayGPWS / SayCockpit / SayATC.
/// </summary>
public class VoiceCallouts : MonoBehaviour
{
    public static VoiceCallouts Instance { get; private set; }

    /// <summary>True while a callout is being spoken (used to duck the engine/wind).</summary>
    public bool Speaking => src != null && src.isPlaying;

    // Only the clips the new system actually uses (gear_up/minimums/pull_up/welcome and the
    // 400/300/200/40/30/20 ladder steps were dropped).
    static readonly string[] Keys =
    {
        "rotate","positive_rate","sink_rate","airspeed_low","stall",
        "alt_500","alt_100","alt_50","alt_10",
        "cleared_takeoff","climb","descend","turn_left","turn_right","traffic","caution",
        "instrument_fault","weather_ahead","waypoint","cleared_land","mission_complete",
    };

    struct Callout { public AudioClip clip; public int tier; }   // tier: 0=GPWS 1=Cockpit 2=ATC

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly List<Callout> pending = new List<Callout>();
    AudioSource src;
    int currentTier = -1;     // tier of the clip currently playing
    float atcCooldown;        // ATC radio lockout timer (seconds)

    // basic flight callout state
    bool wasFlying, saidRotate, saidPositive, wasStalled;
    float prevAlt, sinkCooldown, slowCooldown;
    static readonly int[] AltSteps = { 500, 100, 50, 10 };
    bool[] altSaid;

    void Awake()
    {
        Instance = this;
        altSaid = new bool[AltSteps.Length];
        var go = new GameObject("VoiceSource");
        go.transform.SetParent(transform);
        src = go.AddComponent<AudioSource>();
        src.spatialBlend = 0f; src.playOnAwake = false; src.volume = 0.75f;

        foreach (var key in Keys)
        {
            var clip = LoadWav(Path.Combine(Application.streamingAssetsPath, "Voice", key + ".wav"), key);
            if (clip != null) clips[key] = clip;
        }
    }

    // ---- public tier API (encode the tier at the call site) ----
    public void SayGPWS(string key)    => Enqueue(key, 0);
    public void SayCockpit(string key) => Enqueue(key, 1);
    public void SayATC(string key)
    {
        if (atcCooldown > 0f) return;            // radio busy — discard (HUD still shows it)
        if (Enqueue(key, 2)) atcCooldown = 8f;   // start the lockout only if it actually queued
    }

    bool Enqueue(string key, int tier)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (!clips.TryGetValue(key, out var clip) || clip == null) return false;
        if (pending.Count >= 4) return false;    // safety cap
        // GPWS interrupts a lower-priority clip that is currently talking
        if (tier == 0 && src != null && src.isPlaying && currentTier > 0)
        {
            src.Stop();
            currentTier = -1;
        }
        pending.Add(new Callout { clip = clip, tier = tier });
        return true;
    }

    void PlayNext()
    {
        if (src == null || src.isPlaying || pending.Count == 0) return;
        int best = 0;                            // lowest tier number wins (GPWS beats ATC)
        for (int i = 1; i < pending.Count; i++) if (pending[i].tier < pending[best].tier) best = i;
        var c = pending[best];
        pending.RemoveAt(best);
        currentTier = c.tier;
        // ATC and GPWS speech is a TASK INSTRUCTION, never decoration — a mission whose
        // clearance is inaudible is not the mission it claims to be.
        src.PlayOneShot(c.clip, AudioPolicy.CalloutGain);
    }

    void Update()
    {
        if (atcCooldown > 0f) atcCooldown -= Time.deltaTime;

        var gm = GameManager.Instance;
        if (gm != null && gm.Aircraft != null)
        {
            bool flying = gm.State == GameState.Flying;
            if (flying && !wasFlying) ResetFlight();
            wasFlying = flying;
            var ac = gm.Aircraft;
            if (flying)
            {
                float alt = ac.AltitudeM, vs = ac.VerticalSpeedMs, kmh = ac.AirspeedKmh;
                sinkCooldown -= Time.deltaTime; slowCooldown -= Time.deltaTime;

                // takeoff — cockpit calls
                if (ac.Grounded && ac.Throttle01 > 0.5f && kmh > 90f && !saidRotate) { SayCockpit("rotate"); saidRotate = true; }
                if (!ac.Grounded && vs > 1.2f && saidRotate && !saidPositive) { SayCockpit("positive_rate"); saidPositive = true; }

                // descent altitude floor (GPWS) — only the 4 key heights, while descending
                if (!ac.Grounded && vs < -0.5f)
                    for (int i = 0; i < AltSteps.Length; i++)
                        if (!altSaid[i] && prevAlt > AltSteps[i] && alt <= AltSteps[i]) { SayGPWS("alt_" + AltSteps[i]); altSaid[i] = true; }
                if (alt > 550f) for (int i = 0; i < altSaid.Length; i++) altSaid[i] = false;   // rearm after climbing away

                // GPWS warnings
                if (!ac.Grounded && vs < -6f && alt < 300f && sinkCooldown <= 0f) { SayGPWS("sink_rate"); sinkCooldown = 4f; }
                if (ac.Stalled && !wasStalled) SayGPWS("stall");
                wasStalled = ac.Stalled;
                if (!ac.Grounded && kmh < 78f && alt > 40f && slowCooldown <= 0f) { SayGPWS("airspeed_low"); slowCooldown = 6f; }

                prevAlt = alt;
            }
            else prevAlt = ac.AltitudeM;
        }

        PlayNext();   // after triggers, so a GPWS added this frame plays this frame
    }

    void ResetFlight()
    {
        saidRotate = saidPositive = wasStalled = false;
        sinkCooldown = slowCooldown = 0f;
        atcCooldown = 0f;
        pending.Clear();
        for (int i = 0; i < altSaid.Length; i++) altSaid[i] = false;
        prevAlt = GameManager.Instance != null && GameManager.Instance.Aircraft != null
                ? GameManager.Instance.Aircraft.AltitudeM : 0f;
    }

    // ---- minimal PCM-16 WAV loader (no file:// URI, so a space in the path is fine) ----
    // Walks the RIFF chunks to find BOTH "fmt " and "data" — macOS `say` inserts a
    // "JUNK" chunk before "fmt " and an "FLLR" chunk before "data", so fixed byte
    // offsets don't work; we parse whatever order the chunks come in.
    static AudioClip LoadWav(string path, string name)
    {
        try
        {
            if (!File.Exists(path)) return null;
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 12 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;

            int channels = 0, sampleRate = 0, bits = 0, dataOffset = -1, dataLen = 0;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                int sz = b[pos + 4] | (b[pos + 5] << 8) | (b[pos + 6] << 16) | (b[pos + 7] << 24);
                int body = pos + 8;
                if (b[pos] == 'f' && b[pos + 1] == 'm' && b[pos + 2] == 't' && b[pos + 3] == ' ' && body + 16 <= b.Length)
                {
                    channels   = b[body + 2] | (b[body + 3] << 8);
                    sampleRate = b[body + 4] | (b[body + 5] << 8) | (b[body + 6] << 16) | (b[body + 7] << 24);
                    bits       = b[body + 14] | (b[body + 15] << 8);
                }
                else if (b[pos] == 'd' && b[pos + 1] == 'a' && b[pos + 2] == 't' && b[pos + 3] == 'a')
                {
                    dataOffset = body; dataLen = sz;
                }
                pos = body + sz + (sz & 1);
            }
            if (dataOffset < 0 || bits != 16 || channels < 1 || sampleRate <= 0) return null;
            if (dataOffset + dataLen > b.Length) dataLen = b.Length - dataOffset;

            int n = dataLen / 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                short s = (short)(b[dataOffset + i * 2] | (b[dataOffset + i * 2 + 1] << 8));
                data[i] = s / 32768f;
            }
            var clip = AudioClip.Create(name, n / channels, channels, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        catch { return null; }
    }
}
