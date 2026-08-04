using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// BoxV2 - Enhanced Box component with anti-ping-pong system
/// This works as a wrapper around the original Box component
/// </summary>
public class BoxV2 : MonoBehaviour
{
    [Header("Box V2 - Enhanced with Anti-Ping-Pong")]
    
    // Reference to the original Box component
    private Box originalBox;
    
    // V2 ENHANCEMENT: Anti ping-pong system
    private float lastTransferTime = 0f;
    private const float TRANSFER_COOLDOWN = 1f; // 1 second cooldown between transfers

    void Start()
    {
        // Get or add the original Box component
        originalBox = GetComponent<Box>();
        if (originalBox == null)
        {
            Debug.LogError("BoxV2 requires a Box component on the same GameObject!");
            return;
        }
        
        Debug.Log($"BoxV2 initialized: {gameObject.name}");
    }

    // V2 ENHANCEMENT: Transfer cooldown methods
    public bool CanParticipateInTransfer()
    {
        return Time.time > lastTransferTime + TRANSFER_COOLDOWN;
    }
    
    public void MarkTransferTime()
    {
        lastTransferTime = Time.time;
    }
    
    // Delegate properties to original Box
    public List<Soda> Sodas => originalBox.Sodas;
    public bool IsOnBoard 
    { 
        get => originalBox.IsOnBoard; 
        set => originalBox.IsOnBoard = value; 
    }
    public int column 
    { 
        get => originalBox.column; 
        set => originalBox.column = value; 
    }
    public int row 
    { 
        get => originalBox.row; 
        set => originalBox.row = value; 
    }
    public bool IsDragged 
    { 
        get => originalBox.IsDragged; 
        set => originalBox.IsDragged = value; 
    }
    public float PlacementTimestamp => originalBox.PlacementTimestamp;
    
    // Delegate methods to original Box
    public Dictionary<Soda.SodaColor, int> GetSodaColorCounts()
    {
        return originalBox.GetSodaColorCounts();
    }
    
    public int GetSodasCount()
    {
        return originalBox.GetSodasCount();
    }
    
    public int GetColorCount(Soda.SodaColor color)
    {
        return originalBox.GetColorCount(color);
    }
    
    public int GetAvailableSpaces()
    {
        return originalBox.GetAvailableSpaces();
    }
    
    public bool HasCapacity()
    {
        return originalBox.HasCapacity();
    }
    
    public bool HasSodaOfColor(Soda.SodaColor color)
    {
        return originalBox.HasSodaOfColor(color);
    }
    
    public bool BoxFilled()
    {
        return originalBox.BoxFilled();
    }
    
    public List<Transform> GetEmptySodaPositions()
    {
        return originalBox.GetEmptySodaPositions();
    }
    
    public void UpdateEmptyPositions()
    {
        originalBox.UpdateEmptyPositions();
    }
    
    public void RearrangeSodas()
    {
        originalBox.RearrangeSodas();
    }
    
    // V2 ENHANCEMENT: Enhanced AddSoda with transfer tracking
    public void AddSoda(Soda soda)
    {
        originalBox.AddSoda(soda);
        MarkTransferTime(); // V2: Mark transfer time when soda is added
    }
    
    // V2 ENHANCEMENT: Enhanced RemoveSoda with transfer tracking
    public void RemoveSoda(Soda soda)
    {
        originalBox.RemoveSoda(soda);
        MarkTransferTime(); // V2: Mark transfer time when soda is removed
    }
    
    // Additional V2 utility methods
    public int GetUniqueColorCount()
    {
        var sodaCounts = GetSodaColorCounts();
        return sodaCounts.Count;
    }
    
    public HashSet<Soda.SodaColor> GetDistinctColors()
    {
        var sodaCounts = GetSodaColorCounts();
        return new HashSet<Soda.SodaColor>(sodaCounts.Keys);
    }
    
    // Convenience method to get the underlying Box component
    public Box GetOriginalBox()
    {
        return originalBox;
    }
}