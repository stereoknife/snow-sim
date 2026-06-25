using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SetTransform : MonoBehaviour
{
    [SerializeField] private Transform[] transforms;

    private void Update()
    {
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 0) Set(0);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 1) Set(1);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 2) Set(2);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 3) Set(3);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 4) Set(4);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 5) Set(5);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 6) Set(6);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 7) Set(7);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 8) Set(8);
        if (Keyboard.current.digit0Key.wasPressedThisFrame && transforms.Length > 9) Set(9);
    }

    private void Set(int i)
    {
        transform.SetPositionAndRotation(transforms[i].position, transforms[i].rotation);
    }
}
