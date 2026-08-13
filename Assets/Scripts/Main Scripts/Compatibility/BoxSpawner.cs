using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxSpawner : MonoBehaviour
{
    public static BoxSpawner instance;
    [SerializeField] List<Transform> spawnPositions;  // Array of positions where boxes should spawn (middle, left, right)
    //[Header("Circular Movement ")]
    [SerializeField] private Transform[] startPos;
    [SerializeField] private Transform[] endPos;
    [SerializeField] private Transform center;
    [SerializeField] float radius = 1f;
    public float speed = 100f;

    public GameObject boxPrefab;
    public GameObject sodaPrefab;
    //[SerializeField] Transform[] spawnPositions;  // Array of positions where boxes should spawn (middle, left, right)

    public float spawnDelay = 0.3f;
    public float respawnDelay = 2f;

    private bool[] boxAtEndPosition;  // Array to track if each box has reached its endpoint

    //private Transform[] endPos = new Transform[3];  
    private float[] startAngles;
    private float[] endAngles;
    private float[] currentAngles;

    private List<GameObject> spawnedBoxes = new List<GameObject>();
    private int maxBoxCount = 3;
    public bool stopSpawn;
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        boxAtEndPosition = new bool[maxBoxCount];
        startAngles = new float[3];
        endAngles = new float[3];
        currentAngles = new float[3];

        StartMovement();
        StartCoroutine(SpawnBoxesLoop());
    }
    private void StartMovement()
    {
        int count = maxBoxCount;

        for (int i = 0; i < 3; i++)
        {
            // Calculate start and end angles relative to the center
            startAngles[i] = Mathf.Atan2(startPos[i].position.z - center.position.z,
                startPos[i].position.x - center.position.x) * Mathf.Rad2Deg;

            endAngles[i] = Mathf.Atan2(endPos[i].position.z - center.position.z,
               endPos[i].position.x - center.position.x) * Mathf.Rad2Deg;

            // Set the current angle to the start angle
            currentAngles[i] = startAngles[i];
        }
    }
    private IEnumerator MoveBoxesToEndPosition()
    {
        bool allBoxesAtEnd = false;

        while (!allBoxesAtEnd)
        {
            allBoxesAtEnd = true; // Assume all boxes are at their end position until checked

            for (int i = 0; i < maxBoxCount; i++)
            {
                // Skip the box if it has already reached the endpoint
                if (boxAtEndPosition[i]) continue;

                // Calculate angle difference and move the box
                float angleDifference = Mathf.DeltaAngle(currentAngles[i], endAngles[i]);
                float angleStep = Mathf.Sign(angleDifference) * speed * Time.deltaTime;

                // Check if box reached the endpoint
                if (Mathf.Abs(angleStep) >= Mathf.Abs(angleDifference))
                {
                    angleStep = angleDifference;
                    boxAtEndPosition[i] = true; // Mark box as reached endpoint
                }
                else
                {
                    allBoxesAtEnd = false; // Not all boxes are at end yet
                }

                currentAngles[i] += angleStep;

                // Update box position on the circular path
                float x = center.position.x + Mathf.Cos(currentAngles[i] * Mathf.Deg2Rad) * radius;
                float z = center.position.z + Mathf.Sin(currentAngles[i] * Mathf.Deg2Rad) * radius;
                spawnedBoxes[i].transform.position = new Vector3(x, spawnedBoxes[i].transform.position.y, z);
            }

            yield return null; // Wait until the next frame to update again
        }
    }

    private IEnumerator SpawnBoxesLoop()
    {
        if (GameManager.instance.gameEnded|| stopSpawn)
        {
            yield break;
        }
        yield return new WaitForSeconds(1f);
      
        while (true)
        {
            yield return SpawnBoxesWithDelay();
            yield return StartCoroutine(MoveBoxesToEndPosition());
            yield return new WaitUntil(() => AllBoxesDragged());
            yield return new WaitForSeconds(respawnDelay);
        }
    }
    private IEnumerator SpawnBoxesWithDelay()
    {
        if (stopSpawn == true || GameManager.instance.gameEnded)
        {
            yield break;
        }
        if (spawnedBoxes.Count > 0)
        {
            spawnedBoxes.Clear();
        }
        StartMovement();
        for (int i = 0; i < 3; i++)
        {
            GameObject box = Instantiate(boxPrefab, startPos[i].position + new Vector3(0, 0.2965f,0), Quaternion.Euler(-90, 0, 0));
            spawnedBoxes.Add(box);
            //SpawnSodasInBox(box);
       
            SpawnSoda(box);
            yield return new WaitForSeconds(spawnDelay);
            boxAtEndPosition[i] = false;
        }
        //yield return new WaitForSeconds(3f);

        //StartCoroutine(MoveBoxesToEndPosition());
        //StartCoroutine(CheckBooleran());
    }
    IEnumerator CheckBooleran()
    {
        yield return new WaitForSeconds(3f);
        for (int i = 0; i < 3; i++)
        {
            boxAtEndPosition[i] = false;
        }
    }
    private void SpawnSoda(GameObject box)
    {
        int sodaCount = Random.Range(1, 4);
        Transform[] sodaPositions = GetSodaPositions(box);

        List<Transform> availablePositions = new List<Transform>(sodaPositions);
        for (int j = 0; j < sodaCount; j++)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            GameObject soda = Instantiate(sodaPrefab, availablePositions[randomIndex].position, Quaternion.Euler(-90, 0, 0), box.transform);

            Vector3 tempPos = soda.transform.position;
            tempPos.y = 0.3f;
            soda.transform.position = tempPos;

            Soda sodaScript = soda.GetComponent<Soda>();
            if (sodaScript != null)
            {
                Soda.SodaColor randomColor = GetRandomSodaColor();

                sodaScript.SetColor(randomColor);
            }
            availablePositions.RemoveAt(randomIndex);
        }

    }
    private void SpawnSodasInBox(GameObject box)
    {
        int sodaCount = Random.Range(1, 4);
        Soda.SodaColor randomColor = GetRandomSodaColor();
        //Soda.SodaColor randomColor = Soda.SodaColor.Pink;
        Transform[] sodaPositions = GetSodaPositions(box);

        List<Transform> availablePositions = new List<Transform>(sodaPositions);

        for (int j = 0; j < sodaCount; j++)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            GameObject soda = Instantiate(sodaPrefab, availablePositions[randomIndex].position, Quaternion.Euler(-90, 0, 0), box.transform);

            Vector3 tempPos = soda.transform.position;
            tempPos.y = 0.3f;
            soda.transform.position = tempPos;

            Soda sodaScript = soda.GetComponent<Soda>();
            if (sodaScript != null)
            {
                sodaScript.SetColor(randomColor);
            }
            availablePositions.RemoveAt(randomIndex);
        }
    }

    private bool AllBoxesDragged()
    {
        if (spawnedBoxes.Count == 0)
        {
            return false;
        }
        foreach (GameObject box in spawnedBoxes)
        {
            if (box != null && !box.GetComponent<Box>().IsDragged) // Assuming each box has a script with an 'IsDragged' property
            {
                return false;
            }
        }

        return true;
    }

    private Transform[] GetSodaPositions(GameObject box)
    {
        List<Transform> sodaPositions = new List<Transform>();

        for (int i = 0; i < 4; i++)
        {
            Transform pos = box.transform.Find($"SodaPosition{i}");
            if (pos != null)
            {
                sodaPositions.Add(pos);
            }
        }
        return sodaPositions.ToArray();
    }

    private Soda.SodaColor GetRandomSodaColor()
    {
        Soda.SodaColor[] colors = (Soda.SodaColor[])System.Enum.GetValues(typeof(Soda.SodaColor));
        return colors[Random.Range(0, colors.Length)];
    }

    private void Update()
    {
        if (spawnedBoxes.Count == 3)
        {
            //BoxesCircularMovement(spawnedBoxes);

        }
    }
}


#region version 2

/*
    public class BoxSpawner : MonoBehaviour
{
    public GameObject boxPrefab;
    public GameObject sodaPrefab;
    //[SerializeField] Transform[] spawnPositions;  // Array of positions where boxes should spawn (middle, left, right)
    [SerializeField] List<Transform> spawnPositions;  // Array of positions where boxes should spawn (middle, left, right)
    [SerializeField] List<Transform> YellowPos;  // Array of positions where boxes should spawn (middle, left, right)
    public float spawnDelay = 0.4f;
    public float respawnDelay = 2f;
    private List<GameObject> spawnedBoxes = new List<GameObject>();
    private int maxBoxCount = 3;

    private void Start()
    {
        //if (spawnPositions == null || spawnPositions.Count == 0)
        //{
        //    Debug.LogError("Spawn positions not assigned! Please assign spawn positions in the inspector.");
        //    return;
        //}
        Debug.Log("Number of spawn positions: " + spawnPositions.Count);

        StartCoroutine(SpawnBoxesLoop());
        //GnerateYellowSodas();
    }

    private IEnumerator SpawnBoxesLoop()
    {
        while (true) // Keep spawning indefinitely
        {
            yield return SpawnBoxesWithDelay();
            yield return new WaitUntil(() => AllBoxesDragged());
            yield return new WaitForSeconds(respawnDelay);
        }
    }

    private IEnumerator SpawnBoxesWithDelay()
    {
        if (spawnedBoxes.Count>0)
        {
        spawnedBoxes.Clear();

        }

        for (int i = 0; i < maxBoxCount; i++)
        {
            if (i >= spawnPositions.Count)
            {
                Debug.LogWarning("Not enough spawn positions defined for maxBoxCount. Adjusting to available positions.");
                break;
            }
            GameObject box = Instantiate(boxPrefab, spawnPositions[i].position, Quaternion.identity);
            spawnedBoxes.Add(box);
            SpawnSodasInBox(box);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnSodasInBox(GameObject box)
    {
        int sodaCount = Random.Range(1, 4);
        Soda.SodaColor randomColor = GetRandomSodaColor();
        //Soda.SodaColor randomColor = Soda.SodaColor.Pink;
        Transform[] sodaPositions = GetSodaPositions(box);

        List<Transform> availablePositions = new List<Transform>(sodaPositions);

        for (int j = 0; j < sodaCount; j++)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            GameObject soda = Instantiate(sodaPrefab, availablePositions[randomIndex].position, Quaternion.Euler(-90, 0, 0), box.transform);

            Vector3 tempPos = soda.transform.position;
            tempPos.y = 0.3f;
            soda.transform.position = tempPos;

            Soda sodaScript = soda.GetComponent<Soda>();
            if (sodaScript != null)
            {
                sodaScript.SetColor(randomColor);
            }
            availablePositions.RemoveAt(randomIndex);
        }
    }

    private bool AllBoxesDragged()
    {
        if (spawnedBoxes.Count ==0)
        {
            return false;
        }
        foreach (GameObject box in spawnedBoxes)
        {
            if (box != null && !box.GetComponent<Box>().IsDragged) // Assuming each box has a script with an 'IsDragged' property
            {
                return false;
            }
        }
        return true;
    }

    private Transform[] GetSodaPositions(GameObject box)
    {
        List<Transform> sodaPositions = new List<Transform>();

        for (int i = 0; i < 4; i++)
        {
            Transform pos = box.transform.Find($"SodaPosition{i}");
            if (pos != null)
            {
                sodaPositions.Add(pos);
            }
        }
        return sodaPositions.ToArray();
    }

    private Soda.SodaColor GetRandomSodaColor()
    {
        Soda.SodaColor[] colors = (Soda.SodaColor[])System.Enum.GetValues(typeof(Soda.SodaColor));
        return colors[Random.Range(0, colors.Length)];
    }

    private void GnerateYellowSodas()
    {
        for (int i = 0; i < 4; i++)
        {
        GameObject box = Instantiate(boxPrefab, YellowPos[i].position, Quaternion.identity);
            int sodaCount = Random.Range(1, 4);

            for (int j = 0; j < sodaCount; j++)
            {

                Transform[] sodaPositions = GetSodaPositions(box);
                GameObject soda = Instantiate(sodaPrefab, sodaPositions[j].position, Quaternion.Euler(-90, 0, 0), box.transform);

                Vector3 tempPos = soda.transform.position;
                tempPos.y = 0.3f;
                soda.transform.position = tempPos;

                Soda sodaScript = soda.GetComponent<Soda>();
                if (sodaScript != null)
                {
                    sodaScript.SetColor(Soda.SodaColor.Pink);
                }
            }

        }

    }
}
*/

#endregion

#region old 

//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;

//public class BoxSpawner : MonoBehaviour
//{
//    public GameObject boxPrefab;
//    public GameObject sodaPrefab;
//    public Transform[] spawnPositions;        // Array of positions where boxes should spawn (middle, left, right)
//    private List<GameObject> boxs;

//    public float spawnDelay = 1f;
//    bool canSpawn;
//    List<Transform> emptyBOXPositions;
//    List<Transform> sodaPositions;
//    int maxBoxCount = 3;
//    private void Start()
//    {
//        boxs = new List<GameObject>();
//        emptyBOXPositions = new List<Transform>();

//        canSpawn = true;
//        StartCoroutine(SpawnBoxesLoop());

//    }

//    IEnumerator SetDelay()
//    {
//        yield return new WaitForSeconds(2f);

//        StartCoroutine(SpawnBoxesLoop());

//    }
//    private IEnumerator SpawnBoxesLoop()
//    {

//        while (canSpawn )
//        {

//            for (int i = 0; i < spawnPositions.Length; i++)
//            {
//                GameObject box = Instantiate(boxPrefab, spawnPositions[i].position, Quaternion.identity);
//                boxs.Add(box);
//                int sodaCount = Random.Range(1, 4);

//                Soda.SodaColor randomColor = GetRandomSodaColor();

//                Transform[] sodaPositions = GetSodaPositions(box);
//                List<Transform> availablePositions = new List<Transform>(sodaPositions); // Convert to list for removal

//                for (int j = 0; j < sodaCount; j++)
//                {
//                    int randomIndex = Random.Range(0, availablePositions.Count);
//                    GameObject soda = Instantiate(sodaPrefab, availablePositions[randomIndex].position, Quaternion.Euler(-90, 0, 0), box.transform);

//                    Vector3 tempPos = soda.transform.position;
//                    tempPos.y = 0.3f;
//                    soda.transform.position = tempPos;
//                    // Set the color of the soda using the Soda script
//                    Soda sodaScript = soda.GetComponent<Soda>();
//                    if (sodaScript != null)
//                    {
//                        sodaScript.SetColor(randomColor);
//                    }
//                    availablePositions.RemoveAt(randomIndex);

//                }
//                yield return new WaitForSeconds(0.4f);

//            }
//            canSpawn = false;
//            // Wait for the delay before spawning the next set of boxes
//            //GetEmptyBoxPositions();
//            yield return new WaitForSeconds(spawnDelay);

//        }
//    }

//    private Transform[] GetSodaPositions(GameObject box)
//    {
//        sodaPositions = new List<Transform>();

//        // Find and add each of the child positions named "SodaPosition0", "SodaPosition1", etc.
//        for (int i = 0; i < 4; i++)
//        {
//            Transform pos = box.transform.Find($"SodaPosition{i}");
//            if (pos != null)
//            {
//                sodaPositions.Add(pos); // Add position if it exists
//            }
//        }

//        // Convert the list to an array and return it
//        return sodaPositions.ToArray();
//    }

//    // Method to get a random color from the SodaColor enum
//    private Soda.SodaColor GetRandomSodaColor()
//    {
//        // Get all enum values and return a random one
//        Soda.SodaColor[] colors = (Soda.SodaColor[])System.Enum.GetValues(typeof(Soda.SodaColor));
//        return colors[Random.Range(0, colors.Length)];
//    }

//    public List<Transform> GetEmptyBoxPositions()
//    {
//        Transform[] allPositions = spawnPositions;

//        float tolerance = 0.1f; // Tolerance value for position matching

//        foreach (Transform pos in allPositions)
//        {
//            bool isOccupied = false;

//            // Check if any soda's position is close to this position within tolerance
//            foreach (GameObject box in boxs)
//            {
//                if (Vector3.Distance(box.transform.position, pos.position) < tolerance)
//                {
//                    isOccupied = true;
//                    break;
//                }
//            }

//            if (!isOccupied)
//            {
//                emptyBOXPositions.Add(pos);
//            }
//        }

//        return emptyBOXPositions;
//    }
//    private void Update()
//    {
//        GetEmptyBoxPositions();

//        if (emptyBOXPositions.Count >= maxBoxCount)
//        {
//            emptyBOXPositions.Clear();
//            canSpawn = true;
//        }

//        if (canSpawn)
//        {
//            StartCoroutine(SetDelay());
//            //canSpawn = false;  // Prevents multiple calls


//        }
//    }
//}

#endregion








