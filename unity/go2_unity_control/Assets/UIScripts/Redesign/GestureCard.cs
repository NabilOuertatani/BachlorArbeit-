using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One saved-gesture card on the Home screen: status chip, floor thumbnail,
/// name, mono metadata line and a Run button. Instantiated per saved sequence
/// by <see cref="GestureSequenceUI.DisplaySavedSequences"/>.
/// </summary>
public class GestureCard : MonoBehaviour
{
    public enum Status { Ready, Edited, Running }

    [Header("Wired by RedesignBuilder")]
    public Image statusChipBg;
    public TMP_Text statusChipText;
    public RawImage thumbnail;
    public GameObject thumbnailPlaceholder;
    public TMP_Text nameText;
    public TMP_Text metaText;
    public Button runButton;
    public TMP_Text runButtonText;
    public Button cardButton;   // whole card → edit
    public Button deleteButton; // "X" corner button → delete

    private Texture2D _loadedThumb;

    public void Bind(string gestureName, string metaLine, string thumbnailPath,
                     Status status, Action onRun, Action onEdit, Action onDelete)
    {
        if (nameText != null) nameText.text = gestureName;
        if (metaText != null) metaText.text = metaLine;

        SetStatus(status);
        LoadThumbnail(thumbnailPath);

        if (runButton != null)
        {
            runButton.onClick.RemoveAllListeners();
            runButton.onClick.AddListener(() => onRun?.Invoke());
            runButton.interactable = status != Status.Running;
        }
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onEdit?.Invoke());
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke());
        }
    }

    public void SetStatus(Status status)
    {
        if (statusChipBg == null || statusChipText == null) return;

        switch (status)
        {
            case Status.Running:
                statusChipBg.color = UITheme.WithAlpha(UITheme.Accent, 0.22f);
                statusChipText.color = UITheme.Accent;
                statusChipText.text = "RUNNING";
                break;
            case Status.Edited:
                statusChipBg.color = UITheme.WithAlpha(UITheme.Warning, 0.25f);
                statusChipText.color = UITheme.FromHex("E8B36A");
                statusChipText.text = "EDITED";
                break;
            default:
                statusChipBg.color = UITheme.WithAlpha(UITheme.Accent, 0.18f);
                statusChipText.color = UITheme.FromHex("4CC79A");
                statusChipText.text = "READY";
                break;
        }
    }

    private void LoadThumbnail(string path)
    {
        bool ok = false;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                _loadedThumb = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (_loadedThumb.LoadImage(bytes))
                {
                    thumbnail.texture = _loadedThumb;
                    ok = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GestureCard] Failed to load thumbnail '" + path + "': " + e.Message);
            }
        }

        if (thumbnail != null) thumbnail.gameObject.SetActive(ok);
        if (thumbnailPlaceholder != null) thumbnailPlaceholder.SetActive(!ok);
    }

    void OnDestroy()
    {
        if (_loadedThumb != null) Destroy(_loadedThumb);
    }
}
