using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Screenshot-on-save thumbnail system. Owns a single disabled top-down
/// orthographic camera; on gesture save it frames the waypoint bounding box,
/// renders one 256×144 frame and writes a PNG to
/// Application.persistentDataPath/gestures/{gestureId}.png.
/// No per-card live cameras exist — cards load the PNG from disk.
/// </summary>
public class ThumbnailCapture : MonoBehaviour
{
    public static ThumbnailCapture Instance { get; private set; }

    [Header("Wired by RedesignBuilder")]
    public Camera captureCamera;   // disabled, orthographic, looking straight down

    public const int Width = 256;
    public const int Height = 144;
    public float padding = 1.5f;   // world units added around the waypoint bounds

    public static string ThumbnailDir =>
        Path.Combine(Application.persistentDataPath, "gestures");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Renders a top-down thumbnail of the given waypoints (plus floor, path
    /// overlay and robot) and returns the PNG path, or null on failure.
    /// </summary>
    public string Capture(string gestureId, List<Vector3> waypoints)
    {
        if (captureCamera == null || string.IsNullOrEmpty(gestureId))
            return null;

        // ── Frame the whole floor so the full ground and the robot are visible;
        //    waypoints are encapsulated too in case any lie off the floor.
        //    Fallback when no floor renderer exists: waypoint bounds / origin. ──
        Bounds bounds = default;
        bool hasBounds = TryGetFloorBounds(out bounds);
        if (waypoints != null && waypoints.Count > 0)
        {
            if (!hasBounds) { bounds = new Bounds(waypoints[0], Vector3.zero); hasBounds = true; }
            foreach (Vector3 p in waypoints) bounds.Encapsulate(p);
        }
        if (!hasBounds)
            bounds = new Bounds(Vector3.zero, new Vector3(6f, 0f, 6f));

        float aspect = (float)Width / Height;
        float halfZ = bounds.extents.z + padding;
        float halfX = bounds.extents.x + padding;
        float orthoSize = Mathf.Max(halfZ, halfX / aspect, 1.5f);

        captureCamera.transform.position = new Vector3(bounds.center.x, 20f, bounds.center.z);
        captureCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = orthoSize;
        captureCamera.aspect = aspect;

        // ── Render one frame into a temporary RT ──
        RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevActive = RenderTexture.active;

        Texture2D tex = null;
        string savedPath = null;
        try
        {
            RenderCameraToTexture(captureCamera, rt);

            RenderTexture.active = rt;
            tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            Directory.CreateDirectory(ThumbnailDir);
            savedPath = Path.Combine(ThumbnailDir, gestureId + ".png");
            File.WriteAllBytes(savedPath, tex.EncodeToPNG());
            Debug.Log("[ThumbnailCapture] Saved thumbnail: " + savedPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ThumbnailCapture] Capture failed: " + e.Message);
            savedPath = null;
        }
        finally
        {
            captureCamera.targetTexture = null;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            if (tex != null) Destroy(tex);
        }

        return savedPath;
    }

    const int FloorLayer = 6;

    /// <summary>Combined world bounds of every renderer on the floor layer.</summary>
    static bool TryGetFloorBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.layer != FloorLayer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    /// <summary>
    /// Renders a (disabled) camera into an RT. URP does not support Camera.Render(),
    /// so use a SingleCameraRequest; fall back to Render() for other pipelines.
    /// </summary>
    public static void RenderCameraToTexture(Camera cam, RenderTexture rt)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
    }

    /// <summary>Resolve a stored thumbnail path, tolerating machine moves.</summary>
    public static string ResolvePath(string storedPath, string gestureId)
    {
        if (!string.IsNullOrEmpty(storedPath) && File.Exists(storedPath))
            return storedPath;
        if (!string.IsNullOrEmpty(gestureId))
        {
            string local = Path.Combine(ThumbnailDir, gestureId + ".png");
            if (File.Exists(local)) return local;
        }
        return null;
    }
}
