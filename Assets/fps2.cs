using TMPro;
using UnityEngine;

public class FPSCounter2 : MonoBehaviour
{
    public TextMeshProUGUI fpstext;
    [SerializeField] int Nforflops = 1000;
    int frameCount = 0;
    float deltaTimeSum = 0f;

    void Update()
    {
        frameCount++;
        deltaTimeSum += Time.deltaTime;
        float fps = frameCount / deltaTimeSum;
        fps = fps*20*Mathf.Pow(Nforflops, 2) / 1e9f; // Convert to GFLOPS
        fpstext.text = $"{fps:F1} GFLOPS";
    }
}
