using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural crash visual effects — all three tiers created at runtime in C#
/// with no prefab assets. Attached to the GameManager GameObject.
///   Destroyed  → dark smoke column (persists) + orange flash burst
///   Ditched    → white water-spray burst + one-shot camera shake
///   HardLanding→ dust cloud puff at touchdown
/// Call Trigger() at crash-time; call ClearEffects() on restart.
/// </summary>
public class CrashEffects : MonoBehaviour
{
    CessnaPhysics phys;
    ViewManager   viewMgr;

    readonly List<GameObject> active = new List<GameObject>();

    public void Init(CessnaPhysics p, ViewManager v) { phys = p; viewMgr = v; }

    // ---- public API -------------------------------------------------------

    public void Trigger(LandingTier tier, Vector3 worldPos)
    {
        ClearEffects();
        switch (tier)
        {
            case LandingTier.Destroyed:
                SpawnFlash(worldPos);
                SpawnSmoke(worldPos);
                break;
            case LandingTier.Ditched:
                SpawnSpray(worldPos);
                DoShake(0.45f);
                break;
            case LandingTier.HardLanding:
                SpawnDust(worldPos);
                break;
        }
    }

    public void ClearEffects()
    {
        foreach (var g in active) { if (g) Destroy(g); }
        active.Clear();
    }

    // ---- camera shake -----------------------------------------------------

    void DoShake(float strength)
    {
        // Only shake the external (chase/orbit) camera — the SmoothDamp in
        // ViewManager will smoothly correct the offset back over ~0.1 s, which
        // produces the shake effect for free. Cockpit camera is skipped because
        // there is no correction mechanism for its localPosition.
        if (viewMgr == null || viewMgr.Current == ViewManager.View.Cockpit) return;
        var cam = viewMgr.ActiveCamera;
        if (cam == null) return;
        cam.transform.position += new Vector3(
            Random.Range(-strength, strength),
            Random.Range(-strength * 0.6f, strength * 0.6f),
            0f);
    }

    // ---- smoke column (Destroyed — loops until ClearEffects) --------------

    void SpawnSmoke(Vector3 pos)
    {
        var go = Make("CrashSmoke", pos);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.loop            = true;
        m.duration        = 999f;
        m.startLifetime   = new ParticleSystem.MinMaxCurve(4f, 7f);
        m.startSpeed      = new ParticleSystem.MinMaxCurve(2f, 5f);
        m.startSize       = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
        m.startColor      = new ParticleSystem.MinMaxGradient(
                                new Color(0.06f, 0.06f, 0.06f, 0.90f),
                                new Color(0.28f, 0.28f, 0.28f, 0.65f));
        m.gravityModifier = -0.08f;   // particles rise
        m.maxParticles    = 250;
        m.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 20f;

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 10f;
        sh.radius    = 0.5f;

        ColorOverLifetime(ps,
            new Color(0.04f, 0.04f, 0.04f), new Color(0.42f, 0.42f, 0.42f),
            alphaStart: 0.9f, alphaEnd: 0f);
        SizeOverLifetime(ps, fromScale: 0.30f, toScale: 2.20f);
        SetMat(go, additive: false);
        ps.Play();
    }

    // ---- orange flash burst (Destroyed — one-shot) ------------------------

    void SpawnFlash(Vector3 pos)
    {
        var go = Make("CrashFlash", pos);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.loop          = false;
        m.duration      = 0.20f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.50f);
        m.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 15f);
        m.startSize     = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        m.startColor    = new ParticleSystem.MinMaxGradient(
                              new Color(1f, 0.55f, 0.05f, 1f),
                              new Color(1f, 0.18f, 0.00f, 0.85f));
        m.gravityModifier = 0.10f;
        m.maxParticles  = 80;
        m.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 65) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.7f;

        ColorOverLifetime(ps,
            new Color(1f, 0.70f, 0f), new Color(0.18f, 0.04f, 0f),
            alphaStart: 1f, alphaEnd: 0f);
        SetMat(go, additive: true);
        ps.Play();
    }

    // ---- water spray burst (Ditched — one-shot) ---------------------------

    void SpawnSpray(Vector3 pos)
    {
        var go = Make("CrashSpray", pos);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.loop          = false;
        m.duration      = 0.30f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        m.startSpeed    = new ParticleSystem.MinMaxCurve(6f, 22f);
        m.startSize     = new ParticleSystem.MinMaxCurve(0.25f, 1.2f);
        m.startColor    = new ParticleSystem.MinMaxGradient(
                              new Color(0.80f, 0.92f, 1f, 0.95f),
                              new Color(1f,    1f,    1f, 0.70f));
        m.gravityModifier = 0.70f;   // arc up then fall
        m.maxParticles  = 180;
        m.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 55f;
        sh.radius    = 1.5f;

        ColorOverLifetime(ps,
            Color.white, new Color(0.60f, 0.82f, 1f),
            alphaStart: 0.90f, alphaEnd: 0f);
        SetMat(go, additive: false);
        ps.Play();
    }

    // ---- dust cloud (HardLanding — one-shot) ------------------------------

    void SpawnDust(Vector3 pos)
    {
        var go = Make("CrashDust", pos);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.loop          = false;
        m.duration      = 0.25f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        m.startSpeed    = new ParticleSystem.MinMaxCurve(1.5f, 7f);
        m.startSize     = new ParticleSystem.MinMaxCurve(0.6f, 3.0f);
        m.startColor    = new ParticleSystem.MinMaxGradient(
                              new Color(0.62f, 0.52f, 0.37f, 0.80f),
                              new Color(0.78f, 0.68f, 0.50f, 0.55f));
        m.gravityModifier = 0.05f;   // barely falls — dust drifts
        m.maxParticles  = 90;
        m.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 50f;
        sh.radius    = 1.2f;

        ColorOverLifetime(ps,
            new Color(0.70f, 0.60f, 0.40f), new Color(0.85f, 0.80f, 0.65f),
            alphaStart: 0.75f, alphaEnd: 0f);
        SizeOverLifetime(ps, fromScale: 0.50f, toScale: 1.60f);
        SetMat(go, additive: false);
        ps.Play();
    }

    // ---- helpers ----------------------------------------------------------

    GameObject Make(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, Quaternion.identity);
        active.Add(go);
        return go;
    }

    static void SetMat(GameObject go, bool additive)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        // Try shaders from most-specific to most-fallback
        string[] candidates = additive
            ? new[] { "Particles/Additive", "Legacy Shaders/Particles/Additive",
                       "Mobile/Particles/Additive" }
            : new[] { "Particles/Alpha Blended", "Legacy Shaders/Particles/Alpha Blended",
                       "Sprites/Default" };
        foreach (var n in candidates)
        {
            var sh = Shader.Find(n);
            if (sh == null) continue;
            r.material = new Material(sh);
            return;
        }
        // Last resort: whatever was auto-assigned — just leave it
    }

    static void ColorOverLifetime(ParticleSystem ps,
                                  Color fromCol, Color toCol,
                                  float alphaStart, float alphaEnd)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(fromCol, 0f), new GradientColorKey(toCol, 1f) },
            new[] { new GradientAlphaKey(alphaStart, 0f), new GradientAlphaKey(alphaEnd, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void SizeOverLifetime(ParticleSystem ps, float fromScale, float toScale)
    {
        var sOL = ps.sizeOverLifetime;
        sOL.enabled = true;
        sOL.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, fromScale), new Keyframe(1f, toScale)));
    }
}
