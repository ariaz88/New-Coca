using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SimpleSodaTransferSystem : MonoBehaviour
{
    private bool isTransferInProgress = false;
    private HashSet<(Box, Box)> processedPairs = new HashSet<(Box, Box)>();
    
    public void ProcessTransfers(Box currentBox, List<Box> adjacentBoxes)
    {
        if (isTransferInProgress) return;
        
        StartCoroutine(ProcessTransfersSequentially(currentBox, adjacentBoxes));
    }
    
    private IEnumerator ProcessTransfersSequentially(Box currentBox, List<Box> adjacentBoxes)
    {
        isTransferInProgress = true;
        processedPairs.Clear();
        
        // Find all possible transfers and prioritize them
        var transferPlan = CreateTransferPlan(currentBox, adjacentBoxes);
        
        // Execute transfers one by one to prevent conflicts
        foreach (var transfer in transferPlan)
        {
            yield return ExecuteTransfer(transfer);
            yield return new WaitForSeconds(0.3f); // Delay between transfers
        }
        
        // Check for chain reactions after all transfers complete
        yield return CheckForChainReactions(currentBox, adjacentBoxes);
        
        isTransferInProgress = false;
    }
    
    private List<TransferPlan> CreateTransferPlan(Box currentBox, List<Box> adjacentBoxes)
    {
        var transfers = new List<TransferPlan>();
        
        foreach (var adjacentBox in adjacentBoxes)
        {
            // Skip if this pair was already processed
            if (processedPairs.Contains((currentBox, adjacentBox)) || 
                processedPairs.Contains((adjacentBox, currentBox)))
                continue;
                
            var transfer = CalculateBestTransfer(currentBox, adjacentBox);
            if (transfer != null)
            {
                transfers.Add(transfer);
                processedPairs.Add((currentBox, adjacentBox));
            }
        }
        
        // Sort transfers by priority
        return transfers.OrderByDescending(t => t.Priority).ToList();
    }
    
    private TransferPlan CalculateBestTransfer(Box box1, Box box2)
    {
        // Check cooldown to prevent ping-pong effects
        if (!box1.CanParticipateInTransfer() || !box2.CanParticipateInTransfer())
            return null;
            
        // Find common colors
        var box1Colors = box1.GetSodaColorCounts();
        var box2Colors = box2.GetSodaColorCounts();
        var commonColors = box1Colors.Keys.Intersect(box2Colors.Keys).ToList();
        
        if (commonColors.Count == 0) return null;
        
        TransferPlan bestTransfer = null;
        int highestPriority = 0;
        
        foreach (var color in commonColors)
        {
            // Try transfer from box1 to box2
            var transfer1to2 = CreateTransferPlan(box1, box2, color);
            if (transfer1to2 != null && transfer1to2.Priority > highestPriority)
            {
                bestTransfer = transfer1to2;
                highestPriority = transfer1to2.Priority;
            }
            
            // Try transfer from box2 to box1
            var transfer2to1 = CreateTransferPlan(box2, box1, color);
            if (transfer2to1 != null && transfer2to1.Priority > highestPriority)
            {
                bestTransfer = transfer2to1;
                highestPriority = transfer2to1.Priority;
            }
        }
        
        return bestTransfer;
    }
    
    private TransferPlan CreateTransferPlan(Box sourceBox, Box targetBox, Soda.SodaColor color)
    {
        int sourceCount = sourceBox.GetColorCount(color);
        int targetCount = targetBox.GetColorCount(color);
        int targetSpace = targetBox.GetAvailableSpaces();
        
        if (sourceCount == 0 || targetSpace == 0) return null;
        
        // Calculate how many sodas to transfer
        int transferAmount = CalculateOptimalTransferAmount(sourceBox, targetBox, color);
        if (transferAmount <= 0) return null;
        
        // Calculate priority
        int priority = CalculateTransferPriority(sourceBox, targetBox, color, transferAmount);
        
        return new TransferPlan
        {
            SourceBox = sourceBox,
            TargetBox = targetBox,
            Color = color,
            Amount = transferAmount,
            Priority = priority
        };
    }
    
    private int CalculateOptimalTransferAmount(Box sourceBox, Box targetBox, Soda.SodaColor color)
    {
        int sourceCount = sourceBox.GetColorCount(color);
        int targetSpace = targetBox.GetAvailableSpaces();
        int targetCurrentCount = targetBox.GetColorCount(color);
        
        // Rule 1: Don't transfer if target is full
        if (targetSpace <= 0) return 0;
        
        // Rule 2: Prefer transfers that complete a box (make it 4 of same color)
        int spaceToComplete = 4 - targetCurrentCount;
        if (spaceToComplete > 0 && spaceToComplete <= sourceCount && spaceToComplete <= targetSpace)
        {
            return spaceToComplete;
        }
        
        // Rule 3: Transfer to balance colors (avoid having too many different colors in target)
        if (targetBox.GetSodaColorCounts().Count >= 3) // Target already has many colors
        {
            // Only transfer if target already has this color
            if (targetCurrentCount > 0)
            {
                return Mathf.Min(sourceCount, targetSpace, 2); // Transfer max 2 to avoid overcrowding
            }
            return 0; // Don't add new colors to crowded boxes
        }
        
        // Rule 4: Default transfer amount
        return Mathf.Min(sourceCount, targetSpace, 1); // Transfer 1 soda by default
    }
    
    private int CalculateTransferPriority(Box sourceBox, Box targetBox, Soda.SodaColor color, int amount)
    {
        int priority = 0;
        
        int targetCurrentCount = targetBox.GetColorCount(color);
        
        // High priority: Completing a box (4 of same color)
        if (targetCurrentCount + amount == 4)
        {
            priority += 1000;
        }
        
        // Medium priority: Consolidating colors (target already has this color)
        if (targetCurrentCount > 0)
        {
            priority += 100;
        }
        
        // Low priority: Moving from crowded to less crowded box
        int sourceDiversity = sourceBox.GetSodaColorCounts().Count;
        int targetDiversity = targetBox.GetSodaColorCounts().Count;
        if (sourceDiversity > targetDiversity)
        {
            priority += 10;
        }
        
        return priority;
    }
    
    private IEnumerator ExecuteTransfer(TransferPlan transfer)
    {
        // Transfer sodas one by one with animation
        for (int i = 0; i < transfer.Amount; i++)
        {
            var soda = transfer.SourceBox.Sodas.LastOrDefault(s => s.sodaColor == transfer.Color);
            if (soda == null) break;
            
            if (transfer.TargetBox.GetAvailableSpaces() <= 0) break;
            
            // Move the soda
            transfer.SourceBox.RemoveSoda(soda);
            transfer.TargetBox.AddSoda(soda);
            soda.transform.parent = null;
            
            // Animate movement
            yield return MoveSodaWithAnimation(soda, transfer.TargetBox);
            
            // Add soda to target after animation
            soda.transform.parent = transfer.TargetBox.transform;
            transfer.TargetBox.UpdateEmptyPositions();
            
            yield return new WaitForSeconds(0.1f); // Small delay between each soda
        }
    }
    
    private IEnumerator MoveSodaWithAnimation(Soda soda, Box targetBox)
    {
        var emptyPositions = targetBox.GetEmptySodaPositions();
        if (emptyPositions.Count == 0) yield break;
        
        Vector3 startPos = soda.transform.position;
        Vector3 endPos = emptyPositions[0].position;
        Vector3 controlPoint = (startPos + endPos) / 2 + Vector3.up * 0.5f;
        
        float duration = 0.3f;
        float elapsedTime = 0;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // Parabolic movement
            soda.transform.position = CalculateParabola(startPos, endPos, controlPoint, t);
            yield return null;
        }
        
        soda.transform.position = endPos;
    }
    
    private Vector3 CalculateParabola(Vector3 start, Vector3 end, Vector3 control, float t)
    {
        return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
    }
    
    private IEnumerator CheckForChainReactions(Box currentBox, List<Box> adjacentBoxes)
    {
        // Check if any new transfers are possible after the previous ones
        var newTransfers = CreateTransferPlan(currentBox, adjacentBoxes);
        
        if (newTransfers.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            yield return ProcessTransfersSequentially(currentBox, adjacentBoxes);
        }
    }
}

[System.Serializable]
public class TransferPlan
{
    public Box SourceBox;
    public Box TargetBox;
    public Soda.SodaColor Color;
    public int Amount;
    public int Priority;
}