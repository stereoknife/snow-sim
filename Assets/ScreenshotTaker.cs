using System.IO;
using EasyButtons;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenshotTaker : MonoBehaviour
{
    [SerializeField] private string filename;

    private int num = 0;
    
    [Button]
    private void TakeScreenshot()
    {
        new FileInfo($"{Application.persistentDataPath}/screenshots/").Directory?.Create();
        ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/screenshots/{filename}-{num++}.png");
    }
    
    void Update()
    {
        if (Keyboard.current.pKey.wasReleasedThisFrame)
        {
            TakeScreenshot();
        }
    }
}
