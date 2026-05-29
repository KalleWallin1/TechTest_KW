using UnityEngine;

[ExecuteAlways]
public class AutoPlayDataManager : MonoBehaviour
{
    private MorphDataManager manager;
    private float timer = 0f;
    public float fps = 24f;

    void Start()
    {
        manager = GetComponent<MorphDataManager>();
    }

    void Update()
    {
        if (manager == null) return;
        timer += Time.deltaTime;
        // Animation is 250 frames
        manager.currentFrame = (Mathf.FloorToInt(timer * fps) % 250) + 1;
    }
}
