using UnityEngine;
using UnityEngine.AI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class DogNavigator : MonoBehaviour
{
    private Camera cam;
    private ROSConnection ros;
    private NavMeshAgent agent;

    [Header("Dog")]
    public Transform dogTransform;

    [Header("Movement")]
    public float moveSpeed = 0.5f;
    public float stoppingDistance = 0.5f;

    [Header("Model Orientation")]
    public float fixedXRotation = -90f;
    public float fixedZRotation = 0f;

    private Vector3 targetPosition;
    private bool hasTarget = false;
    private float fixedY;

    private const string topicName = "/unity_clicked_point";
    private static bool publisherRegistered = false;

    void Start()
    {
        cam = Camera.main;
        ros = ROSConnection.GetOrCreateInstance();

        if (!publisherRegistered)
        {
            ros.RegisterPublisher<PointMsg>(topicName);
            publisherRegistered = true;
        }

        if (dogTransform == null)
        {
            Debug.LogError("No dog assigned to DogNavigator.");
            return;
        }

        agent = dogTransform.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.Log("Creating NavMeshAgent on " + dogTransform.name);
            agent = dogTransform.gameObject.AddComponent<NavMeshAgent>();
        }

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.acceleration = 8f;
        agent.angularSpeed = 120f;

        fixedY = dogTransform.position.y;
        targetPosition = dogTransform.position;

        Vector3 startEuler = dogTransform.rotation.eulerAngles;
        dogTransform.rotation = Quaternion.Euler(fixedXRotation, startEuler.y, fixedZRotation);

        Debug.Log("Dog Navigator Ready!");
    }

    void Update()
    {
        if (dogTransform == null || cam == null || agent == null)
            return;

        if (hasTarget && agent.hasPath)
        {
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        hasTarget = false;
                        Debug.Log("Dog reached target!");
                    }
                }
            }
        }

        Vector3 eulerAngles = dogTransform.rotation.eulerAngles;
        dogTransform.rotation = Quaternion.Euler(fixedXRotation, eulerAngles.y, fixedZRotation);

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 point = hit.point;
                targetPosition = new Vector3(point.x, fixedY, point.z);

                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(targetPosition);
                    hasTarget = true;
                    Debug.Log("Moving to: " + targetPosition);
                    PublishDogPosition();  // Publish new target
                }
                else
                {
                    Debug.LogError("Agent not on NavMesh!");
                }

                PointMsg msg = new PointMsg(point.x, point.y, point.z);
                try
                {
                    ros.Publish(topicName, msg);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to publish to ROS: " + e.Message);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StopMovement();
        }
    }

    private void PublishDogPosition()
    {
        if (ros == null || dogTransform == null) return;
        
        Vector3 pos = dogTransform.position;
        PointMsg msg = new PointMsg(pos.x, pos.y, pos.z);
        
        try
        {
            ros.Publish("/unity_dog_position", msg);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to publish dog position: " + e.Message);
        }
    }

    public void StopMovement()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        hasTarget = false;
        Debug.Log("Dog stopped.");

        if (ros != null && dogTransform != null)
        {
            try
            {
                PointMsg stopMsg = new PointMsg(
                    dogTransform.position.x,
                    dogTransform.position.y,
                    dogTransform.position.z
                );
                ros.Publish(topicName, stopMsg);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to send stop: " + e.Message);
            }
        }
    }
}