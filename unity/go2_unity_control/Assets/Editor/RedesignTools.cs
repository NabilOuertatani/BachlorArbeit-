using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode helpers for the UI redesign. Run via:
///   Unity -batchmode -projectPath ... -executeMethod RedesignTools.DumpHierarchy -quit
/// </summary>
public static class RedesignTools
{
    public static void DumpHierarchy()
    {
        var scene = EditorSceneManager.OpenScene("Assets/MainUI.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        sb.AppendLine("=== Scene: " + scene.path + " ===");
        foreach (var root in scene.GetRootGameObjects())
            Dump(root.transform, 0, sb);

        sb.AppendLine();
        sb.AppendLine("=== Build scenes ===");
        foreach (var s in EditorBuildSettings.scenes)
            sb.AppendLine(s.path + " enabled=" + s.enabled);

        string outPath = System.IO.Path.Combine(Application.dataPath, "../hierarchy_dump.txt");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[RedesignTools] Hierarchy written to " + outPath);
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        var comps = t.GetComponents<Component>();
        var names = new StringBuilder();
        foreach (var c in comps)
        {
            if (c == null) { names.Append("MISSING,"); continue; }
            if (c is Transform) continue;
            names.Append(c.GetType().Name).Append(',');
        }

        string rectInfo = "";
        if (t is RectTransform rt)
            rectInfo = $" | aMin={rt.anchorMin} aMax={rt.anchorMax} pos={rt.anchoredPosition} size={rt.sizeDelta}";
        else
            rectInfo = $" | wpos={t.position}";

        sb.AppendLine(new string(' ', depth * 2)
            + (t.gameObject.activeSelf ? "+" : "-") + " "
            + t.name + " [" + names + "]" + rectInfo);

        // Include useful detail for text and image components
        foreach (var c in comps)
        {
            if (c is TMPro.TMP_Text txt)
                sb.AppendLine(new string(' ', depth * 2) + "    text=\"" + txt.text.Replace("\n", "\\n") + "\" size=" + txt.fontSize + " color=" + ColorUtility.ToHtmlStringRGBA(txt.color));
            if (c is UnityEngine.UI.Image img)
                sb.AppendLine(new string(' ', depth * 2) + "    img color=#" + ColorUtility.ToHtmlStringRGBA(img.color) + " sprite=" + (img.sprite ? img.sprite.name : "none") + " type=" + img.type);
            if (c is Camera cam)
                sb.AppendLine(new string(' ', depth * 2) + "    cam depth=" + cam.depth + " ortho=" + cam.orthographic + " fov=" + cam.fieldOfView + " cullmask=" + cam.cullingMask + " clear=" + cam.clearFlags + " bg=#" + ColorUtility.ToHtmlStringRGBA(cam.backgroundColor));
            if (c is Canvas cv)
                sb.AppendLine(new string(' ', depth * 2) + "    canvas mode=" + cv.renderMode + " sort=" + cv.sortingOrder);
        }

        foreach (Transform child in t)
            Dump(child, depth + 1, sb);
    }
}
