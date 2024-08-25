using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(PixelPerfectTransform))]
public class CameraController : MonoBehaviour
{
    public float borderThreshold = 0.3f;// the threshold at which the camera starts to follow the target
    public Transform target; //the target to follow
    public float speed = 10f; // Speed of the camera movement in world units per second
    public float speedWhileRotating = 5f; // Speed of the camera movement in world units per second
    public float rotationSpeed = 5f; // Speed of the camera rotation in degrees per second
    public float rotationReachSpeed = 40f; // The speed at which the camera move to the target while rotating

    private Camera mainCamera;
    private PixelPerfectTransform pixelPerfectTransform;
    private Vector2 cameraOffsetDelta;
    private float targetAngleY;
    private bool isRotating = false;
    private int rotationDirection = 0;
    private float yAngle;
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        pixelPerfectTransform = GetComponent<PixelPerfectTransform>();
        pixelPerfectTransform.onPositionUpdated += SetCameraOffset;
        targetAngleY = transform.eulerAngles.y;
    }

    void Update()
    {
        if (!isRotating)
            MoveCamera();
        else
            MoveCameraAround();

        RotateCamera();
    }
     
    void MoveCamera()
    {
        if (target == null) return;

        Vector3 viewPortPosition = mainCamera.WorldToViewportPoint(target.position);

        viewPortPosition.x = (Mathf.Clamp01(viewPortPosition.x) - 0.5f) * 2; // -1 to 1
        viewPortPosition.y = (Mathf.Clamp01(viewPortPosition.y) - 0.5f) * 2; // -1 to 1

        float absX = Mathf.Abs(viewPortPosition.x);
        float absY = Mathf.Abs(viewPortPosition.y);
        bool moveX = absX > borderThreshold;
        bool moveZ = absY > borderThreshold;

        if (!moveX && !moveZ) return;

        float dirX = Mathf.Sign(viewPortPosition.x);
        float dirY = Mathf.Sign(viewPortPosition.y);
        float horizontal = moveX ? dirX * absX : 0;
        float vertical = moveZ ? dirY * absY : 0;

        Vector3 targetPosition = transform.position;
        Vector3 normalizedForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        targetPosition += Vector3.Cross(normalizedForward, -Vector3.up) * speed * horizontal;
        targetPosition += normalizedForward * speed * vertical;
        Vector3 worldDelta = targetPosition - transform.position;
        worldDelta *= Time.deltaTime;

        pixelPerfectTransform.MoveDirection(worldDelta);
    }

    void MoveCameraAround()
    {
        if (target == null) return;

        Vector3 targetDirection = (new Vector3(target.position.x, 0, target.position.z) - new Vector3(transform.position.x, 0, transform.position.z));
        targetDirection = targetDirection.normalized;

        Vector3 targetDirectionRightLeft = Quaternion.Euler(0, rotationDirection * 90, 0) * targetDirection;


        Vector3 viewPortPosition = mainCamera.WorldToViewportPoint(target.position);

        viewPortPosition.x = (Mathf.Clamp01(viewPortPosition.x) - 0.5f) * 2; // -1 to 1
        viewPortPosition.y = (Mathf.Clamp01(viewPortPosition.y) - 0.5f) * 2; // -1 to 1

        float absX = Mathf.Abs(viewPortPosition.x);
        float absY = Mathf.Abs(viewPortPosition.y);
        float dirX = Mathf.Sign(viewPortPosition.x);
        float dirY = Mathf.Sign(viewPortPosition.y);
        float horizontal = dirX * absX;
        float vertical = dirY * absY;

        Vector3 targetPosition = transform.position;
        Vector3 normalizedForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        targetPosition += Vector3.Cross(normalizedForward, -Vector3.up) * rotationReachSpeed * horizontal;
        targetPosition += normalizedForward * rotationReachSpeed * vertical;

        Vector3 worldDelta = targetDirectionRightLeft * GetAngle(yAngle) * speedWhileRotating + (targetPosition - transform.position);
        worldDelta *= Time.deltaTime;

        pixelPerfectTransform.MoveDirection(worldDelta);
    }

    void RotateCamera()
    {
        if (target == null)
            return;

        if (Input.GetKeyDown(KeyCode.A) && !isRotating)
        {
            targetAngleY += 45f;
            isRotating = true;
            rotationDirection = -1;
        }
        if (Input.GetKeyDown(KeyCode.D) && !isRotating)
        {
            targetAngleY -= 45f;
            isRotating = true;
            rotationDirection = 1;
        }

        if (isRotating)
        {
            // Current rotation
            float currentAngleY = transform.eulerAngles.y;

            // Smoothly interpolate to the target angle
            float newAngleY = Mathf.LerpAngle(currentAngleY, targetAngleY, rotationSpeed * Time.deltaTime);

            transform.Rotate(Vector3.up * (newAngleY - currentAngleY), Space.World);

            // Check if the rotation is close enough to the target

            yAngle = Mathf.Abs(newAngleY - targetAngleY) % 360f;

            if (WithinAngle(yAngle, 0.1f))
            {
                transform.eulerAngles = new Vector3(transform.eulerAngles.x, targetAngleY, transform.eulerAngles.z);
                isRotating = false;
                rotationDirection = 0;
            }
        }
    }

    private bool WithinAngle(float angle, float threshold)
    {
        return angle < threshold || angle > 360f - threshold;
    }

    private float GetAngle(float angle)
    {
        return Mathf.Min(angle, 360f - angle);
    }

    private void SetCameraOffset()
    {
        Vector3 realPosition = pixelPerfectTransform.GetRealPosition();
        Vector3 snappedPosition = pixelPerfectTransform.GetSnappedPosition();

        float offsetX = (realPosition.x - snappedPosition.x);
        float offsetZ = realPosition.z - snappedPosition.z;


        cameraOffsetDelta = new Vector2(offsetX, offsetZ) * -1f;
        CameraResolution.instance.cameraOffset = cameraOffsetDelta;
        CameraResolution.instance.SetCameraOffset();
    }

    private void OnDestroy()
    {
        pixelPerfectTransform.onPositionUpdated -= SetCameraOffset;
    }
}
