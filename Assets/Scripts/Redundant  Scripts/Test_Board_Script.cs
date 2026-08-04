using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test_Board_Script : MonoBehaviour
{

    #region No Use Codes
    //private IEnumerator HandleFullBoxTransferWithDelay2(Box fullBox, Box currentBox, float delay)
    //{

    //    yield return new WaitForSeconds(firstDelay);

    //    if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
    //    {
    //        yield break;
    //    }
    //    var fullBoxColors = fullBox.GetSodaColorCounts();
    //    var currentBoxColors = currentBox.GetSodaColorCounts();
    //    var fullBoxColorList = fullBoxColors.ToList();
    //    var color1 = fullBoxColorList[0];
    //    var color2 = fullBoxColorList[1];
    //    if (color2.Value > color1.Value)
    //    {
    //        var temp = color1;
    //        color1 = color2;
    //        color2 = temp;
    //    }

    //    Soda.SodaColor? targetColor = null;

    //    // Case 1: If full box color1 > color2, current box has only color2
    //    if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key))
    //    {
    //        targetColor = color2.Key;
    //    }
    //    // Case 2: Full box color1 > color2, current box has only color1
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key))
    //    {
    //        targetColor = color1.Key;
    //    }
    //    // Case 3: Full box color1 > color2, current box has both color1 and color2
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] == currentBoxColors[color2.Key])
    //    {
    //        targetColor = color2.Key;
    //        if (currentBox.HasCapacity() && targetColor.HasValue)
    //        {
    //            TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //        }
    //        yield return new WaitForSeconds(delay);

    //        // Continue transfer by moving color1 from currentBox back to fullBox
    //        if (fullBox.HasCapacity())
    //        {
    //            TransferSodas(currentBox, fullBox, 1, color1.Key);

    //        }

    //        yield break;
    //    }
    //    //Case 4: Full box color1 > color2, current box has color1 > color2
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] > currentBoxColors[color2.Key])
    //    {
    //        targetColor = color2.Key;
    //        if (currentBox.HasCapacity() && targetColor.HasValue)
    //        {
    //            TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //        }
    //        yield return new WaitForSeconds(delay);

    //        // Transfer color1 from currentBox back to fullBox
    //        if (fullBox.HasCapacity())
    //        {
    //            TransferSodas(currentBox, fullBox, 1, color1.Key);
    //        }

    //        yield break;

    //    }
    //    // Case 5: Full box color1 > color2, current box has color2 > color1
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors[color2.Key] > currentBoxColors[color1.Key])
    //    {
    //        targetColor = color2.Key;
    //        if (currentBox.HasCapacity() && targetColor.HasValue)
    //        {
    //            TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //        }
    //        yield return new WaitForSeconds(delay);


    //        if (fullBox.HasCapacity())
    //        {
    //            // Transfer color1 from currentBox back to fullBox
    //            TransferSodas(currentBox, fullBox, 1, color1.Key);
    //        }

    //        yield break;
    //    }

    //    else if (color1.Value == color2.Value)
    //    {
    //        // Handling cases where color1 == color2 in fullBox
    //        // If currentBox has one of the colors, transfer that color
    //        if (currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key))
    //        {
    //            targetColor = color1.Key;
    //        }
    //        else if (currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key))
    //        {
    //            targetColor = color2.Key;
    //        }
    //        if (currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key))
    //        {
    //            if (currentBoxColors[color1.Key] > currentBoxColors[color2.Key] ||
    //            currentBoxColors[color2.Key] == currentBoxColors[color1.Key])
    //            {
    //                targetColor = color2.Key;
    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                // Ping-pong by transferring color2 from currentBox to fullBox
    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color1.Key);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color1.Key);
    //                }


    //                yield break;

    //            }
    //            else if (currentBoxColors[color2.Key] > currentBoxColors[color1.Key])
    //            {
    //                targetColor = color1.Key;
    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value);
    //                }

    //                yield return new WaitForSeconds(delay);

    //                // Ping-pong by transferring color2 from currentBox to fullBox
    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color2.Key);
    //                }

    //                yield return new WaitForSeconds(delay);


    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value);
    //                }
    //                yield return new WaitForSeconds(delay);


    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color2.Key);
    //                }

    //                yield break;

    //            }

    //        }

    //    }

    //    if (targetColor.HasValue)
    //    {
    //        int transferCount = fullBoxColors[targetColor.Value];

    //        for (int i = 0; i < transferCount; i++)
    //        {
    //            if (currentBox.GetSodasCount() >= 4)
    //            {
    //                yield break;
    //            }

    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);

    //                yield return new WaitForSeconds(delay);
    //            }
    //            else
    //            {
    //                yield break; // Stop if currentBox becomes full
    //            }

    //        }

    //    }

    //    yield break;
    //}

    // *****************************************************************************************************************

    //private IEnumerator HandleFullBox(Box fullBox, Box currentBox, List<Soda.SodaColor> targetColors, float delay)
    //{
    //    //yield return new WaitForSeconds(firstDelay);

    //    if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
    //    {
    //        yield break;
    //    }



    //    var currentBoxColors = currentBox.GetSodaColorCounts();
    //    var fullBoxColors = fullBox.GetSodaColorCounts();

    //    var matchingColors = targetColors
    //      .Where(color => currentBoxColors.ContainsKey(color))
    //      .OrderByDescending(color => currentBoxColors[color])
    //      .ToList();

    //    Soda.SodaColor firstMatchingColor = default;
    //    Soda.SodaColor secondMatchingColor = default;

    //    if (matchingColors.Count == 2)
    //    {
    //        firstMatchingColor = matchingColors[0];
    //        secondMatchingColor = matchingColors[1];
    //    }

    //    if (currentBoxColors.ContainsKey(firstMatchingColor) && currentBoxColors.ContainsKey(secondMatchingColor))
    //    {
    //        if (currentBoxColors[firstMatchingColor] > currentBoxColors[secondMatchingColor])
    //        {
    //            Debug.Log("Scnario1");
    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, firstMatchingColor);
    //            }
    //            yield return new WaitForSeconds(delay);

    //            if (fullBox.HasCapacity())
    //            {
    //                TransferSodas(currentBox, fullBox, 1, secondMatchingColor);

    //            }

    //            yield break;
    //        }

    //        else if (currentBoxColors[firstMatchingColor] < currentBoxColors[secondMatchingColor] ||
    //                currentBoxColors[firstMatchingColor] == currentBoxColors[secondMatchingColor])
    //        {
    //            //if (currentBoxColors[firstMatchingColor] == currentBoxColors[secondMatchingColor])

    //            Debug.Log("Scnario2");
    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, secondMatchingColor);
    //            }
    //            yield return new WaitForSeconds(delay);

    //            if (fullBox.HasCapacity())
    //            {
    //                TransferSodas(currentBox, fullBox, 1, firstMatchingColor);

    //            }

    //            yield break;

    //        }

    //        //if (  fullBoxColors.ContainsKey(firstMatchingColor) && fullBoxColors.ContainsKey(secondMatchingColor))
    //        //{
    //        //    if (fullBoxColors[firstMatchingColor] > fullBoxColors[secondMatchingColor])
    //        //    {
    //        //        Debug.Log("Scnario3");

    //        //        if (currentBox.HasCapacity())
    //        //        {
    //        //            TransferSodas(fullBox, currentBox, 1, secondMatchingColor);
    //        //        }
    //        //        yield return new WaitForSeconds(delay);

    //        //        if (fullBox.HasCapacity())
    //        //        {
    //        //            TransferSodas(currentBox, fullBox, 1, firstMatchingColor);

    //        //        }

    //        //        yield break;
    //        //    }


    //        //    if (fullBoxColors[firstMatchingColor] == fullBoxColors[secondMatchingColor])


    //        //}
    //    }


    //}

    //***************************************************************************************************************

    //private IEnumerator HandleFullBox3Colors2(Box fullBox, Box currentBox, float delay)
    //{
    //    yield return new WaitForSeconds(firstDelay);

    //    if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
    //    {
    //        yield break;
    //    }
    //    var fullBoxColors = fullBox.GetSodaColorCounts();
    //    var currentBoxColors = currentBox.GetSodaColorCounts();

    //    var fullBoxColorList = fullBoxColors.ToList();
    //    var color1 = fullBoxColorList[0];
    //    var color2 = fullBoxColorList[1];
    //    var color3 = currentBoxColors
    //        .Where(kvp => !fullBoxColors.ContainsKey(kvp.Key))
    //        .Select(kvp => new KeyValuePair<Soda.SodaColor, int>(kvp.Key, kvp.Value))
    //        .FirstOrDefault();

    //    // Ensure color1 has more sodas than color2
    //    if (color2.Value > color1.Value)
    //    {
    //        var temp = color1;
    //        color1 = color2;
    //        color2 = temp;
    //    }

    //    Soda.SodaColor? targetColor = null;

    //    // Case 1: If full box color1 > color2, current box has only color2 and color3
    //    if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key)
    //        && !currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color3.Key)
    //        && !fullBoxColors.ContainsKey(color3.Key))
    //    {
    //        targetColor = color2.Key;
    //    }

    //    // Case 2: Full box color1 > color2, current box has only color1 and color3 
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key)
    //        && !currentBoxColors.ContainsKey(color2.Key) && currentBoxColors.ContainsKey(color3.Key)
    //        && !fullBoxColors.ContainsKey(color3.Key))
    //    {
    //        targetColor = color1.Key;
    //    }

    //    // Case 3: Full box color1 > color2, current box has both color1 and color2 and color3
    //    else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key)
    //        && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] == currentBoxColors[color2.Key]
    //        && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
    //    {
    //        targetColor = color2.Key;
    //        if (currentBox.HasCapacity() && targetColor.HasValue)
    //        {
    //            TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //        }
    //        yield return new WaitForSeconds(delay);

    //        // Continue transfer by moving color1 from currentBox back to fullBox
    //        if (fullBox.HasCapacity())
    //        {
    //            TransferSodas(currentBox, fullBox, 1, color1.Key);

    //        }
    //        yield break;
    //    }

    //    // Case 4: Full box color1 = color2, current box has  color1 and color2 and color3

    //    else if (color1.Value == color2.Value)
    //    {
    //        // Handling cases where color1 == color2 in fullBox
    //        // If currentBox has one of the colors, transfer that color
    //        if (currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key)
    //            && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
    //        {
    //            targetColor = color1.Key;
    //        }

    //        else if (currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key)
    //            && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
    //        {
    //            targetColor = color2.Key;
    //        }

    //        else if (currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key)
    //            && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
    //        {
    //            if (currentBoxColors[color2.Key] == currentBoxColors[color1.Key])
    //            {
    //                targetColor = color2.Key;
    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                // Ping-pong by transferring color2 from currentBox to fullBox
    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color1.Key);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                if (currentBox.HasCapacity() && targetColor.HasValue)
    //                {
    //                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //                }
    //                yield return new WaitForSeconds(delay);

    //                if (fullBox.HasCapacity())
    //                {

    //                    TransferSodas(currentBox, fullBox, 1, color1.Key);
    //                }


    //                yield break;

    //            }

    //        }

    //    }

    //    if (targetColor.HasValue)
    //    {
    //        int transferCount = fullBoxColors[targetColor.Value];

    //        for (int i = 0; i < transferCount; i++)
    //        {
    //            if (currentBox.GetSodasCount() >= 4)
    //            {
    //                yield break;
    //            }

    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, targetColor.Value, true);
    //                //Debug.Log("Full Box Count is : " + fullBox.GetSodasCount() + " " + "  Target Box Count is :   " + currentBox.GetSodasCount());

    //                yield return new WaitForSeconds(delay);
    //            }
    //            else
    //            {
    //                yield break; // Stop if currentBox becomes full
    //            }

    //        }
    //    }

    //    yield break;
    //}
    //****************************************************************************************************************************************
    //private IEnumerator HandleFullBoxof3Colors2(Box fullBox, Box currentBox, float delay)
    //{
    //    // Wait for the initial delay
    //    yield return new WaitForSeconds(delay);

    //    // Create a snapshot of the initial state
    //    var initialFullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
    //    var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());

    //    // Get the sorted lists based on the snapshot
    //    var fullBoxColorList = initialFullBoxColors
    //        .OrderByDescending(kvp => kvp.Value)
    //        .ToList();

    //    var currentBoxColorList = initialCurrentBoxColors
    //        .OrderByDescending(kvp => kvp.Value)
    //        .ToList();

    //    if (fullBoxColorList.Count < 3 || currentBoxColorList.Count < 2)
    //    {
    //        Debug.LogWarning("Not enough colors in fullBox or currentBox for the operation.");
    //        yield break;
    //    }

    //    // Validate that both boxes are in a stable state
    //    if (initialFullBoxColors.Values.Sum() > 4 || initialCurrentBoxColors.Values.Sum() > 4)
    //    {
    //        Debug.LogWarning("Box state is invalid (more than 4 items). Aborting transfer.");
    //        yield break;
    //    }
    //    // Extract the main colors
    //    var color1 = fullBoxColorList[0]; // Most frequent color in fullBox
    //    var color2 = fullBoxColorList[1]; // Second most frequent color
    //    var color3 = fullBoxColorList.Count > 2 ? fullBoxColorList[2] : default; // Safely handle missing third color

    //    var firstColor = currentBoxColorList[0]; // Most frequent color in currentBox
    //    var secondColor = currentBoxColorList.Count > 1 ? currentBoxColorList[1] : default; // Safely handle missing second color

    //    // Map the currentBox colors to their equivalents in fullBox
    //    var firstColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
    //    var secondColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == secondColor.Key);

    //    // Identify the non-matching color in fullBox
    //    var currentColorKeys = currentBoxColorList.Select(color => color.Key).ToHashSet();
    //    var nonMatchColor = fullBoxColorList.FirstOrDefault(color => !currentColorKeys.Contains(color.Key));

    //    Debug.Log($"Snapshot: First color in currentBox: {firstColor.Key}, matches fullBox color: {firstColorInFullBox.Key} with count: {firstColorInFullBox.Value}");
    //    Debug.Log($"Snapshot: Second color in currentBox: {secondColor.Key}, matches fullBox color: {secondColorInFullBox.Key} with count: {secondColorInFullBox.Value}");
    //    Debug.Log($"Snapshot: Non-matching color in fullBox: {nonMatchColor.Key}");

    //    // Begin transfer logic based on initial snapshot
    //    if (!initialCurrentBoxColors.ContainsKey(nonMatchColor.Key))
    //    {
    //        // Scenario 1: First color has a higher count than the second color
    //        if (firstColor.Value > secondColor.Value)
    //        {
    //            Debug.Log("Scenario 1: Transferring based on firstColor dominance.");
    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, firstColorInFullBox.Key);
    //            }

    //            yield return new WaitForSeconds(delay);

    //            if (fullBox.HasCapacity())
    //            {
    //                TransferSodas(currentBox, fullBox, 1, secondColor.Key);
    //            }
    //        }
    //        // Scenario 2: Both colors in the current box have equal counts
    //        else if (firstColor.Value == secondColor.Value)
    //        {
    //            Debug.Log("Scenario 2: Transferring based on equal counts.");
    //            if (currentBox.HasCapacity())
    //            {
    //                TransferSodas(fullBox, currentBox, 1, secondColorInFullBox.Key);
    //            }

    //            yield return new WaitForSeconds(delay);

    //            if (fullBox.HasCapacity())
    //            {
    //                TransferSodas(currentBox, fullBox, 1, firstColor.Key);
    //            }
    //        }
    //    }
    //}



    #endregion

    #region old Codes
    /*
        کد اصلی

        private IEnumerator HandleOneColorTransfer1(Box fullBox, Box currentBox, float delay)
        {
            yield return new WaitForSeconds(firstDelay);

            if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
            {
                yield break;
            }

            var initialFullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
            var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());


            var fullBoxColorList = initialFullBoxColors
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .ToList();     

            var color1 = fullBoxColorList[0];
            var color2 = fullBoxColorList[1];
            var color3 = fullBoxColorList[2];

            var firstColor = GetCurrentKeyValue(currentBox)[0];

            if (firstColor.Key == default && firstColor.Value == 0)
            {
                firstColor = color1;
            }  


            if (initialCurrentBoxColors.Count == 1)
            {
                // Extract the only color in the current box
                var singleCurrentBoxColorPair = initialCurrentBoxColors.First();

                // Ensure the color exists in the full box
                var colorInFullBoxPair = fullBoxColorList.FirstOrDefault(c => c.Key == singleCurrentBoxColorPair.Key);
                if (colorInFullBoxPair.Key == default)
                {
                    Debug.LogError("Color not found in fullBoxColorList.");
                    yield break;
                }

                // Calculate the transfer count
                int emptySlotsInCurrentBox = 4 - singleCurrentBoxColorPair.Value;
                int transferCount = Mathf.Min(colorInFullBoxPair.Value, emptySlotsInCurrentBox);

                // Perform the transfer
                for (int i = 0; i < transferCount && currentBox.GetSodasCount() < 4; i++)
                {
                    if (currentBox.HasCapacity())
                    {
                        TransferSodas(fullBox, currentBox, 1, colorInFullBoxPair.Key);
                        yield return new WaitForSeconds(delay);
                    }
                    else
                    {
                        yield break; // Stop if current box becomes full
                    }
                }
            }


        }


        private bool TryCreateSingleColorBox(Box fullBox, Box adjacentBox, Dictionary<Soda.SodaColor, int> fullBoxColors, Dictionary<Soda.SodaColor, int> adjColors)
        {
            if (adjColors.Count > 1)
            {
                return false;
            }
            foreach (var color in fullBoxColors.Keys)
            {
                if (adjColors.ContainsKey(color))
                {
                    int requiredCount = 4 - adjColors[color];
                    int transferCount = Mathf.Min(requiredCount, fullBoxColors[color], adjacentBox.GetAvailableSpaces());

                    if (transferCount > 0)
                    {
                        TransferSodas(fullBox, adjacentBox, transferCount, color);

                        if (adjacentBox.GetSodaColorCounts().Count == 1 && adjacentBox.GetSodaColorCounts().First().Value == 4)
                        {
                            // اگر جعبه‌ی مجاور تک‌رنگ شد، عملیات کامل شده
                            return true;
                        }
                    }
                }
            }
            return false; // امکان تک‌رنگ کردن جعبه وجود ندارد
        }
        private bool TrySeparateColorsInBoxes(Box fullBox, Box adjacentBox, Dictionary<Soda.SodaColor, int> fullBoxColors, Dictionary<Soda.SodaColor, int> adjColors)
        {
            var color1 = fullBoxColors.ElementAt(0).Key;
            var color2 = fullBoxColors.ElementAt(1).Key;

            if (adjColors.ContainsKey(color1) && adjColors.ContainsKey(color2))
            {
                if (fullBoxColors[color1] >= fullBoxColors[color2])
                {
                    StartCoroutine(PerformTransferWithDelay(fullBox, adjacentBox, color1, color2));
                }
                else
                {
                    StartCoroutine(PerformTransferWithDelay(fullBox, adjacentBox, color2, color1));
                }

                return true; // Separation initiated
            }
            return false; // Separation not possible
        }
        private IEnumerator TransferSodasOneByOne2(Box sourceBox, Box targetBox, int count, Soda.SodaColor color)
        {
            for (int i = 0; i < count; i++)
            {
                if (sourceBox.GetColorCount(color) > 0 && targetBox.GetAvailableSpaces() > 0)
                {
                    Soda soda = sourceBox.Sodas.FirstOrDefault(s => s.sodaColor == color);
                    if (soda != null)
                    {
                        sourceBox.Sodas.Remove(soda);
                        soda.transform.parent = null;
                        yield return StartCoroutine(MoveSodaToTarget(soda, targetBox));
                    }
                }
                yield return new WaitForSeconds(delayInsideTransferSodaMethod);  // Enforce delay between individual transfers
            }
        }

        private IEnumerator PerformTransferWithDelay(Box fullBox, Box adjacentBox, Soda.SodaColor primaryColor, Soda.SodaColor secondaryColor)
        {
            // Step 1: Transfer from fullBox to adjacentBox (primaryColor)
            if (adjacentBox.HasCapacity())
            {
                TransferSodas(fullBox, adjacentBox, 1, primaryColor);
                yield return new WaitForSeconds(0.3f);
            }

            // Step 2: Transfer from adjacentBox to fullBox (secondaryColor)
            if (fullBox.HasCapacity())
            {
                TransferSodas(adjacentBox, fullBox, 1, secondaryColor);
                yield return new WaitForSeconds(0.3f);
            }

            // Step 3: Transfer from fullBox to adjacentBox (primaryColor)
            if (adjacentBox.HasCapacity())
            {
                TransferSodas(fullBox, adjacentBox, 1, primaryColor);
                yield return new WaitForSeconds(0.3f);
            }

            // Step 4: Transfer from adjacentBox to fullBox (secondaryColor)
            if (fullBox.HasCapacity())
            {
                TransferSodas(adjacentBox, fullBox, 1, secondaryColor);
                yield return new WaitForSeconds(0.3f);

            }

            if (fullBox.HasCapacity())
            {
                CheckMatches(fullBox.column, fullBox.row, fullBox);
            }
        }
        private IEnumerator TransferSodasWithDelay1(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay)
        {
            var adjacentFullBoxes = GetAdjacentFullBoxes(currentBox);
            var colorPriorities = new List<(Soda.SodaColor color, Box targetBox, int matchCount, int directionPriority)>();
            Soda.SodaColor? recentTransferColor = null;
            foreach (var color in colorMatchingBoxes.Keys)
            {
                int currentBoxColorCount = currentBox.GetColorCount(color);

                Box fullBoxMatch = adjacentFullBoxes.FirstOrDefault(fb => fb.GetColorCount(color) > 0);
                if (fullBoxMatch != null && currentBoxColorCount > 0)
                {
                    // Perform transfer from fullBox to currentBox with a delay
                    Debug.Log("HANDL FULL BOX ");
                    yield return StartCoroutine(HandleFullBoxTransferWithDelay(fullBoxMatch, currentBox, 0.3f));

                    // Set recentTransferColor to the color transferred from fullBox
                    recentTransferColor = color;
                }

                // Skip adding this color to the transfer list if it was recently transferred from the full box
                if (recentTransferColor.HasValue && color == recentTransferColor.Value)
                    continue;

                // Sort target boxes based on match count, direction priority, and color enum
                var sortedMatches = colorMatchingBoxes[color]
                    .OrderByDescending(boxPair => boxPair.matchCount)
                    .ThenBy(boxPair => GetDirectionPriority(boxPair.targetBox, currentBox.column, currentBox.row))
                    .Select(pair => pair.targetBox)
                    .ToList();

                // Add matches to priority list if there are any matches and the current box has the color
                if (sortedMatches.Count > 0 && currentBoxColorCount > 0)
                {
                    var topMatch = sortedMatches[0];
                    int topMatchCount = colorMatchingBoxes[color][0].matchCount;
                    int directionPriority = GetDirectionPriority(topMatch, currentBox.column, currentBox.row);

                    colorPriorities.Add((color, topMatch, topMatchCount, directionPriority));
                }
            }

            // Sort colors by overall priority and filter out the recentTransferColor
            var sortedColorPriorities = colorPriorities
                .OrderByDescending(match => match.matchCount)
                .ThenBy(match => match.directionPriority)
                .ThenBy(match => (int)match.color)
                .Where(match => match.color != recentTransferColor) // Filter out recent transfer color
                .ToList();

            // Process sorted colors and transfer sodas
            foreach (var (color, prioritizedBox, _, _) in sortedColorPriorities)
            {
                int colorCountInCurrent = currentBox.GetColorCount(color);
                if (colorCountInCurrent == 0) continue;

                foreach (var targetBox in colorMatchingBoxes[color]
                    .OrderByDescending(pair => pair.matchCount)
                    .ThenBy(pair => GetDirectionPriority(pair.targetBox, currentBox.column, currentBox.row))
                    .Select(pair => pair.targetBox))
                {
                    int spaceAvailable = targetBox.GetAvailableSpaces();
                    if (spaceAvailable > 0 && colorCountInCurrent > 0)
                    {
                        int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
                        TransferSodas(currentBox, targetBox, sodasToTransfer, color);

                        // Update count of the color in current box after transfer
                        colorCountInCurrent -= sodasToTransfer;

                        // Stop if no more sodas of this color are left
                        if (colorCountInCurrent == 0) break;
                    }
                }

                // Wait before moving to the next color transfer
                yield return new WaitForSeconds(delay);
            }
        }

        private IEnumerator TransferSodasWithDelay1(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay, Soda.SodaColor? recentTransferColor = null)
        {

            var adjacentFullBoxes = GetAdjacentFullBoxes(currentBox);
            // Step 1: Collect all color matches with prioritization for transfer
            var colorPriorities = new List<(Soda.SodaColor color, Box targetBox, int matchCount, int directionPriority)>();

             recentTransferColor = null; // Keep track of the last color transferred from full box

            foreach (var color in colorMatchingBoxes.Keys)
            {
                int currentBoxColorCount = currentBox.GetColorCount(color);

                Box fullBoxMatch = adjacentFullBoxes.FirstOrDefault(fb => fb.GetColorCount(color) > 0);

                if (fullBoxMatch != null && currentBoxColorCount > 0)
                {
                    //  ** Handle Full Box Transfer **
                   StartCoroutine(HandleFullBoxTransferWithDelay(fullBoxMatch, currentBox, 0.3f)) ;
                    recentTransferColor = color; // Store this color as the recent transfer
                }

                if (recentTransferColor.HasValue && color == recentTransferColor.Value) continue;
                // Sort target boxes for each color based on match count, direction, and color enum
                var sortedMatches = colorMatchingBoxes[color]
                    .OrderByDescending(boxPair => boxPair.matchCount)           // Sort by match count
                    .ThenBy(boxPair => GetDirectionPriority(boxPair.targetBox, currentBox.column, currentBox.row))  // Then by direction priority
                    .Select(pair => pair.targetBox)
                    .ToList();



                // Add each match to our priority list for global color sorting
                if (sortedMatches.Count > 0 && currentBoxColorCount > 0)
                {
                    var topMatch = sortedMatches[0];
                    int topMatchCount = colorMatchingBoxes[color][0].matchCount;
                    int directionPriority = GetDirectionPriority(topMatch, currentBox.column, currentBox.row);

                    colorPriorities.Add((color, topMatch, topMatchCount, directionPriority));
                }
            }

            // Step 2: Sort colors by their overall priority
            var sortedColorPriorities = colorPriorities
                .OrderByDescending(match => match.matchCount)               // Sort by highest match count across all colors
                .ThenBy(match => match.directionPriority)                   // Then by direction priority
                .ThenBy(match => (int)match.color)                          // Final tie-breaker by enum order
                .ToList();

            // Step 3: Process sorted colors and transfer sodas
            foreach (var (color, prioritizedBox, _, _) in sortedColorPriorities)
            {
                int colorCountInCurrent = currentBox.GetColorCount(color);
                if (colorCountInCurrent == 0) continue;

                foreach (var targetBox in colorMatchingBoxes[color]
                    .OrderByDescending(pair => pair.matchCount)
                    .ThenBy(pair => GetDirectionPriority(pair.targetBox, currentBox.column, currentBox.row))
                    .Select(pair => pair.targetBox))
                {
                    int spaceAvailable = targetBox.GetAvailableSpaces();
                    if (spaceAvailable > 0 && colorCountInCurrent > 0)
                    {
                        int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
                        TransferSodas(currentBox, targetBox, sodasToTransfer, color);

                        // Update the count of the color in the current box after transfer
                        colorCountInCurrent -= sodasToTransfer;

                        // Exit if no more sodas of this color are left
                        if (colorCountInCurrent == 0) break;
                    }
                }

                // Wait before moving to the next color transfer
                yield return new WaitForSeconds(delay);
            }
        }


        //** Recursive**

        private bool SeparateOrSingleColor(Box fullBox, Box adjacentBox)
        {
            var fullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
            var adjColors = new Dictionary<Soda.SodaColor, int>(adjacentBox.GetSodaColorCounts());

            bool isOperationComplete = false; // نشانگر برای خروج از حلقه بازگشتی در صورت تکمیل
            PerformRecursiveTransfer(fullBox, adjacentBox, fullBoxColors, adjColors, ref isOperationComplete);

            return isOperationComplete;
        }

        private void PerformRecursiveTransfer(Box fullBox, Box adjacentBox, Dictionary<Soda.SodaColor, int> fullBoxColors, Dictionary<Soda.SodaColor, int> adjColors, ref bool isOperationComplete, int maxRecursionDepth = 4, int currentDepth = 0)
        {
            // Stop recursion if the maximum depth is reached
            if (isOperationComplete || currentDepth >= maxRecursionDepth) return;

            // Sort colors in the full box by count to prioritize transfer
            var sortedFullBoxColors = fullBoxColors.OrderByDescending(c => c.Value).ToList();

            // Check if we can make either box single-colored
            foreach (var colorPair in sortedFullBoxColors)
            {
                var color = colorPair.Key;

                // Attempt to make adjacentBox single-colored
                if (adjacentBox.HasCapacity() && adjColors.ContainsKey(color) && adjColors[color] + fullBoxColors[color] >= 4)
                {
                    int transferCount = Mathf.Min(fullBoxColors[color], adjacentBox.GetAvailableSpaces());
                    for (int i = 0; i < transferCount; i++)
                    {
                        TransferSodas(fullBox, adjacentBox, 1, color);
                        System.Threading.Thread.Sleep(100); // Delay between each transfer
                    }
                    isOperationComplete = true;
                    return;
                }

                // Attempt to make fullBox single-colored
                if (fullBoxColors.Count == 2 && fullBoxColors[color] == 3 && adjacentBox.HasCapacity())
                {
                    var otherColor = fullBoxColors.First(c => c.Key != color).Key;
                    int transferCount = Mathf.Min(fullBoxColors[otherColor], adjacentBox.GetAvailableSpaces());
                    for (int i = 0; i < transferCount; i++)
                    {
                        TransferSodas(fullBox, adjacentBox, 1, otherColor);
                        System.Threading.Thread.Sleep(100); // Delay between each transfer
                    }
                    isOperationComplete = true;
                    return;
                }
            }

            // If single-color transfer was not possible, perform step-by-step transfer
            foreach (var colorPair in sortedFullBoxColors)
            {
                var color = colorPair.Key;
                int count = colorPair.Value;

                // Transfer from fullBox to adjacentBox if possible
                if (adjacentBox.HasCapacity() && count > 1)
                {
                    TransferSodas(fullBox, adjacentBox, 1, color);
                    UpdateColorCounts(fullBoxColors, adjColors, color, -1, 1);
                    System.Threading.Thread.Sleep(100); // Delay for each step
                }
                // Transfer from adjacentBox to fullBox if possible
                else if (fullBox.HasCapacity() && adjColors.ContainsKey(color))
                {
                    TransferSodas(adjacentBox, fullBox, 1, color);
                    UpdateColorCounts(adjColors, fullBoxColors, color, -1, 1);
                    System.Threading.Thread.Sleep(100); // Delay for each step
                }

                // Check if either box is now single-colored
                if (CheckIfSingleColorBox(fullBoxColors) || CheckIfSingleColorBox(adjColors))
                {
                    isOperationComplete = true;
                    return;
                }
            }

            // Recur if operation is still incomplete, increasing current depth
            PerformRecursiveTransfer(fullBox, adjacentBox, fullBoxColors, adjColors, ref isOperationComplete, maxRecursionDepth, currentDepth + 1);
        }

        private void UpdateColorCounts(Dictionary<Soda.SodaColor, int> fromBoxColors, Dictionary<Soda.SodaColor, int> toBoxColors, Soda.SodaColor color, int fromDelta, int toDelta)
        {
            if (fromBoxColors.ContainsKey(color))
            {
                fromBoxColors[color] += fromDelta;
                if (fromBoxColors[color] <= 0)
                    fromBoxColors.Remove(color);
            }

            if (toBoxColors.ContainsKey(color))
                toBoxColors[color] += toDelta;
            else
                toBoxColors[color] = toDelta;
        }

        private bool CheckIfSingleColorBox(Dictionary<Soda.SodaColor, int> colorCounts)
        {
            return colorCounts.Count == 1 && colorCounts.Values.First() == 4;
        }

        //* End Of Recursive**

        private void HandleFullBoxTransfers1(Box fullBox)
        {
            // Identify colors in the full box and sort by enum priority
            var sodaCounts = fullBox.GetSodaColorCounts()
                                    .OrderBy(s => (int)s.Key) // Sort by enum priority
                                    .ToList();

            // Identify primary, secondary, and tertiary colors if they exist
            var primaryColor = sodaCounts[0].Key;
            var secondaryColor = sodaCounts.Count > 1 ? sodaCounts[1].Key : primaryColor;
            var tertiaryColor = sodaCounts.Count > 2 ? sodaCounts[2].Key : primaryColor;

            // Loop over adjacent boxes for transfer attempts
            foreach (var (adjColumn, adjRow) in GetAdjacentPositions(fullBox.column, fullBox.row))
            {
                Box adjacentBox = allBoxes[adjColumn, adjRow];

                if (adjacentBox != null && adjacentBox.HasCapacity())
                {
                    var adjSodaCounts = adjacentBox.GetSodaColorCounts();

                    // Step 1: Transfer secondary color from full box if possible
                    if (adjSodaCounts.ContainsKey(secondaryColor) && adjSodaCounts[secondaryColor] + sodaCounts.First(s => s.Key == secondaryColor).Value <= 4)
                    {
                        int transferCount = Mathf.Min(sodaCounts.First(s => s.Key == secondaryColor).Value, adjacentBox.GetAvailableSpaces());
                        TransferSodas(fullBox, adjacentBox, transferCount, secondaryColor);
                    }

                    // Step 2: Transfer primary color if possible
                    if (adjSodaCounts.ContainsKey(primaryColor) && adjSodaCounts[primaryColor] + sodaCounts.First(s => s.Key == primaryColor).Value <= 4)
                    {
                        int transferCount = Mathf.Min(sodaCounts.First(s => s.Key == primaryColor).Value, adjacentBox.GetAvailableSpaces());
                        TransferSodas(fullBox, adjacentBox, transferCount, primaryColor);
                    }

                    // Step 3: Handle complex cases with multiple colors
                    if (sodaCounts.Count == 3)
                    {
                        var combinedColors = sodaCounts.Concat(adjSodaCounts)
                                                        .GroupBy(x => x.Key)
                                                        .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

                        if (combinedColors.All(c => c.Value <= 4))
                        {
                            foreach (var color in sodaCounts.Select(s => s.Key))
                            {
                                int transferCount = Mathf.Min(sodaCounts.First(s => s.Key == color).Value, adjacentBox.GetAvailableSpaces());
                                TransferSodas(fullBox, adjacentBox, transferCount, color);
                            }
                        }
                    }
                }
            }
        }
        public void HandleFullBoxTransfers2(Box fullBox)
        {
            var fullBoxColors = fullBox.GetSodaColorCounts();
            if (fullBoxColors.Count != 2) return; // Only handle cases with exactly two colors

            var (dominantColor, dominantCount) = fullBoxColors.First();
            var (secondaryColor, secondaryCount) = fullBoxColors.Last();

            foreach (var (adjColumn, adjRow) in GetAdjacentPositions(fullBox.column, fullBox.row))
            {
                Box adjacentBox = allBoxes[adjColumn, adjRow];
                if (adjacentBox == null) continue;

                var adjacentColors = adjacentBox.GetSodaColorCounts();

                // Case 1: One matching color in adjacent box
                if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(dominantColor))
                {
                    int adjColorCount = adjacentColors[dominantColor];
                    int transferCount = Mathf.Min(4 - adjColorCount, dominantCount);
                    TransferSodas(fullBox, adjacentBox, transferCount, dominantColor);
                }
                else if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(secondaryColor))
                {
                    int adjColorCount = adjacentColors[secondaryColor];
                    int transferCount = Mathf.Min(4 - adjColorCount, secondaryCount);
                    TransferSodas(fullBox, adjacentBox, transferCount, secondaryColor);
                }
                // Case 2: Two colors with equal count
                else if (dominantCount == secondaryCount && adjacentColors.ContainsKey(dominantColor) && adjacentColors.ContainsKey(secondaryColor))
                {
                    // Prioritize based on SodaColor enum order
                    if ((int)dominantColor < (int)secondaryColor)
                    {
                        TransferSodas(fullBox, adjacentBox, 1, dominantColor);
                    }
                    else
                    {
                        TransferSodas(fullBox, adjacentBox, 1, secondaryColor);
                    }

                    // Balance the colors by transferring back if needed
                    //AdjustSingleColorBox(fullBox, adjacentBox, dominantColor, secondaryColor);
                }
            }
        }

        private void AdjustSingleColorBox2(Box sourceBox, Box targetBox, Soda.SodaColor color1, Soda.SodaColor color2)
        {
            // Loop to balance colors between the two boxes until each has a single color if possible
            while (sourceBox.HasSodaOfColor(color1) && targetBox.HasSodaOfColor(color2) && sourceBox.GetColorCount(color1) > 1)
            {
                TransferSodas(sourceBox, targetBox, 1, color1);
                TransferSodas(targetBox, sourceBox, 1, color2);
            }
        }

        public void HandleFullBoxTransfers(Box fullBox)
        {
            var fullBoxColors = fullBox.GetSodaColorCounts();

            // Skip boxes that don't meet the specific color-count conditions
            if (fullBoxColors.Count < 2 || fullBoxColors.Count > 3) return;

            // For three-color boxes, ensure one color count is exactly 2
            if (fullBoxColors.Count == 3 && !fullBoxColors.Any(c => c.Value == 2)) return;

            // Extract colors based on counts
            var sortedColors = fullBoxColors.OrderByDescending(c => c.Value).ToList();
            var dominantColor = sortedColors[0].Key;
            var dominantCount = sortedColors[0].Value;
            var secondaryColor = sortedColors.Count > 1 ? sortedColors[1].Key : dominantColor;
            var secondaryCount = sortedColors.Count > 1 ? sortedColors[1].Value : 0;

            foreach (var (adjColumn, adjRow) in GetAdjacentPositions(fullBox.column, fullBox.row))
            {
                Box adjacentBox = allBoxes[adjColumn, adjRow];
                if (adjacentBox == null || !adjacentBox.HasCapacity()) continue;

                var adjacentColors = adjacentBox.GetSodaColorCounts();

                // Case 1: Matching single color in adjacent box
                if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(dominantColor))
                {
                    int adjColorCount = adjacentColors[dominantColor];
                    int transferCount = Mathf.Min(4 - adjColorCount, dominantCount);
                    TransferSodas(fullBox, adjacentBox, transferCount, dominantColor);
                }
                else if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(secondaryColor))
                {
                    int adjColorCount = adjacentColors[secondaryColor];
                    int transferCount = Mathf.Min(4 - adjColorCount, secondaryCount);
                    TransferSodas(fullBox, adjacentBox, transferCount, secondaryColor);
                }
                // Case 2: Handling boxes with both colors
                else if (adjacentColors.ContainsKey(dominantColor) && adjacentColors.ContainsKey(secondaryColor))
                {
                    if (dominantCount > secondaryCount)
                    {
                        TransferSodas(fullBox, adjacentBox, 1, dominantColor);
                    }
                    else
                    {
                        TransferSodas(fullBox, adjacentBox, 1, secondaryColor);
                    }

                    //AdjustSingleColorBox(fullBox, adjacentBox, dominantColor, secondaryColor);
                }
                // Case 3: New colors in adjacent box that partially match
                else if (adjacentColors.Count == 1)
                {
                    var onlyAdjColor = adjacentColors.Keys.First();
                    if (onlyAdjColor == dominantColor || onlyAdjColor == secondaryColor)
                    {
                        int transferCount = Mathf.Min(4 - adjacentColors[onlyAdjColor],
                                                      onlyAdjColor == dominantColor ? dominantCount : secondaryCount);
                        TransferSodas(fullBox, adjacentBox, transferCount, onlyAdjColor);
                    }
                }
            }
        }

        private void HandleTwoColorFullBox(Box fullBox, Box adjacentBox, Soda.SodaColor dominantColor, Soda.SodaColor secondaryColor, int adjCapacity)
        {
            var adjacentColors = adjacentBox.GetSodaColorCounts();

            // If adjacent box has only one color that matches full box
            if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(dominantColor))
            {
                int transferCount = Mathf.Min(adjCapacity, 2);
                TransferSodas(fullBox, adjacentBox, transferCount, dominantColor);
            }
            else if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(secondaryColor))
            {
                int transferCount = Mathf.Min(adjCapacity, 2);
                TransferSodas(fullBox, adjacentBox, transferCount, secondaryColor);
            }
            // If adjacent box has both colors, balance to ensure each color ends up in a separate box
            else if (adjacentColors.Count == 2 && adjacentColors.ContainsKey(dominantColor) && adjacentColors.ContainsKey(secondaryColor))
            {
                TransferSodas(fullBox, adjacentBox, 1, dominantColor);
                AdjustSingleColorBox(fullBox, adjacentBox, dominantColor, secondaryColor);
            }
        }

        private void HandleThreeColorFullBox(Box fullBox, Box adjacentBox, Soda.SodaColor dominantColor, Soda.SodaColor secondaryColor, Soda.SodaColor tertiaryColor, int adjCapacity)
        {
            var adjacentColors = adjacentBox.GetSodaColorCounts();

            // Case where adjacent box has a single color that matches one in the full box
            if (adjacentColors.Count == 1)
            {
                if (adjacentColors.ContainsKey(dominantColor))
                {
                    int transferCount = Mathf.Min(adjCapacity, 2);
                    TransferSodas(fullBox, adjacentBox, transferCount, dominantColor);
                }
                else if (adjacentColors.ContainsKey(secondaryColor))
                {
                    TransferSodas(fullBox, adjacentBox, 1, secondaryColor);
                }
                else if (adjacentColors.ContainsKey(tertiaryColor))
                {
                    TransferSodas(fullBox, adjacentBox, 1, tertiaryColor);
                }
            }
            // Case where adjacent box has a matching secondary and tertiary color; separate into distinct boxes
            else if (adjacentColors.Count == 2 && adjacentColors.ContainsKey(secondaryColor) && adjacentColors.ContainsKey(tertiaryColor))
            {
                TransferSodas(fullBox, adjacentBox, 1, secondaryColor);
                AdjustSingleColorBox(fullBox, adjacentBox, secondaryColor, tertiaryColor);
            }
        }

        private void HandleDominantColorTransfer(Box fullBox, Box adjacentBox, Soda.SodaColor dominantColor, Soda.SodaColor secondaryColor, int adjCapacity)
        {
            var adjacentColors = adjacentBox.GetSodaColorCounts();

            if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(dominantColor))
            {
                int adjColorCount = adjacentColors[dominantColor];
                int transferCount = Mathf.Min(4 - adjColorCount, 3);
                TransferSodas(fullBox, adjacentBox, transferCount, dominantColor);
            }
            else if (adjacentColors.Count == 1 && adjacentColors.ContainsKey(secondaryColor))
            {
                TransferSodas(fullBox, adjacentBox, 1, secondaryColor);
            }
            else if (adjacentColors.Count == 2 && adjacentColors.ContainsKey(dominantColor) && adjacentColors.ContainsKey(secondaryColor))
            {
                TransferSodas(fullBox, adjacentBox, 1, dominantColor);
                AdjustSingleColorBox(fullBox, adjacentBox, dominantColor, secondaryColor);
            }
        }

        private void AdjustSingleColorBox(Box fullBox, Box adjacentBox, Soda.SodaColor color1, Soda.SodaColor color2)
        {
            // Balances the boxes to ensure each color ends up isolated in one of the two boxes
            if (fullBox.GetSodaColorCounts()[color1] > 1 && adjacentBox.GetSodaColorCounts().ContainsKey(color2))
            {
                TransferSodas(adjacentBox, fullBox, 1, color2);
                TransferSodas(fullBox, adjacentBox, 1, color1);
            }
        }



        // ***Cross_transform***
        private IEnumerator TransferSodasWithEnhancedLogic(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay)
        {
            // Sort colors in the current box by their soda counts to determine primary and secondary colors
            var sodaCounts = currentBox.GetSodaColorCounts();
            var sortedColors = sodaCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => (int)pair.Key).ToList();

            // If we have a full box with 3 of one color and 1 of another
            bool isFullBox = currentBox.GetSodasCount() == 4;
            bool hasThreeOneConfig = sortedColors.Count == 2 && sortedColors[0].Value == 3 && sortedColors[1].Value == 1;

            foreach (var color in colorMatchingBoxes.Keys)
            {
                // Get the count of this color in the current box
                int currentBoxColorCount = currentBox.GetColorCount(color);

                // Filter matching boxes based on the new rules
                List<(Box targetBox, int matchCount)> validMatches = colorMatchingBoxes[color]
                    .Where(boxPair =>
                    {
                        var adjacentBoxCounts = boxPair.targetBox.GetSodaColorCounts();

                    // Case 1: Check for a full box with 3 of one color and 1 of another
                    if (hasThreeOneConfig && adjacentBoxCounts.ContainsKey(color))
                        {
                            int primaryColorCount = sortedColors[0].Value;
                            int secondaryColorCount = sortedColors[1].Value;

                        // Adjacent box should only contain primary color or primary with secondary
                        return (adjacentBoxCounts.Count == 1 && adjacentBoxCounts[color] > 0) ||
                                   (adjacentBoxCounts.Count == 2 && adjacentBoxCounts.ContainsKey(color) && adjacentBoxCounts.ContainsKey(sortedColors[1].Key));
                        }

                    // Case 2: Box with 2 colors (2 each) - prioritize by enum number
                    if (sodaCounts.Values.All(count => count == 2) && adjacentBoxCounts.Values.All(count => count == 2))
                        {
                            return true; // Allow sorting based on enum number
                    }

                        return false;
                    })
                    .ToList();

                // Sort the filtered matches based on our rules
                validMatches.Sort((a, b) =>
                {
                    int result = b.matchCount.CompareTo(a.matchCount); // 1. Sort by match count in descending order
                    if (result == 0)
                    {
                        result = GetDirectionPriority(a.targetBox, currentBox.column, currentBox.row)
                            .CompareTo(GetDirectionPriority(b.targetBox, currentBox.column, currentBox.row)); // 2. Sort by direction priority
                    }
                    if (result == 0)
                    {
                        result = ((int)color).CompareTo((int)color); // 3. Sort by enum value as tie-breaker
                    }
                    return result;
                });

                int colorCountInCurrent = currentBoxColorCount;
                if (colorCountInCurrent == 0) continue;

                // Perform the transfers based on sorted results
                foreach (var targetBoxPair in validMatches)
                {
                    Box targetBox = targetBoxPair.targetBox;
                    int spaceAvailable = targetBox.GetAvailableSpaces();
                    if (spaceAvailable > 0 && colorCountInCurrent > 0)
                    {
                        int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
                        TransferSodas(currentBox, targetBox, sodasToTransfer, color);

                        // Update color count in current box after transfer
                        colorCountInCurrent -= sodasToTransfer;

                        // Exit if no more sodas of this color are left
                        if (colorCountInCurrent == 0) break;
                    }
                }

                // Wait before moving to the next color transfer
                yield return new WaitForSeconds(delay);
            }
        }

        private void HandleCrossBoxTransfers1()
        {
            foreach (var box in allBoxes)
            {
                if (box != null && box.GetSodasCount() == 4 && !box.HasSingleColorSoda())
                {
                    var sodaCounts = box.GetSodaColorCounts();

                    Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes = new Dictionary<Soda.SodaColor, List<(Box, int)>>();

                    foreach (var color in sodaCounts.Keys)
                    {
                        foreach (var (adjColumn, adjRow) in GetAdjacentPositions(box.column, box.row))
                        {
                            Box adjacentBox = allBoxes[adjColumn, adjRow];
                            if (adjacentBox != null)
                            {
                                var adjacentCounts = adjacentBox.GetSodaColorCounts();

                                if (adjacentCounts.ContainsKey(color) && (adjacentCounts[color] == 1 || adjacentCounts[color] == 2))
                                {
                                    int matchCount = adjacentCounts[color];
                                    if (!colorMatchingBoxes.ContainsKey(color))
                                    {
                                        colorMatchingBoxes[color] = new List<(Box, int)>();
                                    }
                                    colorMatchingBoxes[color].Add((adjacentBox, matchCount));
                                }
                            }
                        }
                    }

                    if (colorMatchingBoxes.Any())
                    {
                        StartCoroutine(TransferSodasWithEnhancedLogic(box, colorMatchingBoxes, 0.5f));
                    }
                }
            }
        }
        private void HandleCrossBoxTransfers()
        {
            foreach (var box in allBoxes)
            {
                if (box == null) continue;

                var sodaCounts = box.GetSodaColorCounts();

                var primaryColors = sodaCounts.Where(pair => pair.Value >= 2).OrderByDescending(pair => pair.Value).ThenBy(pair => (int)pair.Key).ToList();

                foreach (var primaryColor in primaryColors)
                {
                    Soda.SodaColor color = primaryColor.Key;
                    int colorCount = primaryColor.Value;

                    foreach (var (adjColumn, adjRow) in GetAdjacentPositions(box.column, box.row))
                    {
                        Box adjacentBox = allBoxes[adjColumn, adjRow];
                        if (adjacentBox == null) continue;

                        var adjacentCounts = adjacentBox.GetSodaColorCounts();

                        bool canCompleteSet = adjacentCounts.ContainsKey(color) &&
                                              (adjacentCounts[color] + colorCount == 4) &&
                                              adjacentCounts.Count == 1;

                        if (canCompleteSet)
                        {
                            int transferCount = 4 - adjacentCounts[color];
                            StartCoroutine(TransferSodasOneByOne(box, adjacentBox, transferCount, color));
                            break;
                        }
                    }
                }
            }
        }


        //***Cross_transform_END*** 





    private void CheckMatches2(int column, int row, Box currentBox)
    {
        //List<Box> matchingBoxes = new List<Box>();
        //if (currentBox == null)
        //{
        //    return ;
        //}
        //// Check Right
        //if (column < width - 1)
        //{
        //    Box rightBox = allBoxes[column + 1, row];
        //    if (rightBox != null && rightBox.HasSameColorSoda(currentBox))
        //    {
        //        matchingBoxes.Add(rightBox);
        //    }
        //}
        //// Check Up
        //if (row < height - 1)
        //{
        //    Box upBox = allBoxes[column, row + 1];
        //    if (upBox != null && upBox.HasSameColorSoda(currentBox))
        //    {
        //        matchingBoxes.Add(upBox);
        //    }
        //}

        //// Check Left
        //if (column > 0)
        //{
        //    Box leftBox = allBoxes[column - 1, row];
        //    if (leftBox != null && leftBox.HasSameColorSoda(currentBox))
        //    {
        //        matchingBoxes.Add(leftBox);
        //    }
        //}

        //// Check Down
        //if (row > 0)
        //{
        //    Box downBox = allBoxes[column, row - 1];
        //    if (downBox != null && downBox.HasSameColorSoda(currentBox))
        //    {
        //        matchingBoxes.Add(downBox);
        //    }
        //}
        //if (matchingBoxes.Count == 0)
        //{
        //    return ;
        //}

        //StartCoroutine(TransferSodasWithDelay(currentBox, matchingBoxes, 0.5f));      

    }
    private Box GetBox(int column, int row)
    {
        // Check if the specified position is within the bounds of the board
        if (column >= 0 && column < width && row >= 0 && row < height)
        {
            return allBoxes[column, row]; // Return the box at the specified position
        }
        else
        {
            return null; // Return null if the position is out of bounds
        }
    }

    //private void CheckMatches(int column, int row, Box currentBox)
    //{
    //    List<(Box, int, Soda.SodaColor)> matchingBoxes = new List<(Box, int, Soda.SodaColor)>();
    //    var currentBoxColorCounts = currentBox.GetSodaColorCounts();

    //    // Iterate through adjacent positions
    //    foreach (var (adjColumn, adjRow) in GetAdjacentPositions(column, row))
    //    {
    //        Box adjacentBox = GetBox(adjColumn, adjRow);
    //        if (adjacentBox == null) continue;

    //        var adjacentColorCounts = adjacentBox.GetSodaColorCounts();

    //        // Check each color in the current box for matches in adjacent boxes
    //        foreach (var color in currentBoxColorCounts.Keys)
    //        {
    //            if (adjacentColorCounts.ContainsKey(color))
    //            {
    //                int matchingCount = Mathf.Min(currentBoxColorCounts[color], adjacentColorCounts[color]);
    //                matchingBoxes.Add((adjacentBox, matchingCount, color));
    //            }
    //        }
    //    }

    //    // Sort matching boxes first by the count of matching sodas, then by direction priority
    //    matchingBoxes = matchingBoxes
    //        .OrderByDescending(boxInfo => boxInfo.Item2)  // Highest match count first
    //        //.ThenBy(boxInfo => GetDirectionPriority(boxInfo.Item1, column, row))  // Priority by direction
    //        .ToList();

    //    // Start the transfer process with only matching boxes and colors
    //    StartCoroutine(TransferSodasWithDelay(currentBox, matchingBoxes, 0.5f));
    //}

    //private IEnumerator TransferSodasWithDelay(Box currentBox, List<(Box targetBox, int matchCount, Soda.SodaColor color)> matchingBoxes, float delay)
    //{
    //    foreach (var (targetBox, matchCount, color) in matchingBoxes)
    //    {
    //        int availableSpace = targetBox.GetAvailableSpaces();
    //        int sodasToTransfer = Mathf.Min(availableSpace, currentBox.GetColorCount(color));

    //        if (sodasToTransfer > 0)
    //        {
    //            TransferSodas(currentBox, targetBox, sodasToTransfer, color);

    //            // Exit if there are no more sodas of this color in the current box
    //            if (currentBox.GetColorCount(color) == 0) break;
    //        }

    //        yield return new WaitForSeconds(delay);
    //    }
    //}

    //public void TransferSodas(Box sourceBox, Box targetBox, int count, Soda.SodaColor color)
    //{
    //    StartCoroutine(TransferSodasOneByOne(sourceBox, targetBox, count, color));
    //}

    //private IEnumerator TransferSodasOneByOne(Box sourceBox, Box targetBox, int count, Soda.SodaColor color)
    //{
    //    int transferred = 0;
    //    while (transferred < count && sourceBox.GetColorCount(color) > 0 && targetBox.GetAvailableSpaces() > 0)
    //    {
    //        // Remove the topmost soda of the specified color from sourceBox
    //        Soda sodaToAdd = sourceBox.Sodas.FirstOrDefault(soda => soda.sodaColor == color);
    //        if (sodaToAdd == null) break;

    //        sourceBox.Sodas.Remove(sodaToAdd);

    //        // Move soda to the target box with a parabolic path
    //        yield return StartCoroutine(MoveSodaToTarget(sodaToAdd, targetBox));

    //        transferred++;
    //    }
    //}

    // Helper method to transfer specific colors from one box to another
    //private void CheckMatches(int column, int row, Box currentBox)
    //{
    //    Dictionary<Soda.SodaColor, List<(Box, int)>> colorMatchingBoxes = new Dictionary<Soda.SodaColor, List<(Box, int)>>();
    //    var currentBoxColorCounts = currentBox.GetSodaColorCounts();

    //    foreach (var (adjColumn, adjRow) in GetAdjacentPositions(column, row))
    //    {
    //        Box adjacentBox = allBoxes[adjColumn, adjRow];
    //        if (adjacentBox != null)
    //        {
    //            var adjacentColorCounts = adjacentBox.GetSodaColorCounts();

    //            // Iterate over colors in currentBox to find matches in adjacentBox
    //            foreach (var color in currentBoxColorCounts.Keys)
    //            {
    //                if (adjacentColorCounts.ContainsKey(color))
    //                {
    //                    int matchingCount = adjacentColorCounts[color];
    //                    //int matchingCount = Mathf.Min(currentBoxColorCounts[color], adjacentColorCounts[color]);

    //                    // Initialize list for each color in the dictionary
    //                    if (!colorMatchingBoxes.ContainsKey(color))
    //                    {
    //                        colorMatchingBoxes[color] = new List<(Box, int)>();
    //                    }

    //                    // Add this adjacent box with the count of matching sodas for the specific color
    //                    colorMatchingBoxes[color].Add((adjacentBox, matchingCount));
    //                }
    //            }
    //        }
    //    }

    //    foreach (var color in colorMatchingBoxes.Keys)
    //    {
    //        // Sort each color's list by match count and then direction priority
    //        var sortedMatches = colorMatchingBoxes[color]
    //            .OrderByDescending(boxPair => boxPair.Item2)  // Sort by count of matching sodas for this color
    //            .ThenBy(boxPair => GetDirectionPriority(boxPair.Item1, column, row))  // Sort by direction priority
    //            .Select(pair => pair.Item1)
    //            .ToList();

    //        // Transfer sodas for each color separately
    //        StartCoroutine(TransferSodasWithDelayForColor(currentBox, sortedMatches, color, 0.5f));
    //    }
    //}

    //// Helper method to transfer sodas for a specific color
    //private IEnumerator TransferSodasWithDelayForColor(Box currentBox, List<Box> matchingBoxes, Soda.SodaColor color, float delay)
    //{
    //    foreach (var targetBox in matchingBoxes)
    //    {
    //        int spaceAvailable = targetBox.GetAvailableSpaces();
    //        int colorCountInCurrent = currentBox.GetColorCount(color);

    //        if (spaceAvailable > 0 && colorCountInCurrent > 0)
    //        {
    //            int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
    //            TransferSodas(currentBox, targetBox, sodasToTransfer, color);

    //            // Exit the loop if there are no more sodas of this color to transfer
    //            if (currentBox.GetColorCount(color) == 0) break;
    //        }

    //        yield return new WaitForSeconds(delay);
    //    }
    //}
    private void HandleCrossBoxTransfers1()
    {
        foreach (var box in allBoxes)
        {
            // Check if the box is full and contains 3 sodas of one color and 1 soda of a different color
            if (box != null && box.GetSodasCount() == 4 && !box.HasSingleColorSoda())
            {
                var sodaCounts = box.GetSodaColorCounts();

                foreach (var color in sodaCounts.Keys)
                {
                    if (sodaCounts[color] == 3)  // 3 sodas of the same color, 1 different
                    {
                        // Dictionary to store adjacent boxes that have matching colors
                        Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes = new Dictionary<Soda.SodaColor, List<(Box, int)>>();

                        foreach (var (adjColumn, adjRow) in GetAdjacentPositions(box.column, box.row))
                        {
                            Box adjacentBox = allBoxes[adjColumn, adjRow];
                            if (adjacentBox != null)
                            {
                                var adjacentCounts = adjacentBox.GetSodaColorCounts();

                                // Check if the adjacent box has at least 1 or 2 matching sodas
                                if (adjacentCounts.ContainsKey(color) && (adjacentCounts[color] == 1 || adjacentCounts[color] == 2))
                                {
                                    int matchCount = adjacentCounts[color];
                                    if (!colorMatchingBoxes.ContainsKey(color))
                                    {
                                        colorMatchingBoxes[color] = new List<(Box, int)>();
                                    }
                                    colorMatchingBoxes[color].Add((adjacentBox, matchCount));
                                }
                            }
                        }

                        // If there are no matching adjacent boxes for this color, skip to the next color
                        if (!colorMatchingBoxes.ContainsKey(color)) continue;

                        // Sort the matches for this color by match count, direction priority, and color enum value
                        var sortedMatches = colorMatchingBoxes[color]
                            .OrderByDescending(boxPair => boxPair.matchCount)                  // Sort by match count in adjacent box
                            .ThenBy(boxPair => GetDirectionPriority(boxPair.targetBox, box.column, box.row)) // Sort by direction priority
                            .ThenBy(boxPair => (int)color)                                     // Use color enum value as tie-breaker
                            .Select(pair => pair.targetBox)
                            .ToList();

                        // Determine the transfer count based on adjacent matches: if adjacent has 1 match, transfer 3; if 2, transfer 2
                        int transferCount = sortedMatches.First().GetSodaColorCounts()[color] == 1 ? 3 : 2;

                        // Start the transfer coroutine to move sodas one by one
                        StartCoroutine(TransferSodasOneByOne(box, sortedMatches.First(), transferCount, color));
                        break; // Exit after handling the transfer for this color
                    }
                }
            }
        }
    }

    private IEnumerator TransferSodasOneByOne(Box sourceBox, Box targetBox, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (sourceBox.Sodas.Count > 0 && targetBox.GetAvailableSpaces() > 0)
            {
                Soda sodaToAdd = sourceBox.Sodas[sourceBox.Sodas.Count - 1];
                sourceBox.Sodas.Remove(sodaToAdd);

                // Move soda to the target box
                yield return StartCoroutine(MoveSodaToTarget(sodaToAdd, targetBox));
            }
            else
            {
                // Stop transferring if no more sodas or no more space in target box
                break;
            }
        }
    }
    private IEnumerator TransferSodasWithDelay2(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay)
    {
        foreach (var color in colorMatchingBoxes.Keys)
        {
            // Sort matching boxes for this color by match count, direction priority, and color enum order
            var sortedMatches = colorMatchingBoxes[color]
                .OrderByDescending(boxPair => boxPair.matchCount)  // Sort by match count
                .ThenBy(boxPair => GetDirectionPriority(boxPair.targetBox, currentBox.column, currentBox.row))  // Sort by direction priority
                .Select(pair => pair.targetBox)
                .ToList();

            int colorCountInCurrent = currentBox.GetColorCount(color);
            if (colorCountInCurrent == 0) continue;

            // Transfer sodas to each sorted box in sequence
            foreach (var targetBox in sortedMatches)
            {
                int spaceAvailable = targetBox.GetAvailableSpaces();
                if (spaceAvailable > 0 && colorCountInCurrent > 0)
                {
                    int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
                    TransferSodas(currentBox, targetBox, sodasToTransfer, color);

                    // Update color count in current box after transfer
                    colorCountInCurrent -= sodasToTransfer;

                    // Exit if no more sodas of this color are left
                    if (colorCountInCurrent == 0) break;
                }
            }

            // Wait before moving to the next color transfer
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator TransferSodasWithDelay3(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay)
    {
        foreach (var color in colorMatchingBoxes.Keys)
        {
            int currentBoxColorCount = currentBox.GetColorCount(color);

            // Manually sort the matches by applying each criterion one at a time.
            var sortedMatches = colorMatchingBoxes[color]
                .OrderByDescending(boxPair => boxPair.matchCount)    // 1. Primary sort by match count in the adjacent box
                .ThenBy(boxPair =>
                    boxPair.matchCount == currentBoxColorCount       // 2. Secondary sort by color count in current box only if match counts are equal
                        ? currentBoxColorCount
                        : boxPair.matchCount)
                .ThenBy(boxPair =>
                    boxPair.matchCount == currentBoxColorCount && currentBoxColorCount == boxPair.matchCount
                        ? GetDirectionPriority(boxPair.targetBox, currentBox.column, currentBox.row)
                        : int.MaxValue)  // 3. Only sort by direction priority if previous two criteria are equal
                .ThenBy(boxPair => (int)color)  // 4. Finally, use color's enum order if all other criteria are equal
                .Select(pair => pair.targetBox)
                .ToList();

            Debug.Log($"Color: {color}, Sorted Matches: {string.Join(", ", sortedMatches.Select(b => b.ToString()))}");

            int colorCountInCurrent = currentBoxColorCount;
            if (colorCountInCurrent == 0) continue;

            foreach (var targetBox in sortedMatches)
            {
                int spaceAvailable = targetBox.GetAvailableSpaces();
                if (spaceAvailable > 0 && colorCountInCurrent > 0)
                {
                    int sodasToTransfer = Mathf.Min(spaceAvailable, colorCountInCurrent);
                    TransferSodas(currentBox, targetBox, sodasToTransfer, color);

                    colorCountInCurrent -= sodasToTransfer;
                    if (colorCountInCurrent == 0) break;
                }
            }

            yield return new WaitForSeconds(delay);
        }
    }

    //public IEnumerator TransferSodasWithDelay(Box currentBox, List<Box> matchingBoxes, float delay)
    //{
    //    //var sortedBoxes = matchingBoxes
    //    //    .OrderByDescending(box => box.GetSodasCount())
    //    //    .ToList();

    //    foreach (var targetBox in matchingBoxes)
    //    {
    //        int spaceAvailable = targetBox.GetAvailableSpaces();
    //        if (spaceAvailable > 0)
    //        {
    //            int sodasToTransfer = Mathf.Min(spaceAvailable, currentBox.GetSodasCount());
    //            TransferSodas(currentBox, targetBox, sodasToTransfer);

    //            // Exit the loop if the currentBox's sodas count becomes 0
    //            if (currentBox.GetSodasCount() == 0) break;
    //        }

    //        // Wait for the specified delay before moving to the next box
    //        yield return new WaitForSeconds(delay);
    //    }
    //}
    // In Board class

    */
    #endregion
}
