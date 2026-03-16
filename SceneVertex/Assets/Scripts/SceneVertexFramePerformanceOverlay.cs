using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class SceneVertexFramePerformanceOverlay : MonoBehaviour
{
    private const int MaxSamples = 1024;
    private const float WindowSeconds = 1f;
    private const float PanelWidth = 440f;
    private const float PanelHeight = 176f;
    private const float TopMargin = 12f;
    private const float HeaderHeight = 24f;
    private const float GraphHeight = 92f;
    private const float Padding = 12f;
    private const float GoodFrameMs = 16.67f;
    private const float WarningFrameMs = 33.33f;

    private static SceneVertexFramePerformanceOverlay instance;

    private readonly float[] sampleTimes = new float[MaxSamples];
    private readonly float[] sampleDurations = new float[MaxSamples];

    private int head;
    private int count;
    private float latestDelta = 1f / 60f;
    private float averageDelta = 1f / 60f;
    private float minDelta = 1f / 60f;
    private float maxDelta = 1f / 60f;
    private float newestTimestamp;
    private float oldestTimestamp;

    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle captionStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject("SceneVertex_FramePerformanceOverlay");
        instance = gameObject.AddComponent<SceneVertexFramePerformanceOverlay>();
        DontDestroyOnLoad(gameObject);
    }

    private void Awake()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            Destroy(gameObject);
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        latestDelta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        var now = Time.realtimeSinceStartup;
        AddSample(now, latestDelta);
        PruneSamples(now - WindowSeconds);
        RecalculateStats();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || (!Application.isEditor && !Debug.isDebugBuild))
        {
            return;
        }

        EnsureStyles();

        var panelRect = new Rect((Screen.width - PanelWidth) * 0.5f, TopMargin, PanelWidth, PanelHeight);
        var graphRect = new Rect(
            panelRect.x + Padding,
            panelRect.y + HeaderHeight + Padding,
            panelRect.width - Padding * 2f,
            GraphHeight);
        var metricsRect = new Rect(
            graphRect.x,
            graphRect.yMax + 8f,
            graphRect.width,
            panelRect.yMax - graphRect.yMax - 16f);

        DrawRect(panelRect, new Color(0.04f, 0.05f, 0.06f, 0.82f));
        DrawOutline(panelRect, new Color(1f, 1f, 1f, 0.08f));

        GUI.Label(
            new Rect(panelRect.x + Padding, panelRect.y + 6f, 220f, HeaderHeight),
            "Frame Performance (Last 1s)",
            titleStyle);

        var graphMaxMs = Mathf.Clamp(Mathf.Max(50f, maxDelta * 1000f * 1.15f), 50f, 120f);
        DrawGraph(graphRect, graphMaxMs);
        DrawMetrics(metricsRect);
    }

    private void AddSample(float timestamp, float deltaTime)
    {
        if (count == MaxSamples)
        {
            head = (head + 1) % MaxSamples;
            count--;
        }

        var index = (head + count) % MaxSamples;
        sampleTimes[index] = timestamp;
        sampleDurations[index] = deltaTime;
        count++;
    }

    private void PruneSamples(float cutoffTime)
    {
        while (count > 0 && sampleTimes[head] < cutoffTime)
        {
            head = (head + 1) % MaxSamples;
            count--;
        }
    }

    private void RecalculateStats()
    {
        if (count == 0)
        {
            averageDelta = latestDelta;
            minDelta = latestDelta;
            maxDelta = latestDelta;
            newestTimestamp = Time.realtimeSinceStartup;
            oldestTimestamp = newestTimestamp - latestDelta;
            return;
        }

        var total = 0f;
        var minValue = float.MaxValue;
        var maxValue = 0f;

        for (var i = 0; i < count; i++)
        {
            var index = (head + i) % MaxSamples;
            var delta = sampleDurations[index];
            total += delta;
            if (delta < minValue)
            {
                minValue = delta;
            }

            if (delta > maxValue)
            {
                maxValue = delta;
            }
        }

        averageDelta = total / count;
        minDelta = minValue;
        maxDelta = maxValue;
        oldestTimestamp = sampleTimes[head];
        newestTimestamp = sampleTimes[(head + count - 1) % MaxSamples];
    }

    private void DrawGraph(Rect graphRect, float graphMaxMs)
    {
        DrawRect(graphRect, new Color(0f, 0f, 0f, 0.2f));
        DrawOutline(graphRect, new Color(1f, 1f, 1f, 0.08f));

        DrawReferenceLine(graphRect, graphMaxMs, GoodFrameMs, "16.7 ms");
        DrawReferenceLine(graphRect, graphMaxMs, WarningFrameMs, "33.3 ms");

        if (count == 0)
        {
            return;
        }

        var now = newestTimestamp;
        for (var i = 0; i < count; i++)
        {
            var index = (head + i) % MaxSamples;
            var age = now - sampleTimes[index];
            var normalizedX = 1f - Mathf.Clamp01(age / WindowSeconds);
            var x = graphRect.x + normalizedX * graphRect.width;
            var frameMs = sampleDurations[index] * 1000f;
            var normalizedHeight = Mathf.Clamp01(frameMs / graphMaxMs);
            var barHeight = Mathf.Max(1f, normalizedHeight * graphRect.height);
            var y = graphRect.yMax - barHeight;
            DrawRect(new Rect(x, y, 2f, barHeight), GetFrameColor(frameMs));
        }
    }

    private void DrawReferenceLine(Rect graphRect, float graphMaxMs, float frameMs, string label)
    {
        var normalizedHeight = Mathf.Clamp01(frameMs / graphMaxMs);
        var y = graphRect.yMax - normalizedHeight * graphRect.height;
        DrawRect(new Rect(graphRect.x, y, graphRect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
        GUI.Label(new Rect(graphRect.x + 6f, y - 14f, 64f, 14f), label, captionStyle);
    }

    private void DrawMetrics(Rect metricsRect)
    {
        var currentMs = latestDelta * 1000f;
        var averageMs = averageDelta * 1000f;
        var bestFps = 1f / Mathf.Max(minDelta, 0.0001f);
        var worstFps = 1f / Mathf.Max(maxDelta, 0.0001f);
        var averageFps = 1f / Mathf.Max(averageDelta, 0.0001f);
        var timeSpan = Mathf.Max(newestTimestamp - oldestTimestamp, averageDelta);

        GUI.Label(
            new Rect(metricsRect.x, metricsRect.y, metricsRect.width, 20f),
            $"Current: {currentMs,6:0.00} ms   {1f / Mathf.Max(latestDelta, 0.0001f),6:0.0} FPS",
            bodyStyle);
        GUI.Label(
            new Rect(metricsRect.x, metricsRect.y + 18f, metricsRect.width, 20f),
            $"1s Avg:  {averageMs,6:0.00} ms   {averageFps,6:0.0} FPS   Best: {bestFps,6:0.0}   Worst: {worstFps,6:0.0}",
            bodyStyle);
        GUI.Label(
            new Rect(metricsRect.x, metricsRect.y + 36f, metricsRect.width, 20f),
            $"Samples: {count:0} over {timeSpan:0.00}s",
            bodyStyle);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.88f, 0.92f, 0.96f, 1f) }
        };
        captionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
        };
    }

    private static Color GetFrameColor(float frameMs)
    {
        if (frameMs <= GoodFrameMs)
        {
            return new Color(0.28f, 0.84f, 0.48f, 0.9f);
        }

        if (frameMs <= WarningFrameMs)
        {
            return new Color(0.95f, 0.76f, 0.25f, 0.9f);
        }

        return new Color(0.95f, 0.38f, 0.34f, 0.9f);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        var previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
