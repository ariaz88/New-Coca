using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity Editor Window for easy V2 system setup
/// </summary>
public class V2SystemSetupWindow : EditorWindow
{
    private GameObject boardGameObject;
    private GameObject boxPrefab;
    private GameObject nodePrefab;
    
    [MenuItem("Tools/Coca Sorting V2 Setup")]
    public static void ShowWindow()
    {
        V2SystemSetupWindow window = GetWindow<V2SystemSetupWindow>("V2 System Setup");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }
    
    void OnGUI()
    {
        GUILayout.Label("🚀 Coca Sorting V2 System Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox("This tool will automatically setup the V2 system to fix all transfer bugs!", MessageType.Info);
        GUILayout.Space(10);
        
        // Step 1: Board Setup
        GUILayout.Label("📋 Step 1: Board GameObject Setup", EditorStyles.boldLabel);
        boardGameObject = (GameObject)EditorGUILayout.ObjectField("Board GameObject", boardGameObject, typeof(GameObject), true);
        
        if (boardGameObject != null)
        {
            Board oldBoard = boardGameObject.GetComponent<Board>();
            BoardControllerV2 newBoard = boardGameObject.GetComponent<BoardControllerV2>();
            
            if (oldBoard != null)
            {
                EditorGUILayout.HelpBox($"✅ Found Board component on {boardGameObject.name}", MessageType.None);
                
                if (newBoard != null)
                {
                    EditorGUILayout.HelpBox("✅ BoardControllerV2 already added!", MessageType.None);
                }
                else
                {
                    if (GUILayout.Button("🔧 Add BoardControllerV2 Component"))
                    {
                        AutoSetupV2 autoSetup = boardGameObject.GetComponent<AutoSetupV2>();
                        if (autoSetup == null)
                        {
                            autoSetup = boardGameObject.AddComponent<AutoSetupV2>();
                        }
                        autoSetup.SetupV2System();
                        Debug.Log("✅ BoardControllerV2 setup complete!");
                    }
                }
                
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(oldBoard.enabled ? "🔴 Disable Old Board" : "✅ Old Board Disabled"))
                {
                    oldBoard.enabled = !oldBoard.enabled;
                    EditorUtility.SetDirty(boardGameObject);
                }
                if (newBoard != null)
                {
                    if (GUILayout.Button(newBoard.enabled ? "✅ V2 Board Enabled" : "🟢 Enable V2 Board"))
                    {
                        newBoard.enabled = !newBoard.enabled;
                        EditorUtility.SetDirty(boardGameObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("❌ No Board component found! Please select the correct GameObject.", MessageType.Error);
            }
        }
        
        GUILayout.Space(20);
        
        // Step 2: Prefab Setup
        GUILayout.Label("📦 Step 2: Prefab Setup", EditorStyles.boldLabel);
        boxPrefab = (GameObject)EditorGUILayout.ObjectField("Box Prefab", boxPrefab, typeof(GameObject), false);
        nodePrefab = (GameObject)EditorGUILayout.ObjectField("Node Prefab", nodePrefab, typeof(GameObject), false);
        
        if (boxPrefab != null)
        {
            Box originalBox = boxPrefab.GetComponent<Box>();
            //BoxV2Simple boxV2 = boxPrefab.GetComponent<BoxV2Simple>();
            
            if (originalBox != null)
            {
                EditorGUILayout.HelpBox($"✅ Found Box component on {boxPrefab.name}", MessageType.None);
                
                //if (boxV2 != null)
                //{
                //    EditorGUILayout.HelpBox("✅ BoxV2Simple already added!", MessageType.None);
                //}
              
                    if (GUILayout.Button("🔧 Add BoxV2Simple Component"))
                    {
                        BoxPrefabAutoSetup autoSetup = boxPrefab.GetComponent<BoxPrefabAutoSetup>();
                        if (autoSetup == null)
                        {
                            autoSetup = boxPrefab.AddComponent<BoxPrefabAutoSetup>();
                        }
                        autoSetup.SetupBoxV2();
                        EditorUtility.SetDirty(boxPrefab);
                        AssetDatabase.SaveAssets();
                        Debug.Log("✅ BoxV2Simple setup complete!");
                    }
                
            }
            else
            {
                EditorGUILayout.HelpBox("❌ No Box component found on this prefab!", MessageType.Error);
            }
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("🔍 Auto-Find All Box Prefabs and Setup"))
        {
            SetupAllBoxPrefabs();
        }
        
        GUILayout.Space(20);
        
        // Step 3: Testing
        GUILayout.Label("🧪 Step 3: Testing & Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Debug Controls:\n• Press T = Show board state\n• Press C = Show transfer cooldowns", MessageType.Info);
        
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("✅ Game is running! Test the debug controls now.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("▶️ Press Play to test the V2 system!", MessageType.None);
        }
        
        GUILayout.Space(20);
        
        // Emergency Rollback
        GUILayout.Label("🚨 Emergency Rollback", EditorStyles.boldLabel);
        if (GUILayout.Button("🔄 Rollback to Original System"))
        {
            if (boardGameObject != null)
            {
                AutoSetupV2 autoSetup = boardGameObject.GetComponent<AutoSetupV2>();
                if (autoSetup != null)
                {
                    autoSetup.RollbackToOriginal();
                }
            }
            Debug.Log("🔄 Rollback initiated!");
        }
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("⚠️ Rollback will restore the original system if V2 causes issues.", MessageType.Warning);
    }
    
    private void SetupAllBoxPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int setupCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null && prefab.GetComponent<Box>() != null)
            {
                BoxPrefabAutoSetup autoSetup = prefab.GetComponent<BoxPrefabAutoSetup>();
                if (autoSetup == null)
                {
                    autoSetup = prefab.AddComponent<BoxPrefabAutoSetup>();
                }
                
                autoSetup.SetupBoxV2();
                setupCount++;
                
                EditorUtility.SetDirty(prefab);
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 Auto-setup complete! Configured {setupCount} Box prefabs with BoxV2Simple.");
    }
}