using UnityEngine;
using System.Collections.Generic;

public class PulseManager : MonoBehaviour
{
    public static PulseManager Instance { get; private set; }

    [Header("Pulse Settings")]
    public float pulseSpeed = 8f;
    public float revealHoldDuration = 10f;
    public float waveFadeDuration = 0.75f;
    public float fadeSpeed = 0.6f;

    [Header("Limits")]
    public int maxPulses = 12;
    public int maxGlowPoints = 8;

    struct Pulse
    {
        public Vector3 origin;
        public Vector3 normal;
        public float radius;
        public float intensity;
        public float waveIntensity;
        public float maxRadius;
        public float holdTimeRemaining;
        public bool reachedMaxRadius;
    }

    struct GlowPoint
    {
        public Vector3 position;
        public float radius;
        public float intensity;
    }

    List<Pulse> activePulses = new List<Pulse>();
    List<GlowPoint> activeGlows = new List<GlowPoint>();

    Vector4[] pulseOriginsBuf;
    Vector4[] pulseNormalsBuf;
    float[] pulseIntensitiesBuf;
    float[] pulseWaveIntensitiesBuf;
    Vector4[] glowPointsBuf;
    float[] glowIntensitiesBuf;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        pulseOriginsBuf     = new Vector4[maxPulses];
        pulseNormalsBuf     = new Vector4[maxPulses];
        pulseIntensitiesBuf = new float[maxPulses];
        pulseWaveIntensitiesBuf = new float[maxPulses];
        glowPointsBuf       = new Vector4[maxGlowPoints];
        glowIntensitiesBuf  = new float[maxGlowPoints];
    }

    void Update()
    {
        UpdatePulses();
        UpdateGlows();
        PushToShader();
    }

    public void SpawnPulse(Vector3 origin, float maxRadius)
    {
        SpawnPulse(origin, Vector3.up, maxRadius);
    }

    public void SpawnPulse(Vector3 origin, Vector3 normal, float maxRadius)
    {
        if (activePulses.Count >= maxPulses)
        {
            activePulses.RemoveAt(0);
        }

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;
        else
            normal.Normalize();

        activePulses.Add(new Pulse
        {
            origin    = origin,
            normal    = normal,
            radius    = 0f,
            intensity = 1f,
            waveIntensity = 1f,
            maxRadius = maxRadius,
            holdTimeRemaining = revealHoldDuration,
            reachedMaxRadius = false
        });
    }

    public void AddGlowPoint(Vector3 position, float radius = 0.5f)
    {
        if (activeGlows.Count >= maxGlowPoints)
        {
            activeGlows.RemoveAt(0);
        }

        activeGlows.Add(new GlowPoint
        {
            position  = position,
            radius    = radius,
            intensity = 1f
        });
    }


    public void StartBeacon(Vector3 position, float pulseRadius, float interval, float lifetime)
    {
        StartCoroutine(BeaconRoutine(position, pulseRadius, interval, lifetime));
    }

    System.Collections.IEnumerator BeaconRoutine(Vector3 pos, float radius, float interval, float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            SpawnPulse(pos, radius);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    void UpdatePulses()
    {
        for (int i = activePulses.Count - 1; i >= 0; i--)
        {
            var p = activePulses[i];

            if (!p.reachedMaxRadius)
            {
                p.radius += pulseSpeed * Time.deltaTime;

                if (p.radius >= p.maxRadius)
                {
                    p.radius = p.maxRadius;
                    p.reachedMaxRadius = true;
                }
            }
            else
            {
                if (p.waveIntensity > 0f)
                {
                    var fadeDuration = Mathf.Max(0.01f, waveFadeDuration);
                    p.waveIntensity = Mathf.Max(0f, p.waveIntensity - (Time.deltaTime / fadeDuration));
                }

                if (p.holdTimeRemaining > 0f)
                {
                    p.holdTimeRemaining -= Time.deltaTime;
                }
                else
                {
                    p.intensity -= fadeSpeed * Time.deltaTime;
                }
            }

            if (p.intensity <= 0f)
            {
                activePulses.RemoveAt(i);
                continue;
            }

            activePulses[i] = p;
        }
    }

    void UpdateGlows()
    {
    }

    void PushToShader()
    {
        int pulseCount = Mathf.Min(activePulses.Count, maxPulses);
        for (int i = 0; i < maxPulses; i++)
        {
            if (i < pulseCount)
            {
                var p = activePulses[i];
                pulseOriginsBuf[i] = new Vector4(p.origin.x, p.origin.y, p.origin.z, p.radius);
                pulseNormalsBuf[i] = new Vector4(p.normal.x, p.normal.y, p.normal.z, 0f);
                pulseIntensitiesBuf[i] = p.intensity;
                pulseWaveIntensitiesBuf[i] = p.waveIntensity;
            }
            else
            {
                pulseOriginsBuf[i] = Vector4.zero;
                pulseNormalsBuf[i] = Vector4.zero;
                pulseIntensitiesBuf[i] = 0f;
                pulseWaveIntensitiesBuf[i] = 0f;
            }
        }

        Shader.SetGlobalVectorArray("_PulseOrigins", pulseOriginsBuf);
        Shader.SetGlobalVectorArray("_PulseNormals", pulseNormalsBuf);
        Shader.SetGlobalFloatArray("_PulseIntensities", pulseIntensitiesBuf);
        Shader.SetGlobalFloatArray("_PulseWaveIntensities", pulseWaveIntensitiesBuf);
        Shader.SetGlobalInt("_PulseCount", pulseCount);

        int glowCount = Mathf.Min(activeGlows.Count, maxGlowPoints);
        for (int i = 0; i < maxGlowPoints; i++)
        {
            if (i < glowCount)
            {
                var g = activeGlows[i];
                glowPointsBuf[i] = new Vector4(g.position.x, g.position.y, g.position.z, g.radius);
                glowIntensitiesBuf[i] = g.intensity;
            }
            else
            {
                glowPointsBuf[i] = Vector4.zero;
                glowIntensitiesBuf[i] = 0f;
            }
        }

        Shader.SetGlobalVectorArray("_GlowPoints", glowPointsBuf);
        Shader.SetGlobalFloatArray("_GlowIntensities", glowIntensitiesBuf);
        Shader.SetGlobalInt("_GlowCount", glowCount);
    }
}
