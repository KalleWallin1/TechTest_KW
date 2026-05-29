using UnityEngine;
using System.Globalization;

[ExecuteAlways]
[DefaultExecutionOrder(-10)]
public class Part1Controller : MonoBehaviour
{
    [Header("CSV Data")]
    public TextAsset csvFile;

    [Header("Playback")]
    [Range(1, 250)]
    public int currentFrame = 1;
    public bool autoPlay = true;
    public bool loop = true;
    public float playbackSpeed = 1f;

    [Header("Shape")]
    public MorphController morphController;
    public Renderer shapeRenderer;
    public Transform shapePivot;

    [Header("Number Cube")]
    public Transform numberCubeTransform;
    public Renderer numberRenderer;

    [Header("State Colors")]
    public Color state1Color = new Color(0.78f, 0.78f, 0.24f, 1f);
    public Color state2Color = new Color(0.80f, 0.33f, 0.27f, 1f);
    public Color state3Color = new Color(0.19f, 0.80f, 0.19f, 1f);
    public Color state4Color = new Color(0.27f, 0.53f, 0.87f, 1f);

    [Header("Morph Timing (frames after each state trigger)")]
    public int morphDuration12 = 70;
    public int morphDuration23 = 44;
    public int morphDuration34 = 18;

    [Header("Rotation")]
    [Tooltip("Z-axis rotation of shape during each morph transition")]
    public float shapeDegreesPerMorph = 360f;
    [Tooltip("X-axis rotation of number cube per number change")]
    public float numberDegreesPerStep = 90f;

    [Header("State 4 Pulse")]
    public float pulseAmount = 1f;
    public float pulseFrequency = 2f;

    [Header("Position")]
    public bool applyCSVPosition = true;
    public float worldScale = 0.05f;
    public Vector3 positionOffset;

    private const float FPS = 24f;
    private float playbackTime;
    private int maxFrame;
    private bool isLoaded;
    private TextAsset loadedCsv;

    private int[] frameStates;
    private Vector3[] framePositions;
    private bool[] frameHasPosition;

    private int trigger2, trigger3, trigger4;
    private MaterialPropertyBlock shapeMPB;
    private MaterialPropertyBlock numberMPB;
    private Vector3 lastKnownPosition;

    void OnEnable()
    {
        shapeMPB = new MaterialPropertyBlock();
        numberMPB = new MaterialPropertyBlock();
        if (csvFile != null) LoadCSV();
    }

    void Update()
    {
        if (csvFile != loadedCsv)
        {
            if (csvFile != null) LoadCSV();
            else { isLoaded = false; loadedCsv = null; }
        }
        if (!isLoaded) return;

        if (autoPlay && Application.isPlaying)
        {
            playbackTime += Time.deltaTime * playbackSpeed;
            currentFrame = Mathf.Clamp(1 + Mathf.FloorToInt(playbackTime * FPS), 1, maxFrame);
            if (currentFrame >= maxFrame)
            {
                if (loop) { playbackTime = 0f; currentFrame = 1; }
                else currentFrame = maxFrame;
            }
        }

        ApplyFrame(currentFrame);
    }

    void ApplyFrame(int frame)
    {
        frame = Mathf.Clamp(frame, 1, maxFrame);

        float morph = CalcMorphState(frame);
        Color color = CalcColor(frame);
        float shapeRot = CalcShapeRotation(frame);
        float numberAngle = CalcNumberAngle(frame);
        bool isState4 = trigger4 > 0 && frame >= trigger4;

        if (morphController != null)
            morphController.morphState = morph;

        if (shapeRenderer != null)
        {
            shapeRenderer.GetPropertyBlock(shapeMPB);
            shapeMPB.SetColor("_Color", color);
            shapeRenderer.SetPropertyBlock(shapeMPB);
        }

        if (numberRenderer != null)
        {
            numberRenderer.GetPropertyBlock(numberMPB);
            numberMPB.SetColor("_Color", color);
            numberMPB.SetFloat("_PulseAmount", isState4 ? pulseAmount : 0f);
            numberMPB.SetFloat("_PulseFrequency", pulseFrequency);
            numberRenderer.SetPropertyBlock(numberMPB);
        }

        if (shapePivot != null)
            shapePivot.localEulerAngles = new Vector3(0f, 0f, shapeRot);

        if (numberCubeTransform != null)
            numberCubeTransform.localEulerAngles = new Vector3(numberAngle, 0f, 0f);

        if (applyCSVPosition)
        {
            if (frameHasPosition[frame])
                lastKnownPosition = framePositions[frame] * worldScale + positionOffset;
            transform.localPosition = lastKnownPosition;
        }
    }

    float CalcMorphState(int frame)
    {
        if (trigger2 <= 0 || frame < trigger2) return 0f;
        int end1 = trigger2 + morphDuration12;
        if (frame <= end1)
            return Mathf.InverseLerp(trigger2, end1, frame);

        if (trigger3 <= 0 || frame < trigger3) return 1f;
        int end2 = trigger3 + morphDuration23;
        if (frame <= end2)
            return 1f + Mathf.InverseLerp(trigger3, end2, frame);

        if (trigger4 <= 0 || frame < trigger4) return 2f;
        int end3 = trigger4 + morphDuration34;
        if (frame <= end3)
            return 2f + Mathf.InverseLerp(trigger4, end3, frame);

        return 3f;
    }

    Color CalcColor(int frame)
    {
        if (trigger2 <= 0 || frame < trigger2)
            return state1Color;

        int end1 = trigger2 + morphDuration12;
        if (frame <= end1)
            return Color.Lerp(state1Color, state2Color, Mathf.InverseLerp(trigger2, end1, frame));

        if (trigger3 <= 0 || frame < trigger3)
            return state2Color;

        int end2 = trigger3 + morphDuration23;
        if (frame <= end2)
            return Color.Lerp(state2Color, state3Color, Mathf.InverseLerp(trigger3, end2, frame));

        if (trigger4 <= 0 || frame < trigger4)
            return state3Color;

        int end3 = trigger4 + morphDuration34;
        if (frame <= end3)
            return Color.Lerp(state3Color, state4Color, Mathf.InverseLerp(trigger4, end3, frame));

        return state4Color;
    }

    float CalcShapeRotation(int frame)
    {
        if (trigger2 > 0 && frame >= trigger2 && frame <= trigger2 + morphDuration12)
            return Mathf.InverseLerp(trigger2, trigger2 + morphDuration12, frame) * shapeDegreesPerMorph;

        if (trigger3 > 0 && frame >= trigger3 && frame <= trigger3 + morphDuration23)
            return Mathf.InverseLerp(trigger3, trigger3 + morphDuration23, frame) * shapeDegreesPerMorph;

        if (trigger4 > 0 && frame >= trigger4 && frame <= trigger4 + morphDuration34)
            return Mathf.InverseLerp(trigger4, trigger4 + morphDuration34, frame) * shapeDegreesPerMorph;

        return 0f;
    }

    float CalcNumberAngle(int frame)
    {
        if (trigger2 <= 0 || frame < trigger2)
            return 0f;

        int end1 = trigger2 + morphDuration12;
        float angle = Mathf.Clamp01(Mathf.InverseLerp(trigger2, end1, frame)) * numberDegreesPerStep;

        if (trigger3 <= 0 || frame < trigger3)
            return angle;

        int end2 = trigger3 + morphDuration23;
        angle = numberDegreesPerStep
              + Mathf.Clamp01(Mathf.InverseLerp(trigger3, end2, frame)) * numberDegreesPerStep;

        if (trigger4 <= 0 || frame < trigger4)
            return angle;

        int end3 = trigger4 + morphDuration34;
        angle = 2f * numberDegreesPerStep
              + Mathf.Clamp01(Mathf.InverseLerp(trigger4, end3, frame)) * numberDegreesPerStep;

        return angle;
    }

    void LoadCSV()
    {
        if (csvFile == null) return;

        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        maxFrame = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Trim().Split(',');
            if (cols.Length >= 1 && int.TryParse(cols[0].Trim(), out int f))
                maxFrame = Mathf.Max(maxFrame, f);
        }
        if (maxFrame <= 0) return;

        frameStates = new int[maxFrame + 1];
        framePositions = new Vector3[maxFrame + 1];
        frameHasPosition = new bool[maxFrame + 1];

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] cols = line.Split(',');
            if (cols.Length < 2) continue;

            if (!int.TryParse(cols[0].Trim(), out int frame)) continue;
            if (frame < 1 || frame > maxFrame) continue;

            if (cols.Length >= 6 && int.TryParse(cols[5].Trim(), out int state))
                frameStates[frame] = state;

            if (cols.Length >= 5
                && !string.IsNullOrWhiteSpace(cols[2])
                && float.TryParse(cols[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(cols[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(cols[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float z))
            {
                framePositions[frame] = new Vector3(x, y, z);
                frameHasPosition[frame] = true;
            }
        }

        trigger2 = trigger3 = trigger4 = 0;
        for (int f = 2; f <= maxFrame; f++)
        {
            if (frameStates[f] != frameStates[f - 1])
            {
                if (frameStates[f] == 2 && trigger2 == 0) trigger2 = f;
                else if (frameStates[f] == 3 && trigger3 == 0) trigger3 = f;
                else if (frameStates[f] == 4 && trigger4 == 0) trigger4 = f;
            }
        }

        isLoaded = true;
        loadedCsv = csvFile;
        lastKnownPosition = positionOffset;

        Debug.Log($"[Part1Controller] Loaded {maxFrame} frames. " +
                  $"State triggers: 2@{trigger2}, 3@{trigger3}, 4@{trigger4}");
    }
}
