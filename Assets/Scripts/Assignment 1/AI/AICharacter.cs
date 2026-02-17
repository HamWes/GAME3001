using UnityEngine;
using System;

public enum SteeringBehaviour
{
    None,
    Seek,
    Flee,
    Arrive,
    Avoid
}

public class AICharacter : MonoBehaviour
{
    public SteeringBehaviour currentBehaviour = SteeringBehaviour.None;

    [Header("Movement")]
    public float maxSpeed = 6f;
    public float slowingRadius = 4f; 
    public float maxAcceleration = 6f;
    public float targetRadius = 0.1f;
    public float timeToTarget = 0.15f;

    [Header("Orientation")]
    public float maxRotationSpeed = 360f; // degrees per second
    public float rotationSmoothing = 8f;

    float angularVelocity;

    [Header("Avoidance")]
    public float avoidDistance = 4f; 
    public float avoidRadius = 1.5f; 
    public float avoidStrength = 1f;

    [Header("References")]
    public Transform target;
    public Transform obstacle;

    Vector3 velocity;

    void Update()
    {
        Vector3 steering = Vector3.zero;

        switch (currentBehaviour)
        {
            case SteeringBehaviour.Seek:
                steering = Seek(target.position);
                break;

            case SteeringBehaviour.Flee:
                steering = Flee(target.position);
                break;

            case SteeringBehaviour.Arrive:
                steering = Arrive(target.position);
                break;

            case SteeringBehaviour.Avoid:
                    Vector3 arrive = Arrive(target.position);
                    Vector3 avoid = Avoid(target.position, obstacle);

                    steering = arrive + avoid;
                    break;

            case SteeringBehaviour.None:
                velocity = Vector3.zero;
                break;
        }

        velocity += steering * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        transform.position += velocity * Time.deltaTime;

        UpdateOrientation(velocity);
    }

    void UpdateOrientation(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.001f)
            return;

        float targetAngle = Mathf.Atan2(desiredDirection.x, desiredDirection.z) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.y;

        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);

        float desiredAngularVelocity = angleDifference * rotationSmoothing;

        desiredAngularVelocity = Mathf.Clamp(desiredAngularVelocity, -maxRotationSpeed, maxRotationSpeed);

        angularVelocity = Mathf.Lerp(angularVelocity, desiredAngularVelocity, Time.deltaTime * rotationSmoothing);

        transform.Rotate(Vector3.up, angularVelocity * Time.deltaTime);
    }

    Vector3 Seek(Vector3 targetPos)
    {
        Vector3 desired = (targetPos - transform.position).normalized * maxSpeed;
        return desired - velocity;
    }

    Vector3 Flee(Vector3 threatPos)
    {
        Vector3 desired = (transform.position - threatPos).normalized * maxSpeed;
        return desired - velocity;
    }

    Vector3 Arrive(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance < targetRadius)
        {
            velocity = Vector3.zero;
            angularVelocity = 0f;
            return Vector3.zero;
        }

        float targetSpeed = maxSpeed;
        if (distance < slowingRadius)
        {
            targetSpeed = maxSpeed * (distance / slowingRadius);
        }
        Vector3 targetVelocity = toTarget.normalized * targetSpeed;
        Vector3 acceleration = (targetVelocity - velocity) / timeToTarget;

        return Vector3.ClampMagnitude(acceleration, maxAcceleration);
    }

    Vector3 Avoid(Vector3 targetPos, Transform obstacle)
    {
        if (obstacle == null || velocity.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 toObstacle = obstacle.position - transform.position;
        float distanceToObstacle = toObstacle.magnitude;

        if (distanceToObstacle > avoidDistance)
            return Vector3.zero;

        Vector3 forward = velocity.normalized;
        float dot = Vector3.Dot(forward, toObstacle.normalized);

        if (dot < 0.5f)
            return Vector3.zero;

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float side = Vector3.Dot(right, toObstacle);

        Vector3 avoidDirection = side > 0 ? -right : right;

        Vector3 avoidTarget = obstacle.position + avoidDirection * avoidRadius;

        return Seek(avoidTarget) * avoidStrength;
    }
}
