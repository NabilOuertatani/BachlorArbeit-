using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Headless Play-Mode screenshot driver for the redesigned screens.
/// Seeds demo gestures through the real data pipeline (incl. thumbnail
/// capture), then composites 3D camera + UI canvas into PNGs.
///
/// Run (no -quit; it exits by itself):
///   Unity -batchmode -projectPath ... -executeMethod RedesignShots.Run
///
/// Outputs: <project>/Screenshots/home.png and configure.png
/// NOTE: this writes demo data into Assets/Data/gestures.json — restore the
/// file from git afterwards if you don't want the demo content committed.
/// </summary>
public static class RedesignShots
{
    const string Flag = "REDESIGN_SHOTS_ACTIVE";
    const int W = 1512, H = 920;

    public static void Run()
    {
        SessionState.SetBool(Flag, true);
        EditorSceneManager.OpenScene("Assets/MainUI.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (!SessionState.GetBool(Flag, false)) return;

        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var routine = Script();
                EditorApplication.CallbackFunction step = null;
                step = () =>
                {
                    try
                    {
                        if (!routine.MoveNext())
                        {
                            EditorApplication.update -= step;
                            Finish(0);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[RedesignShots] FAILED: " + e);
                        EditorApplication.update -= step;
                        Finish(1);
                    }
                };
                EditorApplication.update += step;
            }
        };
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Flag, false);
        Debug.Log("[RedesignShots] Exiting with code " + code);
        EditorApplication.Exit(code);
    }

    // ───────────────────────── the actual scripted session ─────────────────────────

    static IEnumerator Script()
    {
        // Let Start()/Invoke() settle
        yield return WaitFrames(40);

        var ui = Object.FindFirstObjectByType<GestureSequenceUI>();
        var gdm = GestureDataManager.Instance;
        if (ui == null || gdm == null)
            throw new System.Exception("GestureSequenceUI / GestureDataManager not found");

        // ── Seed demo gestures through the real pipeline ──
        ui.ShowConfig();                       // activate floor + walls for thumbnails
        yield return WaitFrames(5);

        gdm.savedSequences.RemoveAll(q => q.name == "Fetch loop" || q.name == "Doorway patrol");
        gdm.SaveSequences(gdm.savedSequences);

        var seeding = SeedGesture(gdm, "Fetch loop", "Sit down", false, new[]
        {
            new Vector3(-14.5f, 0, 4.0f), new Vector3(-12.0f, 0, 6.5f), new Vector3(-9.5f, 0, 5.2f)
        });
        while (seeding.MoveNext()) yield return null;
        seeding = SeedGesture(gdm, "Doorway patrol", "Raise Hand", true, new[]
        {
            new Vector3(-15.0f, 0, 7.5f), new Vector3(-13.0f, 0, 5.0f),
            new Vector3(-10.5f, 0, 7.0f), new Vector3(-8.5f, 0, 5.5f)
        });
        while (seeding.MoveNext()) yield return null;

        // ── Home screen shot ──
        ui.ShowHome();
        ui.DisplaySavedSequences();
        yield return WaitFrames(10);
        Capture("home.png");

        // ── Configure screen shot (edit the first gesture; overlays kick in) ──
        ui.EditSavedSequence(gdm.savedSequences.Count - 2 >= 0 ? gdm.savedSequences.Count - 2 : 0);
        yield return WaitFrames(12);
        Capture("configure.png");

        Debug.Log("[RedesignShots] All screenshots captured.");
    }

    static IEnumerator WaitFrames(int n)
    {
        int target = Time.frameCount + n;
        while (Time.frameCount < target) yield return null;
    }

    /// <summary>Builds one demo sequence with real waypoints + thumbnail and persists it.</summary>
    static IEnumerator SeedGesture(GestureDataManager gdm, string name, string behavior, bool edited, Vector3[] wps)
    {
        var seq = new GestureDataManager.SavedSequence { name = name };
        seq.EnsureId();
        seq.edited = edited;

        var move = new GestureDataManager.SavedStep
        {
            stepName = "Move",
            waypoints = new List<GestureDataManager.SerializableVector3>()
        };
        foreach (var p in wps)
            move.waypoints.Add(new GestureDataManager.SerializableVector3(p, 0.4f));
        seq.steps.Add(move);
        seq.steps.Add(new GestureDataManager.SavedStep { stepName = behavior });

        // Real thumbnail through ThumbnailCapture. Load the waypoints, then let a
        // few frames pass so WaypointOverlayController draws the dashed path +
        // markers before the capture (same state a real save happens in).
        if (RobotBridge.Instance != null)
            RobotBridge.Instance.LoadWaypoints(new List<Vector3>(wps));
        var wait = WaitFrames(4);
        while (wait.MoveNext()) yield return null;
        if (ThumbnailCapture.Instance != null)
            seq.thumbnailPath = ThumbnailCapture.Instance.Capture(seq.id, new List<Vector3>(wps));
        if (RobotBridge.Instance != null)
            RobotBridge.Instance.ClearWaypoints();

        gdm.AddSequence(seq);
        Debug.Log("[RedesignShots] Seeded '" + name + "' thumb=" + seq.thumbnailPath);
    }

    // ───────────────────────── frame capture (URP camera stacking) ─────────────────────────

    static void Capture(string fileName)
    {
        var main = Camera.main;
        var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

        // Temp base camera that rasterizes the (normally ScreenSpaceOverlay) canvas
        // over a fully transparent background; composited on the CPU below.
        var uiCamGo = new GameObject("UICaptureCam");
        var uiCam = uiCamGo.AddComponent<Camera>();
        uiCam.orthographic = true;
        uiCam.cullingMask = 1 << 5;
        uiCam.nearClipPlane = 0.1f;
        uiCam.farClipPlane = 10f;
        uiCam.clearFlags = CameraClearFlags.SolidColor;
        uiCam.backgroundColor = new Color(0, 0, 0, 0);

        var prevMode = canvas.renderMode;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCam;
        canvas.planeDistance = 1f;

        var rt3d = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
        var rtUi = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            ThumbnailCapture.RenderCameraToTexture(main, rt3d);
            ThumbnailCapture.RenderCameraToTexture(uiCam, rtUi);

            Color[] scene = ReadPixels(rt3d);
            Color[] ui = ReadPixels(rtUi);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < scene.Length; i++)
            {
                float a = ui[i].a;
                scene[i] = new Color(
                    ui[i].r * a + scene[i].r * (1 - a),
                    ui[i].g * a + scene[i].g * (1 - a),
                    ui[i].b * a + scene[i].b * (1 - a),
                    1f);
            }
            tex.SetPixels(scene);
            tex.Apply();

            string dir = Path.Combine(Application.dataPath, "../Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[RedesignShots] Wrote " + path);
        }
        finally
        {
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt3d);
            RenderTexture.ReleaseTemporary(rtUi);
            canvas.renderMode = prevMode;
            canvas.worldCamera = null;
            Object.Destroy(uiCamGo);
        }
    }

    static Color[] ReadPixels(RenderTexture rt)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        var px = tex.GetPixels();
        Object.Destroy(tex);
        return px;
    }
}
