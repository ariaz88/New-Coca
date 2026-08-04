using UnityEngine;

/// <summary>
/// Auto-Setup Script for V2 System
/// Just add this to your Board GameObject and it will configure everything automatically!
/// </summary>
public class AutoSetupV2 : MonoBehaviour
{
    [Header("Auto Setup V2 System")]
    [SerializeField] private bool enableV2System = true;
    [SerializeField] private bool disableOldBoard = true;
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private GameObject boxPrefab;
    
    [Header("Status")]
    [SerializeField] private bool isSetupComplete = false;
    
    void Start()
    {
        if (enableV2System && !isSetupComplete)
        {
            SetupV2System();
        }
    }
    
    [ContextMenu("Setup V2 System")]
    public void SetupV2System()
    {
        Debug.Log("🚀 Starting Auto Setup V2 System...");
        
        // Step 1: Add BoardControllerV2 if not present
        BoardControllerV2 boardV2 = GetComponent<BoardControllerV2>();
        if (boardV2 == null)
        {
            boardV2 = gameObject.AddComponent<BoardControllerV2>();
            Debug.Log("✅ Added BoardControllerV2 component");
        }
        
        // Step 2: Add UniversalSodaTransferSystem if not present
        UniversalSodaTransferSystem universalSystem = GetComponent<UniversalSodaTransferSystem>();
        if (universalSystem == null)
        {
            universalSystem = gameObject.AddComponent<UniversalSodaTransferSystem>();
            Debug.Log("✅ Added UniversalSodaTransferSystem component");
        }
        
        // Step 3: Add TransferSystemDebugger for testing
        TransferSystemDebugger debugger = GetComponent<TransferSystemDebugger>();
        if (debugger == null)
        {
            debugger = gameObject.AddComponent<TransferSystemDebugger>();
            Debug.Log("✅ Added TransferSystemDebugger component");
        }
        
        // Step 4: Disable old Board component if requested
        if (disableOldBoard)
        {
            Board oldBoard = GetComponent<Board>();
            if (oldBoard != null && oldBoard.enabled)
            {
                oldBoard.enabled = false;
                Debug.Log("✅ Disabled old Board component");
            }
        }
        
        // Step 5: Configure references if provided
        if (nodePrefab != null && boxPrefab != null)
        {
            // Use reflection to set private fields if needed
            var nodeField = typeof(BoardControllerV2).GetField("nodePref", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var boxField = typeof(BoardControllerV2).GetField("boxPref", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
            if (nodeField != null) nodeField.SetValue(boardV2, nodePrefab);
            if (boxField != null) boxField.SetValue(boardV2, boxPrefab);
            
            Debug.Log("✅ Configured prefab references");
        }
        
        isSetupComplete = true;
        Debug.Log("🎉 V2 System Setup Complete! You can now test the game.");
        Debug.Log("📋 Next: Add BoxV2Simple to your Box prefabs using the BoxPrefabAutoSetup script.");
    }
    
    [ContextMenu("Rollback to Original System")]
    public void RollbackToOriginal()
    {
        Debug.Log("🔄 Rolling back to original system...");
        
        // Enable old Board
        Board oldBoard = GetComponent<Board>();
        if (oldBoard != null)
        {
            oldBoard.enabled = true;
            Debug.Log("✅ Enabled old Board component");
        }
        
        // Disable V2 components
        BoardControllerV2 boardV2 = GetComponent<BoardControllerV2>();
        if (boardV2 != null) boardV2.enabled = false;
        
        UniversalSodaTransferSystem universalSystem = GetComponent<UniversalSodaTransferSystem>();
        if (universalSystem != null) universalSystem.enabled = false;
        
        TransferSystemDebugger debugger = GetComponent<TransferSystemDebugger>();
        if (debugger != null) debugger.enabled = false;
        
        isSetupComplete = false;
        Debug.Log("🎉 Rollback Complete! Original system restored.");
    }
    
    void OnValidate()
    {
        // Auto-find prefabs if not set
        if (nodePrefab == null)
        {
            // Try to find node prefab in Resources or common locations
            GameObject foundNode = Resources.Load<GameObject>("NodePrefab");
            if (foundNode == null)
                foundNode = Resources.Load<GameObject>("Node");
            if (foundNode != null)
                nodePrefab = foundNode;
        }
        
        if (boxPrefab == null)
        {
            // Try to find box prefab in Resources or common locations
            GameObject foundBox = Resources.Load<GameObject>("BoxPrefab");
            if (foundBox == null)
                foundBox = Resources.Load<GameObject>("Box");
            if (foundBox != null)
                boxPrefab = foundBox;
        }
    }
}
