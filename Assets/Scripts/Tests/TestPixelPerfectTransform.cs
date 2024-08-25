using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PixelPerfectTransform))]
public class TestPixelPerfectTransform : MonoBehaviour
{
    public float moveSpeed = 10;
    private PixelPerfectTransform pixelPerfectTransform;
    float accumulatedTime = 0;

    private void Awake()
    {
        pixelPerfectTransform = GetComponent<PixelPerfectTransform>();
    }
    // Update is called once per frame
    void Update()
    {
        accumulatedTime += Time.deltaTime;
        float sinTime = Mathf.Sin(accumulatedTime);
        float sign = sinTime == 0 ? 0 : Mathf.Sign(sinTime);
        pixelPerfectTransform.MoveDirection(new Vector3(sign * moveSpeed, 0, 0));
    }
}
