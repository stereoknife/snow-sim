using System;
using UnityEngine;

public class TestTextScript : MonoBehaviour
{
    [SerializeField] private Texture2D tex;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnValidate()
    {
        if (tex == null) return;

        Debug.Log(tex.format);
        
        ushort min = ushort.MaxValue;
        ushort max = 0;

        var data = tex.GetPixelData<ushort>(0);
        foreach (ushort c in data)
        {
            if (c < min) min = c;
            if (c > max) max = c;
        }
        
        Debug.Log($"Min: {min}, Max: {max}");
        Debug.Log($"Ushort max: {ushort.MaxValue}");
    }
}
