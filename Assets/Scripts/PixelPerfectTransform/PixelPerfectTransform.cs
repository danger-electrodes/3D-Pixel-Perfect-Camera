using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelPerfectTransform : MonoBehaviour
{
    private int transformIndex;
    private PixelPerfectTransformManager pixelPerfectTransformManager;
    public Action onPositionUpdated;

    // Start is called before the first frame update
    void Start()
    {
        pixelPerfectTransformManager = PixelPerfectTransformManager.instance;

        if (pixelPerfectTransformManager == null)
            Destroy(this.gameObject);

        transformIndex = pixelPerfectTransformManager.Register(transform.position);
        MoveDirection(transform.forward * 0.000001f);
        pixelPerfectTransformManager.onTransformsUpdated += SetPosition;
    }


    private void SetPosition()
    {
        transform.position = pixelPerfectTransformManager.GetSnappedPosition(transformIndex);
        UpdatePositionCallBack();
    }

    private void UpdatePositionCallBack()
    {
        if (onPositionUpdated == null)
            return;

        onPositionUpdated();
    }

    public void MoveDirection(Vector3 direction)
    {

        pixelPerfectTransformManager.MoveToDirection(transformIndex, direction);
    }

    public Vector3 GetRealPosition()
    {
        return pixelPerfectTransformManager.GetRealPosition(transformIndex);
    }

    public Vector3 GetSnappedPosition()
    {
        return pixelPerfectTransformManager.GetSnappedPosition(transformIndex);
    }

    private void OnDestroy()
    {
        pixelPerfectTransformManager.onTransformsUpdated -= SetPosition;
        pixelPerfectTransformManager.Unregister(transformIndex);
    }
}
