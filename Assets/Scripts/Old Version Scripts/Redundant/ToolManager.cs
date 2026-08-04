using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToolManager : MonoBehaviour
{
    public static ToolManager instance;

    public bool IsSwapActive { get; private set; }
    public bool IsHammerActive { get; private set; }

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void ActivateSwap()
    {
        IsSwapActive = true;
        IsHammerActive = false;
    }

    public void ActivateHammer()
    {
        IsHammerActive = true;
        IsSwapActive = false;
    }

    public void DeactivateAll()
    {
        IsSwapActive = false;
        IsHammerActive = false;
    }
}
