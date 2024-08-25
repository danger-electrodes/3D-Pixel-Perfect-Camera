using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using System;
using System.Linq;

public class PixelPerfectTransformManager : MonoBehaviour
{
    public static PixelPerfectTransformManager instance;

    public int transformsArraySize;
    private NativeArray<TransformStruct> transforms;
    float4x4 cameraViewMatrix, cameraProjectionMatrix;
    private float2 screenSize;
    MoveTransformsJob moveTransformJob;
    JobHandle moveTransformHandle;

    public Action onTransformsUpdated;
    private static (TransformStruct transform, int index) def = (new TransformStruct(), -1);
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transforms = new NativeArray<TransformStruct>(transformsArraySize, Allocator.Persistent);
        }
        else
            Destroy(this.gameObject);
    }


    private void UpdateTransforms()
    {
        if (onTransformsUpdated == null)
            return;

        onTransformsUpdated();
    }

    public int Register(float3 initialPosition)
    {
        int index = transforms.Select((transform, index) => (transform, index)).DefaultIfEmpty(def).FirstOrDefault(transform => transform.transform.exists == false).index;

        if (index < 0)
        {
            Debug.LogWarning("transform array is full! increase the transformsArraySize");
            return -1;
        }

        TransformStruct newTransform = new TransformStruct
        {
            initialPosition = initialPosition,
            accumulatedDelta = float3.zero,
            delta = float3.zero,
            snappedPosition = float3.zero,
            exists = true
        };

        transforms[index] = newTransform;

        return index;
    }

    public float3 GetSnappedPosition(int transformIndex)
    {
        if (transformIndex < 0 || transformIndex >= transforms.Length)
            return float3.zero;

        TransformStruct tr = transforms[transformIndex];
        return tr.snappedPosition;
    }

    public void UpdateInitialPosition(int transformIndex, float3 initialPosition)
    {
        if (transformIndex < 0 || transformIndex >= transforms.Length)
            return;

        TransformStruct transformToUpdate = transforms[transformIndex];
        transformToUpdate.initialPosition = initialPosition;
        transformToUpdate.accumulatedDelta = float3.zero;
        transforms[transformIndex] = transformToUpdate;
    }

    public float3 GetRealPosition(int transformIndex)
    {
        if (transformIndex < 0 || transformIndex >= transforms.Length)
            return Vector3.zero;

        TransformStruct transformToUpdate = transforms[transformIndex];
        return transformToUpdate.initialPosition + transformToUpdate.accumulatedDelta;
    }


    public void MoveToDirection(int transformIndex, float3 delta)
    {
        if (transformIndex < 0 || transformIndex >= transforms.Length)
            return;

        TransformStruct transformToUpdate = transforms[transformIndex];
        transformToUpdate.delta = delta;
        transforms[transformIndex] = transformToUpdate;
    }

    public void SetScreenSize(Vector2Int ScreenSize)
    {
        this.screenSize = new float2(ScreenSize.x, ScreenSize.y);
    }

    private void Update()
    {
        cameraViewMatrix = Camera.main.worldToCameraMatrix;
        cameraProjectionMatrix = Camera.main.projectionMatrix;

        moveTransformJob = new MoveTransformsJob
        {
            cameraViewMatrix = cameraViewMatrix,
            cameraProjectionMatrix = cameraProjectionMatrix,
            screenSize = screenSize,
            transforms = transforms
        };

        moveTransformHandle = moveTransformJob.Schedule(transforms.Length, 64);
        moveTransformHandle.Complete();

        UpdateTransforms();
    }

    public void Unregister(int transformIndex)
    {
        if (transformIndex < 0 || transformIndex >= transforms.Length || !transforms.IsCreated)
            return;

        TransformStruct transformToUpdate = transforms[transformIndex];
        transformToUpdate.exists = false;

        transforms[transformIndex] = transformToUpdate;
    }

    private void OnDestroy()
    {
        if (transforms.IsCreated)
            transforms.Dispose();
    }

    private struct TransformStruct
    {
        public float3 initialPosition;
        public float3 accumulatedDelta;
        public float3 delta;
        public float3 snappedPosition;
        public bool exists;
    }

    private struct MoveTransformsJob : IJobParallelFor
    {
        public float4x4 cameraViewMatrix;
        public float4x4 cameraProjectionMatrix;
        public float2 screenSize;
        public NativeArray<TransformStruct> transforms;
        public void Execute(int index)
        {
            TransformStruct transform = transforms[index];

            if (!transform.exists || math.length(transform.delta) == 0)
                return;

            transform.accumulatedDelta += transform.delta;

            // Calculate the target position in world space
            float3 targetPosition = transform.initialPosition + transform.accumulatedDelta;

            float3 screenPosition = WorldToScreenPoint(cameraViewMatrix, cameraProjectionMatrix, screenSize, targetPosition);

            screenPosition.x = math.round(screenPosition.x);
            screenPosition.y = math.round(screenPosition.y);

            transform.snappedPosition = ScreenToWorldPoint(cameraViewMatrix, cameraProjectionMatrix, screenSize, screenPosition);

            transform.delta = float3.zero;
            transforms[index] = transform;
        }

        private float3 WorldToScreenPoint(float4x4 viewMatrix, float4x4 projectionMatrix, float2 screenSize, float3 worldPosition)
        {
            // Transform world position to view space
            float4 viewPosition = math.mul(viewMatrix, new float4(worldPosition, 1.0f));

            // Transform view space position to clip space
            float4 clipPosition = math.mul(projectionMatrix, viewPosition);

            // Perform perspective division
            float3 ndcPosition = clipPosition.xyz / clipPosition.w;

            // Transform NDC to screen space
            float2 screenPosition;
            screenPosition.x = (ndcPosition.x + 1.0f) * 0.5f * screenSize.x;
            screenPosition.y = (1.0f - (ndcPosition.y + 1.0f) * 0.5f) * screenSize.y; // Invert Y for screen space

            return new float3(screenPosition, ndcPosition.z);
        }

        private float3 ScreenToWorldPoint(float4x4 viewMatrix, float4x4 projectionMatrix, float2 screenSize, float3 screenPosition)
        {
            // Transform screen position to NDC
            float3 ndcPosition;
            ndcPosition.x = 2.0f * screenPosition.x / screenSize.x - 1.0f;
            ndcPosition.y = 1.0f - 2.0f * screenPosition.y / screenSize.y; // Invert Y from screen space to NDC
            ndcPosition.z = screenPosition.z;

            // Transform NDC to clip space
            float4 clipPosition = new float4(ndcPosition, 1.0f);

            // Get the inverse projection matrix
            float4x4 inverseProjectionMatrix = math.inverse(projectionMatrix);

            // Transform clip space to view space
            float4 viewPosition = math.mul(inverseProjectionMatrix, clipPosition);
            viewPosition /= viewPosition.w; // Perform perspective division

            // Get the inverse view matrix
            float4x4 inverseViewMatrix = math.inverse(viewMatrix);

            // Transform view space to world space
            float4 worldPosition = math.mul(inverseViewMatrix, viewPosition);

            return worldPosition.xyz;
        }
    }
}
