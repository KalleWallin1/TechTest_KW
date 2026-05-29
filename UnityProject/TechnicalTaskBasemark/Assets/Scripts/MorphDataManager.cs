using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class MorphDataManager : MonoBehaviour
{
    public TextAsset csvFile;
    public GameObject prefab;
    
    [Range(1, 5000)]
    public int currentFrame = 1;

    [Range(0.001f, 1f)]
    [Tooltip("Multiplier for CSV positions to scale the entire scene down/up.")]
    public float worldScale = 1.0f;
    
    private struct DataRow
    {
        public int frame;
        public int id;
        public Vector3 pos;
        public int state;
        public bool hasPos;
    }

    private Dictionary<int, List<DataRow>> dataByFrame = new Dictionary<int, List<DataRow>>();
    private Dictionary<int, Dictionary<int, int>> stateStartFrames = new Dictionary<int, Dictionary<int, int>>(); 
    private bool isLoaded = false;
    private TextAsset lastLoadedCsv;

    void Update()
    {
        if (csvFile != lastLoadedCsv || (!isLoaded && csvFile != null))
        {
            LoadCSV();
        }

        UpdateScene();
    }

    public void LoadCSV()
    {
        if (csvFile == null)
        {
            dataByFrame.Clear();
            stateStartFrames.Clear();
            isLoaded = false;
            lastLoadedCsv = null;
            return;
        }
        
        dataByFrame.Clear();
        stateStartFrames.Clear();
        
        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        int rowsParsed = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            string[] cols = line.Split(',');
            if (cols.Length < 2) continue;

            if (int.TryParse(cols[0].Trim(), out int f) && int.TryParse(cols[1].Trim(), out int id))
            {
                DataRow row = new DataRow { frame = f, id = id };
                
                if (cols.Length >= 5 && 
                    float.TryParse(cols[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float x) && 
                    float.TryParse(cols[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float y) && 
                    float.TryParse(cols[4].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float z))
                {
                    row.pos = new Vector3(x, y, z);
                    row.hasPos = true;
                }
                
                if (cols.Length >= 6 && int.TryParse(cols[5].Trim(), out int s)) row.state = s;

                if (!dataByFrame.ContainsKey(f)) dataByFrame[f] = new List<DataRow>();
                dataByFrame[f].Add(row);

                if (!stateStartFrames.ContainsKey(id)) stateStartFrames[id] = new Dictionary<int, int>();
                if (!stateStartFrames[id].ContainsKey(row.state))
                {
                    stateStartFrames[id][row.state] = f;
                }
                rowsParsed++;
            }
        }
        isLoaded = true;
        lastLoadedCsv = csvFile;
        Debug.Log($"[MorphDataManager] Loaded {dataByFrame.Count} unique frames and {rowsParsed} rows.");
    }

    public void UpdateScene()
    {
        if (!isLoaded || prefab == null) return;

        HashSet<int> activeIds = new HashSet<int>();
        if (dataByFrame.TryGetValue(currentFrame, out List<DataRow> rows))
        {
            foreach (var row in rows)
            {
                activeIds.Add(row.id);
                
                string objName = "Object_" + row.id;
                Transform objT = transform.Find(objName);
                GameObject obj;

                if (objT == null)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        obj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
                    }
                    else
                    {
                        obj = Instantiate(prefab, transform);
                    }
                    #else
                    obj = Instantiate(prefab, transform);
                    #endif
                    obj.name = objName;
                }
                else
                {
                    obj = objT.gameObject;
                }

                obj.SetActive(true);
                
                if (row.hasPos)
                {
                    obj.transform.localPosition = row.pos * worldScale;
                }

                float targetFrame = 1f;
                if (stateStartFrames.TryGetValue(row.id, out var objectStates) && objectStates.TryGetValue(row.state, out int startFrame))
                {
                    int elapsed = currentFrame - startFrame;
                    switch (row.state)
                    {
                        case 1: targetFrame = 1f; break;
                        case 2: targetFrame = Mathf.Min(43f + elapsed, 113f); break;
                        case 3: targetFrame = Mathf.Min(181f + elapsed, 225f); break;
                        case 4: targetFrame = Mathf.Min(231f + elapsed, 249f); break;
                    }
                }

                Animator anim = obj.GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    anim.enabled = true;
                    float length = anim.runtimeAnimatorController.animationClips[0].length;
                    float time = (targetFrame - 1f) / 24f;
                    anim.Play(0, 0, Mathf.Clamp01(time / length));
                    anim.speed = 0;
                    anim.Update(0);
                }
            }
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Object_"))
            {
                string idStr = child.name.Replace("Object_", "");
                if (int.TryParse(idStr, out int id))
                {
                    if (!activeIds.Contains(id))
                    {
                        if (Application.isPlaying) child.gameObject.SetActive(false);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
    }
}
