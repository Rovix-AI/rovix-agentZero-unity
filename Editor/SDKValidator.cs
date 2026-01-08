using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SDKValidator
{
    static SDKValidator()
    {
        Debug.Log("Rovix SDK loaded in Editor");
    }
}
