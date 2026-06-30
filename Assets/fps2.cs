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
        deltaTimeSum += Time.deltaTime; //Total time elapsed
        float fps = frameCount / deltaTimeSum; //Total number of frames drawn
        float flops = fps*20*Mathf.Pow(Nforflops, 2) / 1e9f; // Calculate and convert to GFLOPS
        fpstext.text = $"{fps:F1} FPS {flops:F1} GFLOPS"; //Display in a text box
    }
}
