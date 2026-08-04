using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Auto-Setup Script for Box Prefabs
/// Add this to your Box prefabs and it will automatically configure BoxV2Simple
/// </summary>
public class BoxPrefabAutoSetup : MonoBehaviour
{
    [Header("Box Prefab Auto Setup")]
    [SerializeField] private bool enableBoxV2 = true;
    [SerializeField] private bool setupOnStart = true;
    
    [Header("Status")]
    [SerializeField] private bool isSetupComplete = false;
    
    void Start()
    {
        if (enableBoxV2 && setupOnStart && !isSetupComplete)
        {
            SetupBoxV2();
        }
    }
    
    [ContextMenu("Setup Box V2")]
    public void SetupBoxV2()
    {
        Debug.Log($"🚀 Setting up BoxV2Simple on {gameObject.name}...");
        
        // Step 1: Ensure original Box component exists and is enabled
        Box originalBox = GetComponent<Box>();
        if (originalBox == null)
        {
            Debug.LogError($"❌ No Box component found on {gameObject.name}! BoxV2Simple requires original Box component.");
            return;
        }
        
        if (!originalBox.enabled)
        {
            originalBox.enabled = true;
            Debug.Log("✅ Enabled original Box component");
        }
        
        // Step 2: Box is already enhanced with V2 features in the new Box.cs
        Debug.Log($"✅ Box V2 features already integrated in {gameObject.name}!");
        
        isSetupComplete = true;
        Debug.Log($"🎉 BoxV2 Setup Complete for {gameObject.name}!");
    }
    
    [ContextMenu("Remove Box V2")]
    public void RemoveBoxV2()
    {
        Debug.Log($"🔄 Box V2 features are integrated into Box.cs for {gameObject.name}...");
        Debug.Log($"🎉 No separate component to remove for {gameObject.name}!");
        isSetupComplete = false;
    }
    
    [ContextMenu("Batch Setup All Box Prefabs")]
    public void BatchSetupAllBoxPrefabs()
    {
#if UNITY_EDITOR
        Debug.Log("🎉 Box V2 features are now integrated directly into Box.cs!");
        Debug.Log("ℹ️ No separate components needed - all boxes automatically have V2 enhancements.");
#endif
    }
}