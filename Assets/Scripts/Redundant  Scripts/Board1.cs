using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class Board1 : MonoBehaviour
{
    public static Board1 instance;
    [SerializeField]GameObject nodePref;
    [SerializeField]GameObject boxPref;
    Node[,] grid;
    Box[,] allBoxes;
    int height = 5;
    int width = 4;
    private Box currentBox; // Track the last added box
    public bool isBoxFull;
    [Header("Delay For Coroutines")]
    private float transferSodaDelay = 0.35f ;
    float delayInsideTransferSodaMethod = 0.2f;
    float handleTransferCoroutineDelay = 0.5f;
    float firstDelay = 0.15f;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    void Start()
    {
        grid = new Node[width, height];
        allBoxes = new Box[width, height];
        GenerateBoard();
    }
    public void SetCurrentBox(Box box)
    {
        currentBox = box;
    }
    private void GenerateBoard()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {                
                Vector3 pos = new Vector3(i*0.37f,0.185f,j*0.37f);
                GameObject cell = Instantiate(nodePref , pos ,Quaternion.identity);
                cell.transform.parent = transform;
                Node node = cell.GetComponent<Node>();
                node.column = i;
                node.row = j;
                grid[i, j] = node;               
            }
        }        
    }
    public void UpdateBoxPosition( int i, int j)
    {
        if (i >= 0 && i < width && j >= 0 && j < height)
        {          
            if (grid[i, j].isOccupied && allBoxes[i, j] == null)
            {
                // Assign the box to the correct position
                allBoxes[i, j] = currentBox;
                currentBox.column = i;
                currentBox.row = j;
                CheckMatches(i, j, currentBox);

                //CheckAndTransferFromFullBoxes();
            }
        }
    }
    private int GetDirectionPriority(Box box, int column, int row)
    {
        if (box.column == column + 1 && box.row == row) return 1;  // Right
        if (box.column == column && box.row == row + 1) return 2;  // Up
        if (box.column == column - 1 && box.row == row) return 3;  // Left
        return 4;  // Down
    }
    public List<(int, int)> GetAdjacentPositions(int column, int row)
    {
        List<(int, int)> adjacentPositions = new List<(int, int)>();

        // Check for position to the left
        if (column > 0)
        {
            adjacentPositions.Add((column - 1, row));
        }

        // Check for position to the right
        if (column < width - 1)
        {
            adjacentPositions.Add((column + 1, row));
        }

        // Check for position above
        if (row > 0)
        {
            adjacentPositions.Add((column, row - 1));
        }

        // Check for position below
        if (row < height - 1)
        {
            adjacentPositions.Add((column, row + 1));
        }

        return adjacentPositions;
    }
    private List<Box> GetAdjacentFullBoxes(Box currentBox)
    {
        List<Box> adjacentFullBoxes = new List<Box>();
        foreach (var (adjColumn, adjRow) in GetAdjacentPositions(currentBox.column, currentBox.row))
        {
            Box adjacentBox = allBoxes[adjColumn, adjRow];

            if (adjacentBox != null && adjacentBox.GetAvailableSpaces() == 0) 
            {
                // Retrieve color counts to check for single-color boxes
                var colorCounts = adjacentBox.GetSodaColorCounts();

                // Exclude the box if it contains only one distinct color with 4 sodas
                if (colorCounts.Count == 1 && colorCounts.Values.First() == 4)
                {
                    continue;
                }

                adjacentFullBoxes.Add(adjacentBox);
            }
        }
        return adjacentFullBoxes;
    }

    private void CheckMatches(int column, int row, Box currentBox)
    {      
        Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes = new Dictionary<Soda.SodaColor, List<(Box, int)>>();

        var currentBoxColorCounts = currentBox.GetSodaColorCounts();

        // Iterate through adjacent positions and gather potential transfers
        foreach (var (adjColumn, adjRow) in GetAdjacentPositions(column, row))
        {
            Box adjacentBox = allBoxes[adjColumn, adjRow];
            if (adjacentBox != null)
            {
                var adjacentColorCounts = adjacentBox.GetSodaColorCounts();

                // Gather matches for all colors
                foreach (var color in currentBoxColorCounts.Keys)
                {
                    if (adjacentColorCounts.ContainsKey(color))
                    {
                        int matchingCount = adjacentColorCounts[color];
                        if (!colorMatchingBoxes.ContainsKey(color))
                        {
                            colorMatchingBoxes[color] = new List<(Box, int)>();
                        }

                        // Add this adjacent box with the count of matching sodas for the specific color
                        colorMatchingBoxes[color].Add((adjacentBox, matchingCount));
                    }
                }
            }
        }
        // Transfer all sodas collectively based on the sorted priorities
        StartCoroutine(TransferSodasWithDelay(currentBox, colorMatchingBoxes, transferSodaDelay));
    }   

    private IEnumerator TransferSodasWithDelay(Box currentBox, Dictionary<Soda.SodaColor, List<(Box targetBox, int matchCount)>> colorMatchingBoxes, float delay)
    {
        List<Box> adjacentFullBoxes = GetAdjacentFullBoxes(currentBox);
        var colorPriorities = new List<(Soda.SodaColor color, Box targetBox, int matchCount, int directionPriority)>();
        Soda.SodaColor? recentTransferColor = null;
        Box handledFullBox = null;

        foreach (Soda.SodaColor color in colorMatchingBoxes.Keys)
        {
            int currentBoxColorCount = currentBox.GetColorCount(color);

            // If there's a full box match, handle it and set `handledFullBox`
            Box fullBoxMatch = adjacentFullBoxes.FirstOrDefault(fb => fb.GetColorCount(color) > 0);

            if (fullBoxMatch != null && currentBoxColorCount > 0)
            {
                //Debug.Log("Handling full box transfer.");
                int colorCount = GetDistinctColorCountId(currentBox, fullBoxMatch);

                if (colorCount == 2)
                {
                    StartCoroutine(HandleFullBoxTransferWithDelay(fullBoxMatch, currentBox, 0.8f));
                }
                else if (colorCount == 3)
                {

                    if (fullBoxMatch.GetSodaColorCounts().Count == 2 && currentBox.GetSodaColorCounts().Count ==2)
                    {
                        StartCoroutine(HandleFullBox3Colors(fullBoxMatch, currentBox, 0.8f));
                    }
                    else if (fullBoxMatch.GetSodaColorCounts().Count == 3)
                    {
                         if (currentBox.GetSodaColorCounts().Count == 1)
                        {
                            StartCoroutine(HandleOneColorTransfer(fullBoxMatch, currentBox, 0.8f));

                        }
                       else  if (currentBox.GetSodaColorCounts().Count == 2 /*|| currentBox.GetSodaColorCounts().Count == 3*/)
                        {
                            if (currentBox.GetSodasCount() == 2)
                            {
                                StartCoroutine(HandleFullBox3By2(fullBoxMatch, currentBox, 0.8f));

                            }

                            //I've tested this, but still have problem
                            //yield return new WaitForSeconds(0.5f);

                            else if (currentBox.GetSodasCount() == 3)
                            {
                                StartCoroutine(HandleFullBoxof3Colors(fullBoxMatch, currentBox, 0.8f));

                            }

                        }
                    }
                }

                else if (colorCount == 4)
                {
                    //StartCoroutine(HandleFullBox4Colors(fullBoxMatch, currentBox, 0.8f));
                    if (currentBox.GetSodaColorCounts().Count == 3 && fullBoxMatch.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(Handle4ColorTransfer(fullBoxMatch, currentBox, 0.8f));
                    }
                    else if (fullBoxMatch.GetSodaColorCounts().Count == 2 && currentBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleFullBox4Colors(fullBoxMatch, currentBox, 0.8f));

                    }
                    if (currentBox.GetSodaColorCounts().Count == 2 && fullBoxMatch.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleOneColorTransfer(fullBoxMatch, currentBox, 0.8f));

                    }


                }
                else if (colorCount == 5)
                {
                    if (fullBoxMatch.GetSodaColorCounts().Count == 3 && currentBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleOneColorTransfer(fullBoxMatch, currentBox, 0.8f));

                    }

                }


                //yield return StartCoroutine(HandleFullBoxTransferWithDelay(fullBoxMatch, currentBox, handleTransferCoroutineDelay));
                yield return new WaitForSeconds(delayInsideTransferSodaMethod);

                // Mark this box as handled
                handledFullBox = fullBoxMatch;

                // Mark the color to avoid transferring again
                recentTransferColor = color;    
            }

            // Skip adding this color if it was recently transferred
            if (recentTransferColor.HasValue && color == recentTransferColor.Value)
                continue;

            // Sort and prioritize matches, excluding `handledFullBox`
            //* Actually we can prioritize first by color count , then by  direction, for each color

            List<Box> sortedMatches = colorMatchingBoxes[color]
                .Where(boxPair => boxPair.targetBox != handledFullBox)  // Exclude handled full box
                .OrderByDescending(boxPair => boxPair.matchCount)
                .ThenBy(boxPair => GetDirectionPriority(boxPair.targetBox, currentBox.column, currentBox.row))
                .Select(pair => pair.targetBox)
                .ToList();

            if (sortedMatches.Count > 0 && currentBoxColorCount > 0)
            {
                Box topMatch = sortedMatches[0];
                int topMatchCount = colorMatchingBoxes[color][0].matchCount;
                int directionPriority = GetDirectionPriority(topMatch, currentBox.column, currentBox.row);

                colorPriorities.Add((color, topMatch, topMatchCount, directionPriority));
            }
        }

        // Process sorted colors and transfer sodas, excluding the handled full box
        //* now if we have more than 1 color, we prioritize the colors , base  on : 1- count 2- direction
        //* 3- enum color rank
        foreach (var (color, prioritizedBox, _, _) in colorPriorities
            .OrderByDescending(match => match.matchCount)
            .ThenBy(match => match.directionPriority)
            .ThenBy(match => (int)match.color)
            .Where(match => match.targetBox != handledFullBox)) // Exclude handled full box
        {
            int colorCountInCurrent = currentBox.GetColorCount(color);
            if (colorCountInCurrent == 0) continue;

            foreach (Box targetBox in colorMatchingBoxes[color]
                .Where(pair => pair.targetBox != handledFullBox)  // Exclude handled full box
                .OrderByDescending(pair => pair.matchCount)
                .ThenBy(pair => GetDirectionPriority(pair.targetBox, currentBox.column, currentBox.row))
                .Select(pair => pair.targetBox))
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
            // This Coroutine apply a delay for cases we have several transfering for several Boxes
            // *first box transfer happens and then delay and the other

            yield return new WaitForSeconds(delay);
        }
        CheckAndTransferFromFullBoxes();
    }

    public void TransferSodas(Box sourceBox, Box targetBox, int count, Soda.SodaColor color, bool reverseForFullBox = false)
    {
        StartCoroutine(TransferSodasOneByOne(sourceBox, targetBox, count, color,  reverseForFullBox));
    }

    private IEnumerator TransferSodasOneByOne(Box sourceBox, Box targetBox, int count, Soda.SodaColor color, bool reverseForFullBox = false)
    {  
        int transferred = 0;

        // Determine colors in both boxes with available space in targetBox
        List<Soda.SodaColor> colorsInBothBoxes = sourceBox.Sodas
            .Where(soda => targetBox.HasSodaOfColor(soda.sodaColor) && targetBox.GetAvailableSpaces() > 0)
            .Select(soda => soda.sodaColor)
            .Distinct()
            .ToList();
        if (colorsInBothBoxes.Count > 0)
        {
            Soda.SodaColor firstColor = colorsInBothBoxes.First(); // Get the first color from the list
                                                                   // Now you can use firstColor for your logic
        }

        // Choose color to transfer based on existing color or lowest enum
        Soda.SodaColor colorToTransfer = colorsInBothBoxes.Contains(color) ? color : colorsInBothBoxes.FirstOrDefault();

        // Reverse soda list for fullBox-to-currentBox transfers if specified
        //List<Soda> sodasToTransfer = reverseForFullBox ? sourceBox.Sodas.ToList().AsEnumerable().Reverse().ToList() : sourceBox.Sodas.ToList();
        //List<Soda> sodasToTransfer = reverseForFullBox ? sourceBox.SodasReversed : sourceBox.Sodas;

        //List<Soda> sodasToTransfer = reverseForFullBox ? reversedSodas : sourceBox.Sodas;
        // Transfer loop


        if (reverseForFullBox)
        {
            for (int i = 0; i < sourceBox.Sodas.Count && transferred < count; i++)
            {
                Soda soda = sourceBox.Sodas[i];

                if (soda.sodaColor == colorToTransfer && targetBox.GetAvailableSpaces() > 0)
                {
                    sourceBox.Sodas.Remove(soda);
                    soda.transform.parent = null;
                    //** This make the adding process logical and prompt to error or weird actions
                    targetBox.AddSoda(soda);
                    yield return StartCoroutine(MoveSodaToTarget(soda, targetBox));
                    transferred++;
                }
            }
        }
        else
        {
        for (int i = sourceBox.Sodas.Count - 1; i >= 0 && transferred < count; i--)
        {            
            Soda soda = sourceBox.Sodas[i];

            if (soda.sodaColor == colorToTransfer && targetBox.GetAvailableSpaces() > 0)
            {
                sourceBox.Sodas.Remove(soda);
                soda.transform.parent = null;
                targetBox.AddSoda(soda);
                yield return StartCoroutine(MoveSodaToTarget(soda, targetBox));
                transferred++;
            }
        }
        }
      
    }

    private IEnumerator MoveSodaToTarget(Soda soda, Box targetBox)
    {
        Vector3 startPos = soda.transform.position;
        Vector3 endPos = targetBox.GetEmptySodaPositions()[0].position;
        Vector3 controlPoint = (startPos + endPos) / 2 + Vector3.up * 0.5f; // Midpoint control for parabola

        float duration = 0.4f;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Calculate position along parabolic path
            soda.transform.position = CalculateParabola(startPos, endPos, controlPoint, t);
            yield return null;
        }

        // Set final position and add soda to target box
        soda.transform.position = endPos;
        soda.transform.parent = targetBox.transform;
        //targetBox.AddSoda(soda);

    }
    private Vector3 CalculateParabola(Vector3 start, Vector3 end, Vector3 control, float t)
    {
        // Quadratic Bezier formula for parabolic motion
        return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
    }
    private void RemoveEmptyBoxes()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i,j] !=null && (allBoxes[i, j].Sodas.Count == 0 /*|| allBoxes[i, j].Sodas.Count == 4)*/))
                {
                    StartCoroutine(MakeInvisibleAfterDelay(allBoxes[i, j], 0.5f));
                    //Destroy(allBoxes[i, j].gameObject, 1.5f);

                    grid[i, j].isOccupied = false;
                }
                if (allBoxes[i, j] != null  && allBoxes[i,j].BoxFilled())
                {
                    Transform tempPos = allBoxes[i, j].transform;

                    allBoxes[i, j].CloseBox(tempPos);
                    grid[i, j].isOccupied = false;
                }
            }
        }
    }
    private IEnumerator MakeInvisibleAfterDelay(Box box, float delay)
    {

        foreach (Transform child in box.transform)
        {
            MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
            if (childRenderer != null)
            {
                childRenderer.enabled = false;
            }
        }

        yield return new WaitForSeconds(delay);

        //box.gameObject.SetActive(false);
        if (box!=null)
        {

        Destroy(box.gameObject, 1.5f);
        }

    }
    private void Update()
    {
        if (currentBox!=null)
        {
        //Debug.Log("CurrentBox.count is : " + " " + currentBox.GetSodasCount() + " " +

        //             "CurrentBox empty spaces  is : " + " " + currentBox.GetAvailableSpaces());
        }

        RemoveEmptyBoxes();
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i, j]!=null && allBoxes[i,j].GetAvailableSpaces() == 0)
                {
                    //Debug.Log("currentBox Available Space is : " + currentBox.GetAvailableSpaces());
                    //HandleFullBoxTransfers(allBoxes[i, j]);

                }
            }
        }
    }

    #region MOVE FROM FULL TO CURRENT BOXES 

    // ****************** HANDLE MOVEMENT FROM FULL BOX TO CURRENT BOX ********************
    private IEnumerator HandleFullBoxTransferWithDelay(Box fullBox, Box currentBox, float delay , bool canReverse = false)
    {
        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }
        //if (fllBox.)
        //{

        //}

        //yield return new WaitForSeconds(handleTransferCoroutineDelay);

        var fullBoxColors = fullBox.GetSodaColorCounts();
        var currentBoxColors = currentBox.GetSodaColorCounts();

        //if (fullBoxColors.Count == 2 && currentBoxColors.Count > 0)
        //{
        var fullBoxColorList = fullBoxColors.ToList();
        var color1 = fullBoxColorList[0];
        var color2 = fullBoxColorList[1];
        // Ensure color1 has more sodas than color2
        if (color2.Value > color1.Value)
        {
            var temp = color1;
            color1 = color2;
            color2 = temp;
        }


        Soda.SodaColor? targetColor = null;

        // Case 1: If full box color1 > color2, current box has only color2
        if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key))
        {
            targetColor = color2.Key;
        }
        // Case 2: Full box color1 > color2, current box has only color1
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key))
        {
            targetColor = color1.Key;
        }
        // Case 3: Full box color1 > color2, current box has both color1 and color2
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] == currentBoxColors[color2.Key])
        {
            targetColor = color2.Key;
            if (currentBox.HasCapacity() && targetColor.HasValue)
            {
                TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
            }
            yield return new WaitForSeconds(delay);

            // Continue transfer by moving color1 from currentBox back to fullBox
            if (fullBox.HasCapacity())
            {
                TransferSodas(currentBox, fullBox, 1, color1.Key);

            }
            yield break;
        }
        //Case 4: Full box color1 > color2, current box has color1 > color2
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] > currentBoxColors[color2.Key])
        {
            targetColor = color2.Key;
            if (currentBox.HasCapacity() && targetColor.HasValue)
            {
                TransferSodas(fullBox, currentBox, 1, targetColor.Value , canReverse);
            }
            yield return new WaitForSeconds(delay);

            // Transfer color1 from currentBox back to fullBox
            if (fullBox.HasCapacity())
            {
                TransferSodas(currentBox, fullBox, 1, color1.Key);
            }
            yield break;

        }
        // Case 5: Full box color1 > color2, current box has color2 > color1
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors.ContainsKey(color1.Key) && currentBoxColors[color2.Key] > currentBoxColors[color1.Key])
        {
            targetColor = color2.Key;
            if (currentBox.HasCapacity() && targetColor.HasValue)
            {
                TransferSodas(fullBox, currentBox, 1, targetColor.Value , canReverse);
            }
            yield return new WaitForSeconds(delay);


            if (fullBox.HasCapacity())
            {
                // Transfer color1 from currentBox back to fullBox
                TransferSodas(currentBox, fullBox, 1, color1.Key);
            }

            yield break;
        }
        else if (color1.Value == color2.Value)
        {
            // Handling cases where color1 == color2 in fullBox
            // If currentBox has one of the colors, transfer that color
            if (currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key))
            {
                targetColor = color1.Key;
            }
            else if (currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key))
            {
                targetColor = color2.Key;
            }
            if (currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key))
            {
                if (currentBoxColors[color1.Key] > currentBoxColors[color2.Key] ||
                currentBoxColors[color2.Key] == currentBoxColors[color1.Key])
                {
                    targetColor = color2.Key;
                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }
                    yield return new WaitForSeconds(delay);

                    // Ping-pong by transferring color2 from currentBox to fullBox
                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color1.Key);
                    }
                    yield return new WaitForSeconds(delay);

                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }
                    yield return new WaitForSeconds(delay);

                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color1.Key);
                    }


                    yield break;

                }
                else if (currentBoxColors[color2.Key] > currentBoxColors[color1.Key])
                {
                    targetColor = color1.Key;
                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }

                    yield return new WaitForSeconds(delay);

                    // Ping-pong by transferring color2 from currentBox to fullBox
                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color2.Key);
                    }

                    yield return new WaitForSeconds(delay);


                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }
                    yield return new WaitForSeconds(delay);


                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color2.Key);
                    }

                    yield break;

                }

            }

        }

        if (targetColor.HasValue)
        {
            int transferCount = fullBoxColors[targetColor.Value];

            for (int i = 0; i < transferCount; i++)
            {
                if (currentBox.GetSodasCount() >= 4)
                {
                    yield break;
                }

                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    //Debug.Log("Full Box Count is : " + fullBox.GetSodasCount() + " " + "  Target Box Count is :   " + currentBox.GetSodasCount());

                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield break; // Stop if currentBox becomes full
                }

            }
        }

        yield break;
    }  
    private IEnumerator HandleFullBox3Colors(Box fullBox, Box currentBox, float delay, bool canReverse = false)
    {
        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }
        var fullBoxColors = fullBox.GetSodaColorCounts();
        var currentBoxColors = currentBox.GetSodaColorCounts();

        var fullBoxColorList = fullBoxColors.ToList();
        var color1 = fullBoxColorList[0];
        var color2 = fullBoxColorList[1];
        var color3 = currentBoxColors
            .Where(kvp => !fullBoxColors.ContainsKey(kvp.Key))
            .Select(kvp => new KeyValuePair<Soda.SodaColor, int>(kvp.Key, kvp.Value))
            .FirstOrDefault();

        // Ensure color1 has more sodas than color2
        if (color2.Value > color1.Value)
        {
            var temp = color1;
            color1 = color2;
            color2 = temp;
        }

        Soda.SodaColor? targetColor = null;

        // Case 1: If full box color1 > color2, current box has only color2 and color3
        if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key)
            && !currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color3.Key)
            && !fullBoxColors.ContainsKey(color3.Key))
        {
            targetColor = color2.Key;
        }

        // Case 2: Full box color1 > color2, current box has only color1 and color3 
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key)
            && !currentBoxColors.ContainsKey(color2.Key) && currentBoxColors.ContainsKey(color3.Key)
             && !fullBoxColors.ContainsKey(color3.Key))
        {
            targetColor = color1.Key;
        }

        // Case 3: Full box color1 > color2, current box has both color1 and color2 and color3
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key)
           && currentBoxColors.ContainsKey(color2.Key) && currentBoxColors[color1.Key] == currentBoxColors[color2.Key]
           && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
        {
            targetColor = color2.Key;
            if (currentBox.HasCapacity() && targetColor.HasValue)
            {
                TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
            }
            yield return new WaitForSeconds(delay);

            // Continue transfer by moving color1 from currentBox back to fullBox
            if (fullBox.HasCapacity())
            {
                TransferSodas(currentBox, fullBox, 1, color1.Key);

            }
            yield break;
        }

        // Case 4: Full box color1 = color2, current box has  color1 and color2 and color3

        else if (color1.Value == color2.Value)
        {
            // Handling cases where color1 == color2 in fullBox
            // If currentBox has one of the colors, transfer that color
            if (currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key)
                && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
            {
                targetColor = color1.Key;
            }

            else if (currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key)
                && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
            {
                targetColor = color2.Key;
            }

            else if (currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color2.Key)
                && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key))
            {
                if (currentBoxColors[color2.Key] == currentBoxColors[color1.Key])
                {
                    targetColor = color2.Key;
                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }
                    yield return new WaitForSeconds(delay);

                    // Ping-pong by transferring color2 from currentBox to fullBox
                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color1.Key);
                    }
                    yield return new WaitForSeconds(delay);

                    if (currentBox.HasCapacity() && targetColor.HasValue)
                    {
                        TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    }
                    yield return new WaitForSeconds(delay);

                    if (fullBox.HasCapacity())
                    {

                        TransferSodas(currentBox, fullBox, 1, color1.Key);
                    }


                    yield break;

                }

            }

        }

        if (targetColor.HasValue)
        {
            //int transferCount = fullBoxColors[targetColor.Value] - currentBoxColors[color3.Key];
            int transferCount = fullBoxColors[targetColor.Value];
            //Debug.Log("transferCount" + transferCount);

            for (int i = 0; i < transferCount; i++)
            {
                if (currentBox.GetSodasCount() >= 4)
                {
                    yield break;
                }

                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    //Debug.Log("Full Box Count is : " + fullBox.GetSodasCount() + " " + "  Target Box Count is :   " + currentBox.GetSodasCount());

                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield break; // Stop if currentBox becomes full
                }

            }
        }

        yield break;
    }
    private IEnumerator HandleFullBox4Colors(Box fullBox, Box currentBox, float delay, bool canReverse = false)
    {
        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }
        var fullBoxColors = fullBox.GetSodaColorCounts();
        var currentBoxColors = currentBox.GetSodaColorCounts();

        var fullBoxColorList = fullBoxColors.ToList();
        var color1 = fullBoxColorList[0];
        var color2 = fullBoxColorList[1];
        var color3 = currentBoxColors
            .Where(kvp => !fullBoxColors.ContainsKey(kvp.Key))
            .Select(kvp => new KeyValuePair<Soda.SodaColor, int>(kvp.Key, kvp.Value))
            .FirstOrDefault();
        var color4 = currentBoxColors
           .Where(kvp => !fullBoxColors.ContainsKey(kvp.Key))
           .Select(kvp => new KeyValuePair<Soda.SodaColor, int>(kvp.Key, kvp.Value))
           .FirstOrDefault();

        // Ensure color1 has more sodas than color2
        if (color2.Value > color1.Value)
        {
            var temp = color1;
            color1 = color2;
            color2 = temp;
        }

        Soda.SodaColor? targetColor = null;

        // Case 1: If full box color1 > color2, current box has only color2 and color3
        if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color2.Key)
            && !currentBoxColors.ContainsKey(color1.Key) && currentBoxColors.ContainsKey(color3.Key)
            && !fullBoxColors.ContainsKey(color3.Key) && currentBoxColors.ContainsKey(color4.Key)
            && !fullBoxColors.ContainsKey(color4.Key))
        {
            targetColor = color2.Key;
        }

        // Case 2: Full box color1 > color2, current box has only color1 and color3 
        else if (color1.Value > color2.Value && currentBoxColors.ContainsKey(color1.Key)
            && !currentBoxColors.ContainsKey(color2.Key) && currentBoxColors.ContainsKey(color3.Key)
            && !fullBoxColors.ContainsKey(color3.Key) && currentBoxColors.ContainsKey(color4.Key)
            && !fullBoxColors.ContainsKey(color4.Key))
        {
            // *** This case is not logical!! and need to refine***
            targetColor = color1.Key;
        }

       

        // Case 4: Full box color1 = color2

        else if (color1.Value == color2.Value)
        {
            
            if (currentBoxColors.ContainsKey(color1.Key) && !currentBoxColors.ContainsKey(color2.Key)
                && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key)
                && currentBoxColors.ContainsKey(color4.Key)
                && !fullBoxColors.ContainsKey(color4.Key))
            {
                targetColor = color1.Key;
            }

            else if (currentBoxColors.ContainsKey(color2.Key) && !currentBoxColors.ContainsKey(color1.Key)
                && currentBoxColors.ContainsKey(color3.Key) && !fullBoxColors.ContainsKey(color3.Key)
                && currentBoxColors.ContainsKey(color4.Key)
                && !fullBoxColors.ContainsKey(color4.Key))
            {
                targetColor = color2.Key;
            }
           

        }

        if (targetColor.HasValue)
        {
            //int transferCount = fullBoxColors[targetColor.Value] - currentBoxColors[color3.Key];
            int transferCount = fullBoxColors[targetColor.Value];
            Debug.Log("transferCount" + transferCount);

            for (int i = 0; i < transferCount; i++)
            {
                if (currentBox.GetSodasCount() >= 4)
                {
                    yield break;
                }

                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, targetColor.Value, canReverse);
                    //Debug.Log("Full Box Count is : " + fullBox.GetSodasCount() + " " + "  Target Box Count is :   " + currentBox.GetSodasCount());

                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield break; // Stop if currentBox becomes full
                }

            }
        }

        yield break;
    }
    private IEnumerator HandleFullBoxof3Colors(Box fullBox, Box currentBox, float delay, bool canReverse = false)
    {
        Debug.Log("Inside the Main Scenario");
        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }     

            var initialFullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
            var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());

            var currentColorKeys = GetCurrentKeyValue(currentBox).Select(color => color.Key).ToHashSet();

            var fullBoxColorList = initialFullBoxColors
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .ToList();

            var matchingColors = fullBoxColorList
            .Where(color => currentColorKeys.Contains(color.Key))
             .ToList();


            var color1 = fullBoxColorList[0];
            var color2 = fullBoxColorList[1];
            var color3 = fullBoxColorList[2];

        var firstColor = GetCurrentKeyValue(currentBox)[0];
        var secondColor = GetCurrentKeyValue(currentBox)[1];

        if (firstColor.Key == default && firstColor.Value == 0)
        {
            firstColor = color1;
        }

        if (secondColor.Key == default && secondColor.Value == 0)
        {
            secondColor = color2;
        }
  

        var firstColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
            var secondColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == secondColor.Key);

            int currentListCount = GetCurrentKeyValue(currentBox).Count;
            var nonMatchColor = fullBoxColorList.FirstOrDefault(color => !currentColorKeys.Contains(color.Key));
            bool areBothColorsMatching =
        matchingColors.Any(mc => mc.Key == firstColor.Key) &&
        matchingColors.Any(mc => mc.Key == secondColor.Key);



         if (currentBox.GetSodasCount() == 3)
        {
            if (!initialCurrentBoxColors.ContainsKey(nonMatchColor.Key) && areBothColorsMatching)
            {
            if (firstColor.Value > secondColor.Value)
            {
                Debug.Log("Scnario1");
                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, firstColorInFullBox.Key, canReverse);
                }
                yield return new WaitForSeconds(delay);

                if (fullBox.HasCapacity())
                {
                    TransferSodas(currentBox, fullBox, 1, secondColor.Key);

                }

                yield break;
            }
            }
            //Case 1:  full box and current box has : color1 , color 2 , color3

                if (color1.Value > color2.Value && color2.Value == color3.Value && initialCurrentBoxColors.ContainsKey(color1.Key)
                && initialCurrentBoxColors.ContainsKey(color2.Key) && initialCurrentBoxColors.ContainsKey(color3.Key))
            {
                Debug.Log("3*");
                //targetColor = color2.Key;

                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, color2.Key, canReverse);
                }
                yield return new WaitForSeconds(delay);

                // Continue transfer by moving color1 from currentBox back to fullBox
                if (fullBox.HasCapacity())
                {
                    TransferSodas(currentBox, fullBox, 1, color1.Key);
                }
                yield return new WaitForSeconds(delay);

                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, color3.Key);
                }
                yield break;
            }
            //*****************************************************
        }


        //*****************************************************



        //if (!currentBoxColorList.Any(color => color.Key == color1.Key))

        //*****************************************************


    }
    private IEnumerator HandleFullBox3By2(Box fullBox, Box currentBox, float delay, bool canReverse = false)
    {
        Debug.Log("Inside the subset Scenario");

        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }

        var initialFullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
        var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());

        var currentColorKeys = GetCurrentKeyValue(currentBox).Select(color => color.Key).ToHashSet();

        var fullBoxColorList = initialFullBoxColors
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .ToList();

        var matchingColors = fullBoxColorList
        .Where(color => currentColorKeys.Contains(color.Key))
         .ToList();


        var color1 = fullBoxColorList[0];
        var color2 = fullBoxColorList[1];
        var color3 = fullBoxColorList[2];

        var firstColor = GetCurrentKeyValue(currentBox)[0];
        var secondColor = GetCurrentKeyValue(currentBox)[1];

        if (firstColor.Key == default && firstColor.Value == 0)
        {
            firstColor = color1;
        }

        if (secondColor.Key == default && secondColor.Value == 0)
        {
            secondColor = color2;
        }

        var firstColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
        var secondColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == secondColor.Key);

        var nonMatchColor = fullBoxColorList.FirstOrDefault(color => !currentColorKeys.Contains(color.Key));
        bool areBothColorsMatching =
    matchingColors.Any(mc => mc.Key == firstColor.Key) &&
    matchingColors.Any(mc => mc.Key == secondColor.Key);
               
        
            if (!initialCurrentBoxColors.ContainsKey(nonMatchColor.Key) && areBothColorsMatching)
            {
                //StartCoroutine(HandleFullBox(fullBox, currentBox, targetColors, delay));

                if (firstColor.Value == secondColor.Value)
                {
                    if (currentBox.HasCapacity())
                    {
                        TransferSodas(fullBox, currentBox, 1, secondColorInFullBox.Key, canReverse);
                    }
                    yield return new WaitForSeconds(delay);

                    if (fullBox.HasCapacity())
                    {
                        TransferSodas(currentBox, fullBox, 1, firstColor.Key);

                    }

                    yield break;

                }


            }

        
    }
     private IEnumerator Handle4ColorTransfer(Box fullBox, Box currentBox, float delay, bool canReverse = false)
    {
        yield return new WaitForSeconds(firstDelay);

        if (fullBox.GetSodaList().Count > 4 || currentBox.GetSodaList().Count > 4)
        {
            yield break;
        }

        var initialFullBoxColors = new Dictionary<Soda.SodaColor, int>(fullBox.GetSodaColorCounts());
        var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());

        var currentColorKeys = GetCurrentKeyValue(currentBox).Select(color => color.Key).ToHashSet();

        var fullBoxColorList = initialFullBoxColors
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .ToList();
        var FullColorKeys = fullBoxColorList.Select(color => color.Key).ToHashSet();


        var matchingColors = fullBoxColorList
        .Where(color => currentColorKeys.Contains(color.Key))
         .ToList();


        var color1 = fullBoxColorList[0];
        var color2 = fullBoxColorList[1];
        var color3 = fullBoxColorList[2];

        var firstColor = GetCurrentKeyValue(currentBox)[0];
        var secondColor = GetCurrentKeyValue(currentBox)[1];
        var thirdColor = GetCurrentKeyValue(currentBox)[2];

        if (firstColor.Key == default && firstColor.Value == 0)
        {
            firstColor = color1;
        }

        if (secondColor.Key == default && secondColor.Value == 0)
        {
            secondColor = color2;
        }



        var firstColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
        var secondColorInFullBox = fullBoxColorList.FirstOrDefault(c => c.Key == secondColor.Key);

        int currentListCount = GetCurrentKeyValue(currentBox).Count;
        var nonMatchColor = fullBoxColorList.FirstOrDefault(color => !currentColorKeys.Contains(color.Key));
        var nonMatchColorInCurrent = GetCurrentKeyValue(currentBox).FirstOrDefault(color => !FullColorKeys.Contains(color.Key));
        bool areBothColorsMatching =
    matchingColors.Any(mc => mc.Key == firstColor.Key) &&
    matchingColors.Any(mc => mc.Key == secondColor.Key);

        if (!initialCurrentBoxColors.ContainsKey(nonMatchColor.Key) && !initialFullBoxColors.ContainsKey(nonMatchColorInCurrent.Key) && areBothColorsMatching)
        {
            //StartCoroutine(HandleFullBox(fullBox, currentBox, targetColors, delay));
            Debug.Log("4Colors _ 2 unmatched Between full and current Box");
            if (firstColor.Value == secondColor.Value)
            {
                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, secondColorInFullBox.Key, canReverse);
                }
                yield return new WaitForSeconds(delay);

                if (fullBox.HasCapacity())
                {
                    TransferSodas(currentBox, fullBox, 1, firstColor.Key);
                }

                yield break;

            }
        }
    }

    private IEnumerator HandleOneColorTransfer(Box fullBox, Box currentBox, float delay, bool canReverse = false)
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

        var currentBoxColorList = GetCurrentKeyValue(currentBox);

        // Create color variables based on the number of colors in the current box
        KeyValuePair<Soda.SodaColor, int>? firstColor = null;
        KeyValuePair<Soda.SodaColor, int>? secondColor = null;
        KeyValuePair<Soda.SodaColor, int>? thirdColor = null;

        // Assign colors dynamically based on the number of colors in the currentBox
        if (currentBoxColorList.Count >= 1)
        {
            firstColor = currentBoxColorList[0];
        }

        if (currentBoxColorList.Count >= 2)
        {
            secondColor = currentBoxColorList[1];
        }

        if (currentBoxColorList.Count >= 3)
        {
            thirdColor = currentBoxColorList[2];
        }

        // Case 1: Only one matched color in current box
        if (currentBoxColorList.Count == 1)
        {
            HandleSingleColorCase(fullBox, currentBox, firstColor.Value, fullBoxColorList, delay);
        }
        // Case 2: One matched color and one non-matched color in current box
        else if (currentBoxColorList.Count == 2)
        {
            HandleTwoColorCase(fullBox, currentBox, firstColor.Value, secondColor.Value, fullBoxColorList, delay);
        }
        // Case 3: One matched color and two non-matched colors in current box
        else if (currentBoxColorList.Count == 3)
        {
            HandleThreeColorCase(fullBox, currentBox, firstColor.Value, secondColor.Value, thirdColor.Value, fullBoxColorList, delay);
        }
    }
    private void HandleSingleColorCase(Box fullBox, Box currentBox, KeyValuePair<Soda.SodaColor, int> firstColor,
    List<KeyValuePair<Soda.SodaColor, int>> fullBoxColorList, float delaybool, bool canReverse = false)
    {
        var matchingColor = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);

        if (matchingColor.Key != default)
        {
            int emptySlots = 4 - currentBox.GetSodasCount();
            int transferCount = Mathf.Min(matchingColor.Value, emptySlots);

            for (int i = 0; i < transferCount && currentBox.GetSodasCount() < 4; i++)
            {
                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, matchingColor.Key , canReverse );
                }
            }
        }
    }
    private void HandleTwoColorCase(Box fullBox, Box currentBox,
    KeyValuePair<Soda.SodaColor, int> firstColor, KeyValuePair<Soda.SodaColor, int> secondColor,
    List<KeyValuePair<Soda.SodaColor, int>> fullBoxColorList, float delaybool, bool canReverse = false)
    {
        var matchingColor = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
        var nonMatchingColor = secondColor;

        if (matchingColor.Key != default)
        {
            int emptySlots = 4 - currentBox.GetSodasCount();
            int transferCount = Mathf.Min(matchingColor.Value, emptySlots);

            for (int i = 0; i < transferCount && currentBox.GetSodasCount() < 4; i++)
            {
                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, matchingColor.Key, canReverse);
                }
            }
        }

        Debug.Log($"Non-matching color: {nonMatchingColor.Key} in currentBox.");
    }

    private void HandleThreeColorCase(Box fullBox, Box currentBox,
    KeyValuePair<Soda.SodaColor, int> firstColor, KeyValuePair<Soda.SodaColor, int> secondColor, KeyValuePair<Soda.SodaColor, int> thirdColor,
    List<KeyValuePair<Soda.SodaColor, int>> fullBoxColorList, float delay, bool canReverse = false)
    {
        var matchingColor = fullBoxColorList.FirstOrDefault(c => c.Key == firstColor.Key);
        var nonMatchingColors = new List<KeyValuePair<Soda.SodaColor, int>> { secondColor, thirdColor };

        if (matchingColor.Key != default)
        {
            int emptySlots = 4 - currentBox.GetSodasCount();
            int transferCount = Mathf.Min(matchingColor.Value, emptySlots);

            for (int i = 0; i < transferCount && currentBox.GetSodasCount() < 4; i++)
            {
                if (currentBox.HasCapacity())
                {
                    TransferSodas(fullBox, currentBox, 1, matchingColor.Key, canReverse);
                }
            }
        }

        Debug.Log($"Non-matching colors in currentBox: {string.Join(", ", nonMatchingColors.Select(c => c.Key))}");
    }

    // ****************** END OF : HANDLE MOVEMENT FROM FULL BOX TO CURRENT BOX ********************
    #endregion
    private List<KeyValuePair<Soda.SodaColor, int>> GetCurrentKeyValue( Box currentBox)
    {
        var initialCurrentBoxColors = new Dictionary<Soda.SodaColor, int>(currentBox.GetSodaColorCounts());
        var currentBoxColorList = initialCurrentBoxColors
          .OrderByDescending(kvp => kvp.Value)
         .ThenBy(kvp => kvp.Key)
         .ToList();

        return currentBoxColorList;
    }

    private void CheckAndTransferFromFullBoxes()
    {
        foreach (var fullBox in allBoxes)
        {
            if (fullBox == null || fullBox.GetAvailableSpaces() > 0) continue; // فقط جعبه‌های کامل را پردازش کن

            //var colorCounts = fullBox.GetSodaColorCounts();
            //if (colorCounts.Count != 2) continue;
            if (fullBox.HasSingleColorSoda())
            {
                continue;
            }

            foreach (var (adjColumn, adjRow) in GetAdjacentPositions(fullBox.column, fullBox.row))
            {
                Box adjacentBox = allBoxes[adjColumn, adjRow];

                if (adjacentBox == null) continue;

                int adjCapacity = adjacentBox.GetAvailableSpaces();
                if (adjCapacity == 0) continue;

                if (adjacentBox != currentBox)
                {
                    continue;
                }
                int colorCount = GetDistinctColorCountId(adjacentBox, fullBox);

                //StartCoroutine(HandleFullBoxTransferWithDelay(fullBox, adjacentBox, 0.8f));

                if (colorCount == 2)
                {
                    StartCoroutine(HandleFullBoxTransferWithDelay(fullBox, currentBox, 0.8f,true));
                }
                else if (colorCount == 3)
                {

                    if (fullBox.GetSodaColorCounts().Count == 2 && currentBox.GetSodaColorCounts().Count == 2)
                    {
                        StartCoroutine(HandleFullBox3Colors(fullBox, currentBox, 0.8f, true));
                    }
                    else if (fullBox.GetSodaColorCounts().Count == 3)
                    {
                        if (currentBox.GetSodaColorCounts().Count == 1)
                        {
                            StartCoroutine(HandleOneColorTransfer(fullBox, currentBox, 0.8f,true));

                        }
                        else if (currentBox.GetSodaColorCounts().Count == 2 /*|| currentBox.GetSodaColorCounts().Count == 3*/)
                        {
                            if (currentBox.GetSodasCount() == 2)
                            {
                                StartCoroutine(HandleFullBox3By2(fullBox, currentBox, 0.8f , true));

                            }

                            //I've tested this, but still have problem
                            //yield return new WaitForSeconds(0.5f);

                          else  if (currentBox.GetSodasCount() == 3)
                            {
                                StartCoroutine(HandleFullBoxof3Colors(fullBox, currentBox, 0.8f , true));

                            }

                        }
                    }
                }

                else if (colorCount == 4)
                {
                    //StartCoroutine(HandleFullBox4Colors(fullBoxMatch, currentBox, 0.8f));
                    if (currentBox.GetSodaColorCounts().Count == 3 && fullBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(Handle4ColorTransfer(fullBox, currentBox, 0.8f , true));
                    }
                    else if (fullBox.GetSodaColorCounts().Count == 2 && currentBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleFullBox4Colors(fullBox, currentBox, 0.8f , true));

                    }
                    if (currentBox.GetSodaColorCounts().Count == 2 && fullBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleOneColorTransfer(fullBox, currentBox, 0.8f , true));

                    }


                }
                else if (colorCount == 5)
                {
                    if (fullBox.GetSodaColorCounts().Count == 3 && currentBox.GetSodaColorCounts().Count == 3)
                    {
                        StartCoroutine(HandleOneColorTransfer(fullBox, currentBox, 0.8f , true));

                    }

                }



                //var adjColors = adjacentBox.GetSodaColorCounts();

            }
        }
    }
    // Method to return an ID based on the number of distinct colors in currentBox and fullBox
    private int GetDistinctColorCountId(Box currentBox, Box fullBox)
    {
        // Create a HashSet to store distinct colors
        HashSet<Soda.SodaColor> distinctColors = new HashSet<Soda.SodaColor>();

        // Add colors from currentBox to the set
        foreach (var soda in currentBox.Sodas)
        {
            distinctColors.Add(soda.sodaColor);
        }

        // Add colors from fullBox to the set
        foreach (var soda in fullBox.Sodas)
        {
            distinctColors.Add(soda.sodaColor);
        }

        // Return an ID based on the count of distinct colors
        return distinctColors.Count;
    }


}
