using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraToggler : MonoBehaviour
{
    private FreeCamera _freeCamera;
    
    void Awake()
    {
        _freeCamera = GetComponent<FreeCamera>();
    }
    
    void Update()
    {
        _freeCamera.enabled = Mouse.current?.rightButton?.isPressed ?? false;
    }
}
