using UnityEngine;

public class TransferSystemDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    public bool enableDebugLogs = true;
    public bool showTransferCooldowns = true;
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T) && enableDebugLogs)
        {
            LogCurrentBoardState();
        }
        
        if (Input.GetKeyDown(KeyCode.C) && showTransferCooldowns)
        {
            ShowTransferCooldowns();
        }
    }
    
    private void LogCurrentBoardState()
    {
        Debug.Log("=== BOARD STATE ===");
        
        if (Board.instance == null) return;
        
        for (int i = 0; i < Board.instance.Width; i++)
        {
            for (int j = 0; j < Board.instance.Height; j++)
            {
                var box = Board.instance.allBoxes[i, j];
                if (box != null)
                {
                    var colors = box.GetSodaColorCounts();
                    string colorInfo = "";
                    foreach (var kvp in colors)
                    {
                        colorInfo += $"{kvp.Key}:{kvp.Value} ";
                    }
                    Debug.Log($"Box at ({i},{j}): {colorInfo.Trim()}");
                }
            }
        }
    }
    
    private void ShowTransferCooldowns()
    {
        Debug.Log("=== TRANSFER COOLDOWNS ===");
        
        if (Board.instance == null) return;
        
        for (int i = 0; i < Board.instance.Width; i++)
        {
            for (int j = 0; j < Board.instance.Height; j++)
            {
                var box = Board.instance.allBoxes[i, j];
                if (box != null)
                {
                    bool canTransfer = box.CanParticipateInTransfer();
                    Debug.Log($"Box at ({i},{j}): Can Transfer = {canTransfer}");
                }
            }
        }
    }
}