using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine;

public class CameraResolution : MonoBehaviour
{
    public static CameraResolution instance;

    public float worldUnitsPerPixel;
    public Vector2Int ScreenSize = new Vector2Int(384, 216);
    public int pixelPerUnits = 16;
    public Vector2 cameraOffset;
    public Vector2 pixelWorldSize;

    private Camera mainCamera, renderTextureCamera;
    private PixelPerfectTransformManager pixelPerfectManager;
    private RectTransform rawImageRenderRectTr;
    private Vector2 pixelRatio;
    private bool fullScreen;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this.gameObject);
    }


    private void Start()
    {
        InitRawImageRender();
        SetResolution();
        SetFullScreen();
        GetPixelSize();
    }

    private void InitRawImageRender()
    {
        rawImageRenderRectTr = GameObject.FindGameObjectWithTag("RawImageRender").GetComponent<RectTransform>();
        renderTextureCamera = GameObject.FindGameObjectWithTag("CameraRenderTexture").GetComponent<Camera>();
    }

    private void GetPixelSize()
    {
        // Assume you want to measure the pixel size at the center of the screen
        Vector3 screenPos1 = new Vector3(Screen.width / 2, Screen.height / 2, mainCamera.nearClipPlane);
        Vector3 screenPos2 = new Vector3(Screen.width / 2 + 1, Screen.height / 2, mainCamera.nearClipPlane);

        // Convert these screen positions to world space
        Vector3 worldPos1 = mainCamera.ScreenToWorldPoint(screenPos1);
        Vector3 worldPos2 = mainCamera.ScreenToWorldPoint(screenPos2);

        // The size of one pixel in world space is the distance between these two points
        Vector2 ViewPortRes = new Vector2(Screen.width, Screen.height);
        pixelWorldSize = Vector3.Distance(worldPos1, worldPos2) * (ViewPortRes / ScreenSize);
    }

    private void SetResolution()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        PixelatedFullScreenRendererFeature.SetScreenDownSampleHeight(ScreenSize.y);

        mainCamera.orthographic = true;
        worldUnitsPerPixel = mainCamera.orthographicSize * 2 / mainCamera.pixelHeight;

        pixelPerfectManager = PixelPerfectTransformManager.instance;
        pixelPerfectManager.SetScreenSize(ScreenSize);

        Vector2 ViewPortRes = new Vector2(Screen.width, Screen.height);

        Vector2 normalizedRes = ((Vector2)ScreenSize) / ViewPortRes;

        Vector2 size = normalizedRes / (Mathf.Max(normalizedRes.x, normalizedRes.y));

        pixelRatio = ViewPortRes / ScreenSize;

        mainCamera.rect = new Rect(default, size)
        {
            center = Vector2.one * 0.5f
        };

        mainCamera.allowMSAA = false;
        rawImageRenderRectTr.sizeDelta = ViewPortRes + (fullScreen ? pixelRatio * 2 : Vector2.zero);
    }

    public void SetCameraOffset()
    {
        rawImageRenderRectTr.anchoredPosition = pixelRatio * cameraOffset;
    }

    private async void SetFullScreen()
    {
        fullScreen = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;

        if (fullScreen)
            Screen.SetResolution(ScreenSize.x, ScreenSize.y, FullScreenMode.Windowed);
        else
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.Windowed);

        Screen.SetMSAASamples(0);
        await Task.Yield();

        Screen.fullScreenMode = fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
    }

    private void OnValidate()
    {
        PixelatedFullScreenRendererFeature.SetScreenDownSampleHeight(ScreenSize.y);
    }

    private void Update()
    {
        SetResolution();
        if (Input.GetKeyDown(KeyCode.F))
            SetFullScreen();
    }
}
