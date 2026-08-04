using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCylinder : MonoBehaviour
{

    private Transform[] pathWaypoints; // Define the slanted path using waypoints
    public float Maxspeed = 5f; // Speed of the ball movement along the path
    private GameObject removeTrigger;

    private int currentWaypointIndex = 0; // Index of the current waypoint
    private bool isFreezed;
    private float speed;
    private Vector3 offsetDirection; // The direction for parallel offset
    Vector3[] pathWaypointPositions;

    private TrailRenderer trailRenderer; // Reference to the TrailRenderer


    void Start()
    {
        pathWaypoints = new Transform[] { Board.instance.grid[2,2].transform, Board.instance.grid[0,4].transform,
         Board.instance.grid[3,4].transform };

        pathWaypointPositions = new Vector3[pathWaypoints.Length];
        for (int i = 0; i < pathWaypoints.Length; i++)
        {
            // Copy the position and adjust the Y value
            pathWaypointPositions[i] = new Vector3(
                pathWaypoints[i].position.x,
                0.28f, // Set the desired Y position
                pathWaypoints[i].position.z
            );
        }


        if (pathWaypointPositions.Length > 1)
        {
            // Calculate the parallel offset based on the ball's initial position


            // Calculate initial offset direction for the first segment (perpendicular to the path)
            Vector3 pathDirection = (pathWaypointPositions[1] - pathWaypointPositions[0]).normalized;
        }
        //isFreezed = false;
        speed = Maxspeed;
        trailRenderer = gameObject.AddComponent<TrailRenderer>();
        ConfigureTrailRenderer();
    }

    void Update()
    {
        if (pathWaypointPositions.Length == 0) return;

        // Move the ball towards the next waypoint, keeping parallel
        MoveBallAlongPath();


        // Check if the ball has reached the current waypoint
        if (Vector3.Distance(transform.position, pathWaypointPositions[currentWaypointIndex] )< 0.1f)
        {
            // Move to the next waypoint if reached
            //currentWaypointIndex = (currentWaypointIndex + 1) % pathWaypoints.Length;
            if (currentWaypointIndex <= pathWaypointPositions.Length - 2)
            {
                currentWaypointIndex++;
                PhysicsMaterial ballMaterial = new PhysicsMaterial();
                ballMaterial.bounciness = 0f;  // No bounce
                ballMaterial.dynamicFriction = 1f;  // High friction to reduce sliding

            }
            else
            {
                //rigidbody.isKinematic = false;
                //rigidbody.useGravity = true;
                Destroy(gameObject);

            }
            if (currentWaypointIndex == 3)
            {
                //rigidbody.mass = 1;
                //rigidbody.drag = 0;

            }
        }

    }
    private void ConfigureTrailRenderer()
    {
        // Set the TrailRenderer properties
        trailRenderer.time = 2f; // Trail duration in seconds
        trailRenderer.startWidth = 0.1f;
        trailRenderer.endWidth = 0.05f;

        // Set trail colors
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.cyan, 0f),
                new GradientColorKey(Color.blue, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trailRenderer.colorGradient = gradient;

        // Set trail material (optional, ensure you have a material in your assets)
        trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }
    void MoveBallAlongPath()
    {
        // Calculate step size based on speed and delta time
        float step = speed * Time.deltaTime;

        // Get the target parallel position
        Vector3 targetPosition = GetParallelPosition(pathWaypointPositions[currentWaypointIndex]);

        // Move the ball towards the target parallel position
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * step;

        transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0,-45,0)*direction);

        //transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);
        //        RotateTowardsTarget(targetPosition);

        // Rotate the ball to face the next waypoint
        //Vector3 direction = (targetPosition - transform.position).normalized;
        //offsetDirection = Vector3.Cross(direction, Vector3.up).normalized;

        //transform.rotation = Quaternion.LookRotation(offsetDirection);


    }
    void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;


        if (direction != Vector3.zero) 
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Vector3 FinalDirection = Vector3.Cross(direction, Vector3.up).normalized;

            // Set the cylinder's forward direction
            transform.rotation = Quaternion.LookRotation(FinalDirection);
            //transform.rotation = targetRotation;

            //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }
    Vector3 GetParallelPosition(Vector3 targetWaypoint)
    {
        Vector3 pathDirection;

        // Handle edge case when at the last waypoint
        if (currentWaypointIndex == pathWaypointPositions.Length - 1)
        {
            // Calculate direction based on the previous waypoint (moving backward)
            pathDirection = (targetWaypoint - pathWaypointPositions[currentWaypointIndex - 1]).normalized;
        }
        else
        {
            // Normal case: Calculate direction from current waypoint to the next one
            pathDirection = (pathWaypointPositions[currentWaypointIndex + 1] - targetWaypoint).normalized;
        }

        // Calculate the perpendicular direction to the path (cross product with the Y axis)
        offsetDirection = Vector3.Cross(pathDirection, Vector3.up).normalized;

        // Return the parallel position (waypoint position + offset)
        return targetWaypoint/*+ offsetDirection * 1*/;
    }


    // Method to calculate parallel offset based on ball's start position relative to middle point


    private void OnTriggerEnter(Collider other)
    {

        if (other.transform.CompareTag("Carrier"))
        {

            Destroy(other.gameObject);
        }

    }

}
