using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// One-shot builder for the gesture-console UI redesign.
/// Generates all sprites/icons procedurally (no binary assets in the repo
/// besides PNGs it writes), creates the JetBrains Mono TMP font asset, builds
/// the GestureCard / WaypointListItem / WaypointMarker prefabs and restructures
/// the MainUI scene — wiring every new reference on the existing components.
///
/// Run headless:
///   Unity -batchmode -projectPath ... -executeMethod RedesignBuilder.BuildAll -quit
///
/// It is safe to re-run: generated objects are found-and-rebuilt by name.
/// </summary>
public static class RedesignBuilder
{
    const string SpriteDir = "Assets/UI/Redesign";
    const string PrefabDir = "Assets/Prefabs/UI";
    const string FontAssetPath = "Assets/Fonts/JetBrainsMono-Regular SDF.asset";
    const string DashMatPath = "Assets/Material/DashedPath.mat";

    // Loaded asset refs
    static Sprite round12Fill, round12Stroke, round6Fill, round6Stroke;
    static Sprite circle, ring, cornerTick, gridPattern, dashedCardBorder;
    static Sprite iconPlay, iconPlus, iconSearch, iconTrash, iconWalk, iconTarget, iconSave;
    static TMP_FontAsset monoFont, sansFont;
    static Material dashMat;
    static GestureCard cardPrefab;
    static WaypointListItem listItemPrefab;
    static WaypointMarker markerPrefab;

    public static void BuildAll()
    {
        GenerateSprites();
        GenerateFont();
        GenerateMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadAssets();
        BuildPrefabs();
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[RedesignBuilder] BuildAll complete.");
    }

    // ═════════════════════════════ Sprites ═════════════════════════════

    static void GenerateSprites()
    {
        Directory.CreateDirectory(SpriteDir);

        // Rounded rects (9-sliced)
        WriteSprite("round12_fill", RoundRect(64, 64, 12, 0), border: new Vector4(20, 20, 20, 20));
        WriteSprite("round12_stroke", RoundRect(64, 64, 12, 1.2f), border: new Vector4(20, 20, 20, 20));
        WriteSprite("round6_fill", RoundRect(32, 32, 6, 0), border: new Vector4(12, 12, 12, 12));
        WriteSprite("round6_stroke", RoundRect(32, 32, 6, 1.2f), border: new Vector4(12, 12, 12, 12));

        // Circles
        var p = new Painter(64, 64);
        p.FillCircle(new Vector2(32, 32), 29);
        WriteSprite("circle", p);

        p = new Painter(64, 64);
        p.StrokeCircle(new Vector2(32, 32), 27, 6f);
        WriteSprite("ring", p);

        // Corner HUD tick (top-left "L"; rotated at use for other corners)
        p = new Painter(24, 24);
        p.StrokeSeg(new Vector2(4, 20), new Vector2(15, 20), 2.4f);
        p.StrokeSeg(new Vector2(4, 20), new Vector2(4, 9), 2.4f);
        WriteSprite("corner_tick", p);

        // Dash cell for the LineRenderer material (wrap = Repeat)
        p = new Painter(32, 8);
        p.FillRect(new Rect(1, 0, 15, 8));
        WriteSprite("dash", p, wrap: TextureWrapMode.Repeat);

        // Grid placeholder for missing thumbnails
        p = new Painter(256, 144);
        for (int x = 8; x < 256; x += 24) p.StrokeSeg(new Vector2(x, 0), new Vector2(x, 144), 1f, 0.35f);
        for (int y = 12; y < 144; y += 24) p.StrokeSeg(new Vector2(0, y), new Vector2(256, y), 1f, 0.35f);
        WriteSprite("grid", p);

        // Dashed rounded border for the "New gesture" tile (drawn at card size)
        p = new Painter(250, 270);
        p.DashedRoundRectBorder(new Rect(1.5f, 1.5f, 247, 267), 12, 1.6f, 7f, 6f);
        WriteSprite("dashed_card_border", p);

        // ── Icons (Tabler-ish stroked glyphs, 40×40, white) ──
        p = new Painter(40, 40);   // play — triangle
        p.StrokeSeg(new Vector2(15, 11), new Vector2(15, 29), 3.2f);
        p.StrokeSeg(new Vector2(15, 29), new Vector2(29, 20), 3.2f);
        p.StrokeSeg(new Vector2(29, 20), new Vector2(15, 11), 3.2f);
        WriteSprite("icon_play", p);

        p = new Painter(40, 40);   // plus
        p.StrokeSeg(new Vector2(20, 10), new Vector2(20, 30), 3.4f);
        p.StrokeSeg(new Vector2(10, 20), new Vector2(30, 20), 3.4f);
        WriteSprite("icon_plus", p);

        p = new Painter(40, 40);   // search
        p.StrokeCircle(new Vector2(17, 23), 8, 3f);
        p.StrokeSeg(new Vector2(23, 17), new Vector2(31, 9), 3.2f);
        WriteSprite("icon_search", p);

        p = new Painter(40, 40);   // trash
        p.StrokeSeg(new Vector2(9, 29), new Vector2(31, 29), 3f);
        p.StrokeSeg(new Vector2(16, 33), new Vector2(24, 33), 3f);
        p.StrokeSeg(new Vector2(13, 29), new Vector2(14.5f, 9), 3f);
        p.StrokeSeg(new Vector2(27, 29), new Vector2(25.5f, 9), 3f);
        p.StrokeSeg(new Vector2(14.5f, 9), new Vector2(25.5f, 9), 3f);
        WriteSprite("icon_trash", p);

        p = new Painter(40, 40);   // walk — stick figure
        p.FillCircle(new Vector2(22, 33), 3.4f);
        p.StrokeSeg(new Vector2(21.5f, 28), new Vector2(19.5f, 20), 3f);
        p.StrokeSeg(new Vector2(19.5f, 20), new Vector2(14, 8), 3f);
        p.StrokeSeg(new Vector2(19.5f, 20), new Vector2(25, 13), 3f);
        p.StrokeSeg(new Vector2(25, 13), new Vector2(27, 7), 3f);
        p.StrokeSeg(new Vector2(21, 26), new Vector2(14.5f, 21), 3f);
        p.StrokeSeg(new Vector2(21.5f, 26), new Vector2(27, 22), 3f);
        WriteSprite("icon_walk", p);

        p = new Painter(40, 40);   // target — circle + center dot (Add point)
        p.StrokeCircle(new Vector2(20, 20), 10, 3f);
        p.FillCircle(new Vector2(20, 20), 3.4f);
        WriteSprite("icon_target", p);

        p = new Painter(40, 40);   // save — floppy
        p.StrokeRoundRect(new Rect(8, 8, 24, 24), 3, 3f);
        p.StrokeSeg(new Vector2(14, 32), new Vector2(14, 25), 2.6f);
        p.StrokeSeg(new Vector2(14, 25), new Vector2(26, 25), 2.6f);
        p.StrokeSeg(new Vector2(26, 25), new Vector2(26, 32), 2.6f);
        p.StrokeRoundRect(new Rect(15, 11, 10, 8), 1.5f, 2.6f);
        WriteSprite("icon_save", p);
    }

    static Painter RoundRect(int w, int h, float radius, float strokeWidth)
    {
        var p = new Painter(w, h);
        var rect = new Rect(1, 1, w - 2, h - 2);
        if (strokeWidth <= 0) p.FillRoundRect(rect, radius);
        else p.StrokeRoundRect(rect, radius, strokeWidth);
        return p;
    }

    static void WriteSprite(string name, Painter painter,
                            Vector4? border = null, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        string path = SpriteDir + "/" + name + ".png";
        File.WriteAllBytes(path, painter.EncodePNG());
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = wrap;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (border.HasValue) importer.spriteBorder = border.Value;
        importer.SaveAndReimport();
    }

    // ═════════════════════════════ Font ═════════════════════════════

    static void GenerateFont()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return;

        var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/JetBrainsMono-Regular.ttf");
        if (ttf == null) { Debug.LogError("[RedesignBuilder] JetBrainsMono TTF missing"); return; }

        var fa = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 512, 512,
                                               AtlasPopulationMode.Dynamic, true);
        fa.name = "JetBrainsMono-Regular SDF";
        AssetDatabase.CreateAsset(fa, FontAssetPath);
        fa.material.name = fa.name + " Material";
        fa.atlasTexture.name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
    }

    // ═════════════════════════════ Materials ═════════════════════════════

    static void GenerateMaterials()
    {
        var dashTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir + "/dash.png");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DashMatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, DashMatPath);
        }
        mat.shader = Shader.Find("Sprites/Default");
        mat.mainTexture = dashTex;
        mat.color = UITheme.Accent;
        EditorUtility.SetDirty(mat);
    }

    // ═════════════════════════════ Asset loading ═════════════════════════════

    static Sprite S(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");

    static void LoadAssets()
    {
        round12Fill = S("round12_fill"); round12Stroke = S("round12_stroke");
        round6Fill = S("round6_fill"); round6Stroke = S("round6_stroke");
        circle = S("circle"); ring = S("ring"); cornerTick = S("corner_tick");
        gridPattern = S("grid"); dashedCardBorder = S("dashed_card_border");
        iconPlay = S("icon_play"); iconPlus = S("icon_plus"); iconSearch = S("icon_search");
        iconTrash = S("icon_trash"); iconWalk = S("icon_walk"); iconTarget = S("icon_target");
        iconSave = S("icon_save");
        monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        dashMat = AssetDatabase.LoadAssetAtPath<Material>(DashMatPath);
    }

    // ═════════════════════════════ Prefabs ═════════════════════════════

    static void BuildPrefabs()
    {
        Directory.CreateDirectory(PrefabDir);
        sansFont = TMP_Settings.defaultFontAsset;

        BuildGestureCardPrefab();
        BuildWaypointListItemPrefab();
        BuildWaypointMarkerPrefab();

        cardPrefab = AssetDatabase.LoadAssetAtPath<GestureCard>(PrefabDir + "/GestureCard.prefab");
        listItemPrefab = AssetDatabase.LoadAssetAtPath<WaypointListItem>(PrefabDir + "/WaypointListItem.prefab");
        markerPrefab = AssetDatabase.LoadAssetAtPath<WaypointMarker>(PrefabDir + "/WaypointMarker.prefab");
    }

    static void BuildGestureCardPrefab()
    {
        var root = NewUI("GestureCard", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 270);
        var bg = AddImg(root, round12Fill, UITheme.Panel, Image.Type.Sliced, raycast: true);
        var card = root.AddComponent<GestureCard>();
        var cardBtn = root.AddComponent<Button>();
        cardBtn.targetGraphic = bg;

        var border = NewUI("Border", root.transform);
        Stretch(border, Vector2.zero, Vector2.zero);
        AddImg(border, round12Stroke, UITheme.Hairline, Image.Type.Sliced);

        // Status chip
        var chip = NewUI("StatusChip", root.transform);
        Point(chip, new Vector2(0, 1), new Vector2(14, -14), new Vector2(66, 20), pivot: new Vector2(0, 1));
        var chipBg = AddImg(chip, round6Fill, UITheme.WithAlpha(UITheme.Accent, 0.18f), Image.Type.Sliced);
        var chipLabel = NewUI("Label", chip.transform);
        Stretch(chipLabel, Vector2.zero, Vector2.zero);
        var chipText = AddText(chipLabel, "READY", monoFont, 10, UITheme.FromHex("4CC79A"),
                               TextAlignmentOptions.Center);
        chipText.characterSpacing = 3;

        // "···" menu (delete)
        var menu = NewUI("MenuButton", root.transform);
        Point(menu, new Vector2(1, 1), new Vector2(-8, -6), new Vector2(32, 28), pivot: new Vector2(1, 1));
        var menuBg = AddImg(menu, null, new Color(1, 1, 1, 0f), Image.Type.Simple, raycast: true);
        var menuBtn = menu.AddComponent<Button>();
        menuBtn.targetGraphic = menuBg;
        var menuLabel = NewUI("Label", menu.transform);
        Stretch(menuLabel, Vector2.zero, Vector2.zero);
        AddText(menuLabel, "···", sansFont, 16, UITheme.TextMuted, TextAlignmentOptions.Center);

        // Thumbnail area
        var thumb = NewUI("Thumb", root.transform);
        StretchTop(thumb, new Vector2(14, -168), new Vector2(-14, -42));
        AddImg(thumb, round6Fill, UITheme.Page, Image.Type.Sliced);
        var thumbImg = NewUI("ThumbImage", thumb.transform);
        Stretch(thumbImg, new Vector2(3, 3), new Vector2(-3, -3));
        var raw = thumbImg.AddComponent<RawImage>();
        raw.raycastTarget = false;
        var placeholder = NewUI("ThumbPlaceholder", thumb.transform);
        Stretch(placeholder, new Vector2(3, 3), new Vector2(-3, -3));
        AddImg(placeholder, gridPattern, UITheme.WithAlpha(UITheme.TextMuted, 0.5f), Image.Type.Simple);

        // Name + meta
        var name = NewUI("Name", root.transform);
        StretchTop(name, new Vector2(14, -198), new Vector2(-14, -174));
        var nameText = AddText(name, "Gesture", sansFont, 15, UITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        nameText.fontStyle = FontStyles.Bold;

        var meta = NewUI("Meta", root.transform);
        StretchTop(meta, new Vector2(14, -220), new Vector2(-14, -198));
        var metaText = AddText(meta, "0 WAYPOINTS", monoFont, 10, UITheme.TextMuted, TextAlignmentOptions.MidlineLeft);
        metaText.characterSpacing = 2;

        // Run button
        var run = NewUI("RunButton", root.transform);
        StretchBottom(run, new Vector2(14, 12), new Vector2(-14, 48));
        var runBg = AddImg(run, round6Fill, UITheme.Card, Image.Type.Sliced, raycast: true);
        var runBtn = run.AddComponent<Button>();
        runBtn.targetGraphic = runBg;
        var runIcon = NewUI("Icon", run.transform);
        Point(runIcon, new Vector2(0.5f, 0.5f), new Vector2(-22, 0), new Vector2(13, 13));
        AddImg(runIcon, iconPlay, UITheme.TextPrimary, Image.Type.Simple);
        var runLabel = NewUI("Label", run.transform);
        Point(runLabel, new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(60, 24));
        var runText = AddText(runLabel, "Run", sansFont, 13, UITheme.TextPrimary, TextAlignmentOptions.Center);

        // Wire component refs
        card.statusChipBg = chipBg;
        card.statusChipText = chipText;
        card.thumbnail = raw;
        card.thumbnailPlaceholder = placeholder;
        card.nameText = nameText;
        card.metaText = metaText;
        card.runButton = runBtn;
        card.runButtonText = runText;
        card.cardButton = cardBtn;
        card.deleteButton = menuBtn;

        SavePrefab(root, PrefabDir + "/GestureCard.prefab");
    }

    static void BuildWaypointListItemPrefab()
    {
        var root = NewUI("WaypointListItem", null);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(290, 34);
        AddImg(root, round6Fill, UITheme.WithAlpha(UITheme.Card, 0.55f), Image.Type.Sliced);
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 34;
        le.minHeight = 34;
        var item = root.AddComponent<WaypointListItem>();

        var badge = NewUI("Badge", root.transform);
        Point(badge, new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(20, 20), pivot: new Vector2(0, 0.5f));
        item.badgeBg = AddImg(badge, round6Fill, UITheme.WithAlpha(UITheme.Accent, 0.30f), Image.Type.Sliced);
        var badgeLabel = NewUI("Label", badge.transform);
        Stretch(badgeLabel, Vector2.zero, Vector2.zero);
        item.badgeText = AddText(badgeLabel, "1", monoFont, 11, UITheme.AccentSoft, TextAlignmentOptions.Center);

        var coords = NewUI("Coords", root.transform);
        Stretch(coords, new Vector2(36, 0), new Vector2(-8, 0));
        item.coordsText = AddText(coords, "x 0.0, y 0.0", monoFont, 12, UITheme.TextSecondary,
                                  TextAlignmentOptions.MidlineLeft);

        SavePrefab(root, PrefabDir + "/WaypointListItem.prefab");
    }

    static void BuildWaypointMarkerPrefab()
    {
        var root = NewUI("WaypointMarker", null);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        rt.localScale = Vector3.one * 0.012f;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        root.AddComponent<BillboardToCamera>();
        var marker = root.AddComponent<WaypointMarker>();

        var fill = NewUI("Fill", root.transform);
        Stretch(fill, new Vector2(10, 10), new Vector2(-10, -10));
        marker.fill = AddImg(fill, circle, Color.white, Image.Type.Simple);

        var ringGo = NewUI("Ring", root.transform);
        Stretch(ringGo, Vector2.zero, Vector2.zero);
        marker.ring = AddImg(ringGo, ring, UITheme.Accent, Image.Type.Simple);

        var num = NewUI("Num", root.transform);
        Stretch(num, Vector2.zero, Vector2.zero);
        marker.numberText = AddText(num, "1", monoFont, 42, UITheme.Page, TextAlignmentOptions.Center);
        marker.numberText.fontStyle = FontStyles.Bold;

        SavePrefab(root, PrefabDir + "/WaypointMarker.prefab");
    }

    static void SavePrefab(GameObject temp, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
    }

    // ═════════════════════════════ Scene ═════════════════════════════

    static Transform canvasT, mainPanel, homeScreen, configScene, dogPanel;
    static GestureSequenceUI ui;

    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/MainUI.unity", OpenSceneMode.Single);

        canvasT = GameObject.Find("Canvas").transform;
        mainPanel = FindDeep(canvasT, "MainPanel");
        homeScreen = FindDeep(mainPanel, "HomeScreen");
        configScene = FindDeep(mainPanel, "Configscene");
        dogPanel = FindDeep(canvasT, "dogPanel");
        ui = Object.FindFirstObjectByType<GestureSequenceUI>(FindObjectsInactive.Include);
        sansFont = FindDeep(homeScreen, "HomeTitle").GetComponent<TMP_Text>().font;

        StyleGlobal();
        BuildHome();
        BuildConfig();
        AddSystems();
        WireUI();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[RedesignBuilder] Scene saved.");
    }

    static void StyleGlobal()
    {
        // Single controlled dim layer over the 3D view; page tint
        SetColor(canvasT, "Background", UITheme.WithAlpha(UITheme.Page, 0.45f));
        SetColor(canvasT, "AppRobot", new Color(0, 0, 0, 0));
        SetColor(mainPanel, null, new Color(0, 0, 0, 0));

        // Top bar: branding moves into home header / config breadcrumb
        var toBar = FindDeep(mainPanel, "ToBar");
        toBar.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        FindDeep(toBar, "LogoText").gameObject.SetActive(false);

        // Sidebar
        var sidebar = FindDeep(canvasT, "Sidebar");
        sidebar.GetComponent<Image>().color = UITheme.FromHex("171A26");
        foreach (string btnName in new[] { "HomeButton", "ConfigButton", "ActivityButton" })
        {
            var btn = FindDeep(sidebar, btnName);
            if (btn == null) continue;
            btn.GetComponent<Image>().color = new Color(1, 1, 1, 0.03f);
            var icon = btn.Find("Image");
            if (icon != null)
                icon.GetComponent<Image>().color =
                    btnName == "HomeButton" ? UITheme.Accent : UITheme.TextMuted;
        }

        // 3D camera: keep framing/lens — only swap the clear color to the dark page hue
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        cam.backgroundColor = UITheme.FromHex("262A3A");
    }

    // ───────────────────────────── Home ─────────────────────────────

    static void BuildHome()
    {
        homeScreen.GetComponent<Image>().color = UITheme.Page;
        var homeRt = (RectTransform)homeScreen;
        homeRt.anchoredPosition = Vector2.zero;
        homeRt.sizeDelta = Vector2.zero;   // cover the (now-empty) top bar strip too

        // Header
        var title = FindDeep(homeScreen, "HomeTitle").GetComponent<TMP_Text>();
        title.text = "Gesture profiles";
        title.fontSize = 26;
        title.fontStyle = FontStyles.Bold;
        title.color = UITheme.TextPrimary;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(0, 1);
        titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(50, -28);
        titleRt.sizeDelta = new Vector2(500, 36);

        Kill(homeScreen, "HomeSubtitle");
        var sub = NewUI("HomeSubtitle", homeScreen);
        StretchTop(sub, new Vector2(50, -92), new Vector2(-50, -66));
        AddText(sub, "Pick a routine to run, or design a new one.", sansFont, 13,
                UITheme.TextMuted, TextAlignmentOptions.MidlineLeft);

        var count = FindDeep(homeScreen, "ConfigCountText").GetComponent<TMP_Text>();
        count.font = monoFont;
        count.fontSize = 11;
        count.color = UITheme.TextMuted;
        count.characterSpacing = 3;
        count.alignment = TextAlignmentOptions.MidlineRight;
        var countRt = (RectTransform)count.transform;
        countRt.anchoredPosition = new Vector2(-40, -48);
        countRt.sizeDelta = new Vector2(220, 24);

        // Watermark gone
        var watermark = FindDeep(homeScreen, "ExistingConfigurationsLabel");
        if (watermark != null) Object.DestroyImmediate(watermark.gameObject);

        BuildFilterBar();

        // Scroll area + grid
        var scroll = FindDeep(homeScreen, "HomeScrollview");
        var scrollRt = (RectTransform)scroll;
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(40, 74);   // above the status bar
        scrollRt.offsetMax = new Vector2(-40, -152);
        scroll.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var homeViewport = FindDeep(scroll, "Viewport");
        homeViewport.GetComponent<Image>().color = Color.white;   // mask shape must stay opaque
        var homeMask = homeViewport.GetComponent<Mask>();
        if (homeMask != null) homeMask.showMaskGraphic = false;

        var homeScrollbar = FindDeep(scroll, "Scrollbar Vertical");
        if (homeScrollbar != null)
        {
            homeScrollbar.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var sbHandle = FindDeep(homeScrollbar, "Handle");
            if (sbHandle != null) sbHandle.GetComponent<Image>().color = UITheme.WithAlpha(UITheme.TextMuted, 0.35f);
            ((RectTransform)homeScrollbar).sizeDelta = new Vector2(6, ((RectTransform)homeScrollbar).sizeDelta.y);
        }

        var grid = FindDeep(scroll, "SavedSequenceCardTemplate");
        var gridRt = (RectTransform)grid;
        gridRt.anchorMin = new Vector2(0, 1);
        gridRt.anchorMax = new Vector2(1, 1);
        gridRt.pivot = new Vector2(0.5f, 1);
        gridRt.anchoredPosition = new Vector2(0, -6);
        gridRt.sizeDelta = new Vector2(-10, 880);
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(250, 270);
        layout.spacing = new Vector2(18, 18);
        layout.padding = new RectOffset(2, 2, 2, 2);

        BuildNewGestureTile(grid);
        BuildStatusBar();
    }

    static void BuildFilterBar()
    {
        Kill(homeScreen, "FilterBar");
        var bar = NewUI("FilterBar", homeScreen);
        StretchTop(bar, new Vector2(50, -140), new Vector2(-40, -106));

        MakeChip(bar, "AllChip", "All 6", 0, 64, active: true);
        MakeChip(bar, "RecentChip", "Recent", 72, 76, active: false);
        MakeChip(bar, "IdleChip", "Idle", 156, 56, active: false);

        // Search button (pushed right)
        var search = NewUI("SearchButton", bar.transform);
        Point(search, new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(92, 32), pivot: new Vector2(1, 0.5f));
        var searchBg = AddImg(search, round6Fill, UITheme.Panel, Image.Type.Sliced, raycast: true);
        search.AddComponent<Button>().targetGraphic = searchBg;
        var sBorder = NewUI("Border", search.transform);
        Stretch(sBorder, Vector2.zero, Vector2.zero);
        AddImg(sBorder, round6Stroke, UITheme.Hairline, Image.Type.Sliced);
        var sIcon = NewUI("Icon", search.transform);
        Point(sIcon, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(14, 14), pivot: new Vector2(0, 0.5f));
        AddImg(sIcon, iconSearch, UITheme.TextSecondary, Image.Type.Simple);
        var sLabel = NewUI("Label", search.transform);
        Stretch(sLabel, new Vector2(32, 0), new Vector2(-6, 0));
        AddText(sLabel, "Search", sansFont, 12.5f, UITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);

        // Hidden search input, revealed by the Search button
        var input = NewUI("SearchInput", bar.transform);
        Point(input, new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(180, 32), pivot: new Vector2(1, 0.5f));
        var inputBg = AddImg(input, round6Fill, UITheme.Panel, Image.Type.Sliced, raycast: true);
        var iBorder = NewUI("Border", input.transform);
        Stretch(iBorder, Vector2.zero, Vector2.zero);
        AddImg(iBorder, round6Stroke, UITheme.WithAlpha(UITheme.Accent, 0.5f), Image.Type.Sliced);
        var field = input.AddComponent<TMP_InputField>();
        field.targetGraphic = inputBg;
        var area = NewUI("Text Area", input.transform);
        Stretch(area, new Vector2(10, 5), new Vector2(-10, -5));
        area.AddComponent<RectMask2D>();
        var ph = NewUI("Placeholder", area.transform);
        Stretch(ph, Vector2.zero, Vector2.zero);
        var phText = AddText(ph, "Search gestures…", monoFont, 12, UITheme.TextMuted,
                             TextAlignmentOptions.MidlineLeft);
        var txt = NewUI("Text", area.transform);
        Stretch(txt, Vector2.zero, Vector2.zero);
        var txtText = AddText(txt, "", monoFont, 12, UITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        field.textViewport = (RectTransform)area.transform;
        field.textComponent = txtText;
        field.placeholder = phText;
        field.fontAsset = monoFont;
        input.SetActive(false);
    }

    static void MakeChip(GameObject bar, string name, string label, float x, float width, bool active)
    {
        var chip = NewUI(name, bar.transform);
        Point(chip, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(width, 32), pivot: new Vector2(0, 0.5f));
        var bg = AddImg(chip, round6Fill,
                        active ? UITheme.WithAlpha(UITheme.Accent, 0.28f) : UITheme.Panel,
                        Image.Type.Sliced, raycast: true);
        chip.AddComponent<Button>().targetGraphic = bg;
        var border = NewUI("Border", chip.transform);
        Stretch(border, Vector2.zero, Vector2.zero);
        AddImg(border, round6Stroke,
               active ? UITheme.WithAlpha(UITheme.Accent, 0.7f) : UITheme.Hairline, Image.Type.Sliced);
        var lbl = NewUI("Label", chip.transform);
        Stretch(lbl, Vector2.zero, Vector2.zero);
        AddText(lbl, label, sansFont, 12.5f,
                active ? UITheme.AccentSoft : UITheme.TextSecondary, TextAlignmentOptions.Center);
    }

    static void BuildNewGestureTile(Transform grid)
    {
        var tile = FindDeep(grid, "AddNewConfigButton");
        tile.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); // keep raycast for the Button

        var old = tile.Find("name");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (string n in new[] { "DashedBorder", "PlusCircle", "NewTitle", "NewSub" })
            Kill(tile, n);

        var dashed = NewUI("DashedBorder", tile);
        Stretch(dashed, Vector2.zero, Vector2.zero);
        AddImg(dashed, dashedCardBorder, UITheme.WithAlpha(UITheme.TextMuted, 0.75f), Image.Type.Simple);

        var plus = NewUI("PlusCircle", tile);
        Point(plus, new Vector2(0.5f, 0.5f), new Vector2(0, 44), new Vector2(46, 46));
        AddImg(plus, circle, UITheme.WithAlpha(UITheme.Accent, 0.85f), Image.Type.Simple);
        var plusIcon = NewUI("Icon", plus.transform);
        Stretch(plusIcon, new Vector2(12, 12), new Vector2(-12, -12));
        AddImg(plusIcon, iconPlus, Color.white, Image.Type.Simple);

        var t = NewUI("NewTitle", tile);
        Point(t, new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(220, 24));
        var tt = AddText(t, "New gesture", sansFont, 15, UITheme.TextPrimary, TextAlignmentOptions.Center);
        tt.fontStyle = FontStyles.Bold;

        var s = NewUI("NewSub", tile);
        Point(s, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(200, 40));
        AddText(s, "Sketch a path,\npick a behavior", sansFont, 12, UITheme.TextMuted,
                TextAlignmentOptions.Center);
    }

    static void BuildStatusBar()
    {
        var statusPanel = FindDeep(homeScreen, "RobotstatusPanel");
        statusPanel.SetParent(homeScreen, false);
        var rt = (RectTransform)statusPanel;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 16);
        rt.offsetMin = new Vector2(40, 16);
        rt.offsetMax = new Vector2(-40, 60);
        var img = statusPanel.GetComponent<Image>();
        img.sprite = round6Fill;
        img.type = Image.Type.Sliced;
        img.color = UITheme.Panel;

        Kill(statusPanel, "Border");
        var border = NewUI("Border", statusPanel);
        Stretch(border, Vector2.zero, Vector2.zero);
        AddImg(border, round6Stroke, UITheme.Hairline, Image.Type.Sliced);

        var icon = FindDeep(statusPanel, "RobotIcon");
        if (icon != null) icon.gameObject.SetActive(false);

        Kill(statusPanel, "Dot");
        var dot = NewUI("Dot", statusPanel);
        Point(dot, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(10, 10), pivot: new Vector2(0, 0.5f));
        AddImg(dot, circle, UITheme.FromHex("35C77B"), Image.Type.Simple);

        var nameT = FindDeep(statusPanel, "RobotName").GetComponent<TMP_Text>();
        nameT.text = "Robot connected";
        nameT.font = sansFont;
        nameT.fontSize = 12.5f;
        nameT.fontStyle = FontStyles.Normal;
        nameT.color = UITheme.TextSecondary;
        nameT.alignment = TextAlignmentOptions.MidlineLeft;
        var nameRt = (RectTransform)nameT.transform;
        nameRt.anchorMin = new Vector2(0, 0.5f);
        nameRt.anchorMax = new Vector2(0, 0.5f);
        nameRt.pivot = new Vector2(0, 0.5f);
        nameRt.anchoredPosition = new Vector2(34, 0);
        nameRt.sizeDelta = new Vector2(240, 24);

        Kill(statusPanel, "StatusRight");
        var right = NewUI("StatusRight", statusPanel);
        Point(right, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(320, 24), pivot: new Vector2(1, 0.5f));
        var rightT = AddText(right, "BATTERY 84% · SIGNAL -47DBM", monoFont, 11,
                             UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
        rightT.characterSpacing = 2;
    }

    // ───────────────────────────── Config ─────────────────────────────

    const float LeftPanelW = 330;
    const float CrumbH = 56;
    const float ToolbarH = 46;

    static void BuildConfig()
    {
        // Re-run safety: restore reparented originals before deleting generated containers
        Evacuate("BreadcrumbBar", new[] { "BackButton" }, configScene);
        Evacuate("ConfigLeftPanel", new[]
        {
            "GestureStepDropdown", "AddstepButton", "ChooseWaypointsButton",
            "ResetButton", "squencePanel", "PlayButton", "SaveButton"
        }, configScene);
        Evacuate("ViewportHUD", new[] { "Click Floor to add waypoints" }, dogPanel);
        Evacuate("Toolbar", new[] { "ClearButton", "WalkButton", "SaveButton", "SaveButton (1)" }, dogPanel);
        foreach (string n in new[] { "BreadcrumbBar", "ConfigLeftPanel", "ViewportHUD", "Toolbar" })
            Kill(configScene, n);

        // Full-bleed container (transparent — the live 3D scene shows through)
        var rt = (RectTransform)configScene;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var rootImg = configScene.GetComponent<Image>();
        rootImg.color = new Color(0, 0, 0, 0);
        rootImg.raycastTarget = false;

        var oldTitle = FindDeep(configScene, "configTitle");
        if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);

        BuildBreadcrumb();
        BuildLeftPanel();
        BuildToolbar();
        BuildViewportHud();
    }

    static void BuildBreadcrumb()
    {
        Kill(configScene, "BreadcrumbBar");
        var bar = NewUI("BreadcrumbBar", configScene);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(0, -CrumbH);
        rt.offsetMax = new Vector2(0, 0);
        AddImg(bar, null, UITheme.FromHex("171A26"), Image.Type.Simple, raycast: true);
        var hairline = NewUI("Hairline", bar.transform);
        var hrt = (RectTransform)hairline.transform;
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = new Vector2(0, 1);
        AddImg(hairline, null, UITheme.Hairline, Image.Type.Simple);

        // Back button becomes the "RobotDog › Gestures" breadcrumb prefix
        var back = FindDeep(canvasT, "BackButton");
        back.SetParent(bar.transform, false);
        var brt = (RectTransform)back;
        brt.anchorMin = new Vector2(0, 0.5f);
        brt.anchorMax = new Vector2(0, 0.5f);
        brt.pivot = new Vector2(0, 0.5f);
        brt.anchoredPosition = new Vector2(20, 0);
        brt.sizeDelta = new Vector2(148, 30);
        var backImg = back.GetComponent<Image>();
        backImg.color = new Color(1, 1, 1, 0.001f);
        var backText = back.GetComponentInChildren<TMP_Text>(true);
        backText.text = "RobotDog › Gestures";
        backText.fontSize = 13;
        backText.color = UITheme.TextMuted;
        backText.alignment = TextAlignmentOptions.MidlineLeft;

        var sep = NewUI("Sep", bar.transform);
        Point(sep, new Vector2(0, 0.5f), new Vector2(166, 0), new Vector2(14, 24), pivot: new Vector2(0, 0.5f));
        AddText(sep, "›", sansFont, 13, UITheme.TextMuted, TextAlignmentOptions.Center);

        var nameGo = NewUI("BreadcrumbName", bar.transform);
        Point(nameGo, new Vector2(0, 0.5f), new Vector2(184, 0), new Vector2(320, 26), pivot: new Vector2(0, 0.5f));
        var nameText = AddText(nameGo, "New gesture", sansFont, 13.5f, UITheme.TextPrimary,
                               TextAlignmentOptions.MidlineLeft);
        nameText.fontStyle = FontStyles.Bold;
        ui.breadcrumbNameText = nameText;

        // Amber "unsaved" chip, right-aligned
        var chip = NewUI("UnsavedChip", bar.transform);
        Point(chip, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(78, 22), pivot: new Vector2(1, 0.5f));
        AddImg(chip, round6Fill, UITheme.WithAlpha(UITheme.Warning, 0.28f), Image.Type.Sliced);
        var chipBorder = NewUI("Border", chip.transform);
        Stretch(chipBorder, Vector2.zero, Vector2.zero);
        AddImg(chipBorder, round6Stroke, UITheme.WithAlpha(UITheme.Warning, 0.6f), Image.Type.Sliced);
        var chipLbl = NewUI("Label", chip.transform);
        Stretch(chipLbl, Vector2.zero, Vector2.zero);
        var chipText = AddText(chipLbl, "unsaved", monoFont, 10.5f, UITheme.FromHex("E8B36A"),
                               TextAlignmentOptions.Center);
        chipText.characterSpacing = 1.5f;
        chip.SetActive(false);
        ui.unsavedChip = chip;
    }

    static void BuildLeftPanel()
    {
        Kill(configScene, "ConfigLeftPanel");
        var panel = NewUI("ConfigLeftPanel", configScene);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 0.5f);
        rt.offsetMin = new Vector2(0, 0);
        rt.offsetMax = new Vector2(LeftPanelW, -CrumbH);
        AddImg(panel, null, UITheme.Panel, Image.Type.Simple, raycast: true);
        var edge = NewUI("Edge", panel.transform);
        var ert = (RectTransform)edge.transform;
        ert.anchorMin = new Vector2(1, 0);
        ert.anchorMax = new Vector2(1, 1);
        ert.offsetMin = new Vector2(-1, 0);
        ert.offsetMax = Vector2.zero;
        AddImg(edge, null, UITheme.Hairline, Image.Type.Simple);

        var pt = panel.transform;

        // Section: gesture type
        MonoLabel(pt, "GestureTypeLabel", "GESTURE TYPE", new Vector2(20, -18));
        var dropdown = FindDeep(configScene, "GestureStepDropdown");
        dropdown.SetParent(pt, false);
        PlaceTop(dropdown, new Vector2(20, -40), new Vector2(290, 38));
        StyleDropdown(dropdown.GetComponent<TMP_Dropdown>());

        // Step actions row
        var addStep = FindDeep(configScene, "AddstepButton");
        addStep.SetParent(pt, false);
        PlaceTop(addStep, new Vector2(20, -90), new Vector2(132, 30));
        StyleSmallButton(addStep, "+ Add gesture");

        var addWps = FindDeep(configScene, "ChooseWaypointsButton");
        addWps.SetParent(pt, false);
        PlaceTop(addWps, new Vector2(160, -90), new Vector2(116, 30));
        StyleSmallButton(addWps, "+ Waypoints");

        var reset = FindDeep(configScene, "ResetButton");
        reset.SetParent(pt, false);
        PlaceTop(reset, new Vector2(284, -90), new Vector2(26, 30));
        var resetImg = reset.GetComponent<Image>();
        resetImg.sprite = round6Fill;
        resetImg.type = Image.Type.Sliced;
        resetImg.color = UITheme.Card;
        var resetIcon = reset.Find("Image");
        if (resetIcon != null)
        {
            resetIcon.GetComponent<Image>().color = Color.white;
            ((RectTransform)resetIcon).sizeDelta = new Vector2(14, 14);
        }

        // Steps list
        MonoLabel(pt, "StepsLabel", "STEPS", new Vector2(20, -134));
        var steps = FindDeep(configScene, "squencePanel");
        steps.SetParent(pt, false);
        var fitter = steps.GetComponent<ContentSizeFitter>();
        if (fitter != null) Object.DestroyImmediate(fitter);
        var vl = steps.GetComponent<VerticalLayoutGroup>();
        if (vl != null) Object.DestroyImmediate(vl);
        var srt = (RectTransform)steps;
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.offsetMin = new Vector2(20, -296);
        srt.offsetMax = new Vector2(-20, -152);
        steps.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        var stepsScroll = steps.Find("Scroll View");
        if (stepsScroll != null)
        {
            var ssrt = (RectTransform)stepsScroll;
            ssrt.anchorMin = Vector2.zero;
            ssrt.anchorMax = Vector2.one;
            ssrt.offsetMin = Vector2.zero;
            ssrt.offsetMax = Vector2.zero;
            var sb = FindDeep(stepsScroll, "Scrollbar Vertical");
            if (sb != null)
            {
                sb.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
                var handle = FindDeep(sb, "Handle");
                if (handle != null) handle.GetComponent<Image>().color = UITheme.WithAlpha(UITheme.TextMuted, 0.4f);
                ((RectTransform)sb).sizeDelta = new Vector2(4, ((RectTransform)sb).sizeDelta.y);
            }
        }
        var stepTemplate = FindDeep(steps, "StepItemTemplate");
        if (stepTemplate != null)
        {
            var stImg = stepTemplate.GetComponent<Image>();
            stImg.sprite = round6Fill;
            stImg.type = Image.Type.Sliced;
            stImg.color = UITheme.WithAlpha(UITheme.Card, 0.55f);
            ((RectTransform)stepTemplate).sizeDelta = new Vector2(250, 32);
            var stText = stepTemplate.GetComponentInChildren<TMP_Text>(true);
            stText.fontSize = 12;
            stText.color = UITheme.TextSecondary;
            stText.alignment = TextAlignmentOptions.MidlineLeft;
            var strt = (RectTransform)stText.transform;
            strt.anchorMin = Vector2.zero;
            strt.anchorMax = Vector2.one;
            strt.offsetMin = new Vector2(10, 0);
            strt.offsetMax = new Vector2(-4, 0);
            strt.anchoredPosition = Vector2.zero;
        }

        // Waypoint list
        var wpHeader = MonoLabel(pt, "WaypointHeader", "WAYPOINTS · 0", new Vector2(20, -318));
        BuildWaypointList(pt);

        // Preview + Save
        var play = FindDeep(configScene, "PlayButton");
        play.SetParent(pt, false);
        PlaceBottom(play, new Vector2(20, 62), new Vector2(290, 38));
        StyleBigButton(play, "Preview", iconPlay, UITheme.TextPrimary, UITheme.Card, UITheme.Hairline);

        var save = FindDeep(configScene, "SaveButton");
        save.SetParent(pt, false);
        PlaceBottom(save, new Vector2(20, 14), new Vector2(290, 40));
        StyleBigButton(save, "Save gesture", iconSave, UITheme.FromHex("4CC79A"),
                       UITheme.WithAlpha(UITheme.Accent, 0.16f), UITheme.WithAlpha(UITheme.Accent, 0.65f));

        // keep the count text reference for the overlay controller
        wpHeaderText = wpHeader;
    }

    static TMP_Text wpHeaderText;
    static Transform wpListContent;

    static void BuildWaypointList(Transform parent)
    {
        Kill(parent, "WaypointList");
        var list = NewUI("WaypointList", parent);
        var rt = (RectTransform)list.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(20, 112);
        rt.offsetMax = new Vector2(-20, -340);
        var scroll = list.AddComponent<ScrollRect>();

        var viewport = NewUI("Viewport", list.transform);
        Stretch(viewport, Vector2.zero, Vector2.zero);
        viewport.AddComponent<RectMask2D>();

        var content = NewUI("Content", viewport.transform);
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.offsetMin = new Vector2(0, -40);
        crt.offsetMax = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = crt;
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20;

        wpListContent = content.transform;
    }

    static void BuildToolbar()
    {
        Kill(configScene, "Toolbar");
        var bar = NewUI("Toolbar", configScene);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(LeftPanelW + 16, 12);
        rt.offsetMax = new Vector2(-16, 12 + ToolbarH);
        var hl = bar.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleRight;
        hl.spacing = 10;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childControlWidth = false;
        hl.childControlHeight = false;

        // Identify the four floating buttons by their labels and reparent them in order
        MoveToToolbar(bar.transform, "ADD POINTS", "Add points", iconTarget, false, 128);
        MoveToToolbar(bar.transform, "SAVE", "Save", iconSave, false, 96);
        MoveToToolbar(bar.transform, "WALK", "Walk path", iconWalk, false, 120);
        MoveToToolbar(bar.transform, "CLEAR", "Clear", iconTrash, true, 96);
    }

    static void MoveToToolbar(Transform bar, string currentLabel, string newLabel,
                              Sprite icon, bool danger, float width)
    {
        Transform found = null;
        foreach (var btn in dogPanel.GetComponentsInChildren<Button>(true))
        {
            var t = btn.GetComponentInChildren<TMP_Text>(true);
            if (t != null && (t.text.Trim().ToUpperInvariant() == currentLabel || t.text == newLabel))
            {
                found = btn.transform;
                break;
            }
        }
        if (found == null)
        {
            // Already moved on a previous run?
            foreach (var btn in bar.GetComponentsInChildren<Button>(true))
            {
                var t = btn.GetComponentInChildren<TMP_Text>(true);
                if (t != null && (t.text == newLabel || t.text.Trim().ToUpperInvariant() == currentLabel))
                {
                    found = btn.transform;
                    break;
                }
            }
        }
        if (found == null)
        {
            Debug.LogWarning("[RedesignBuilder] Toolbar button not found: " + currentLabel);
            return;
        }

        found.SetParent(bar, false);
        found.SetAsLastSibling();
        var rt = (RectTransform)found;
        rt.sizeDelta = new Vector2(width, 38);
        var le = GetOrAdd<LayoutElement>(found.gameObject);
        le.preferredWidth = width;
        le.preferredHeight = 38;

        var img = found.GetComponent<Image>();
        img.sprite = round6Fill;
        img.type = Image.Type.Sliced;
        img.color = UITheme.Panel;

        Kill(found, "Border");
        var border = NewUI("Border", found);
        Stretch(border, Vector2.zero, Vector2.zero);
        AddImg(border, round6Stroke,
               danger ? UITheme.WithAlpha(UITheme.Danger, 0.55f) : UITheme.Hairline, Image.Type.Sliced);

        Kill(found, "ToolIcon");
        var iconGo = NewUI("ToolIcon", found);
        Point(iconGo, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(15, 15), pivot: new Vector2(0, 0.5f));
        AddImg(iconGo, icon, danger ? UITheme.Danger : UITheme.TextSecondary, Image.Type.Simple);

        var label = found.GetComponentInChildren<TMP_Text>(true);
        label.text = newLabel;
        label.fontSize = 12.5f;
        label.fontStyle = FontStyles.Normal;
        label.color = danger ? UITheme.Danger : UITheme.TextPrimary;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(34, 0);
        lrt.offsetMax = new Vector2(-6, 0);
    }

    static void BuildViewportHud()
    {
        Kill(configScene, "ViewportHUD");
        var hud = NewUI("ViewportHUD", configScene);
        var rt = (RectTransform)hud.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(LeftPanelW + 16, 12 + ToolbarH + 10);
        rt.offsetMax = new Vector2(-16, -(CrumbH + 12));

        // 4 corner tick marks (base sprite is the top-left L)
        MakeTick(hud, "TickTL", new Vector2(0, 1), new Vector2(0, 0), 0);
        MakeTick(hud, "TickTR", new Vector2(1, 1), new Vector2(0, 0), -90);
        MakeTick(hud, "TickBR", new Vector2(1, 0), new Vector2(0, 0), 180);
        MakeTick(hud, "TickBL", new Vector2(0, 0), new Vector2(0, 0), 90);

        // Live status text (MultiGoalManager.statusText) becomes the mono hint top-left
        var status = FindDeep(dogPanel, "Click Floor to add waypoints");
        if (status != null)
        {
            status.SetParent(hud.transform, false);
            var st = status.GetComponent<TMP_Text>();
            st.font = monoFont;
            st.fontSize = 10.5f;
            st.color = UITheme.TextMuted;
            st.characterSpacing = 3;
            st.fontStyle = FontStyles.UpperCase;
            st.alignment = TextAlignmentOptions.MidlineLeft;
            st.raycastTarget = false;
            var srt = (RectTransform)status;
            srt.anchorMin = new Vector2(0, 1);
            srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(0, 1);
            srt.offsetMin = new Vector2(34, -54);
            srt.offsetMax = new Vector2(-34, -30);
        }

        // Restyle the speed picker popup so it matches the theme
        var speedPanel = FindDeep(dogPanel, "SpeedPanel");
        if (speedPanel != null)
        {
            var spImg = speedPanel.GetComponent<Image>();
            spImg.sprite = round12Fill;
            spImg.type = Image.Type.Sliced;
            spImg.color = UITheme.WithAlpha(UITheme.Panel, 0.97f);
        }
    }

    static void MakeTick(GameObject hud, string name, Vector2 anchor, Vector2 pos, float rotZ)
    {
        var tick = NewUI(name, hud.transform);
        Point(tick, anchor, pos, new Vector2(22, 22), pivot: anchor);
        tick.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
        AddImg(tick, cornerTick, UITheme.WithAlpha(UITheme.TextMuted, 0.9f), Image.Type.Simple);
    }

    // ───────────────────────────── Systems & wiring ─────────────────────────────

    static void AddSystems()
    {
        // Thumbnail capture rig (root object, always present; camera stays disabled)
        var thumbGo = GameObject.Find("ThumbnailCapture");
        if (thumbGo == null) thumbGo = new GameObject("ThumbnailCapture");
        var thumbCap = GetOrAdd<ThumbnailCapture>(thumbGo);
        var camT = thumbGo.transform.Find("ThumbCamera");
        GameObject camGo = camT != null ? camT.gameObject : new GameObject("ThumbCamera");
        camGo.transform.SetParent(thumbGo.transform, false);
        var cam = GetOrAdd<Camera>(camGo);
        cam.enabled = false;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UITheme.FromHex("262A3A");
        cam.cullingMask = ~(1 << 5);   // everything except UI
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        thumbCap.captureCamera = cam;

        // Waypoint overlay controller under the config screen
        var overlayT = FindDeep(configScene, "WaypointOverlay");
        GameObject overlayGo = overlayT != null ? overlayT.gameObject : NewUI("WaypointOverlay", configScene);
        var ctl = GetOrAdd<WaypointOverlayController>(overlayGo);
        ctl.markerPrefab = markerPrefab;
        ctl.dashedLineMaterial = dashMat;
        ctl.listParent = wpListContent;
        ctl.listItemPrefab = listItemPrefab;
        ctl.waypointCountText = wpHeaderText;
        ctl.gestureUI = ui;
        EditorUtility.SetDirty(ctl);
    }

    static void WireUI()
    {
        ui.gestureCardPrefab = cardPrefab;
        ui.homeCountText = FindDeep(homeScreen, "ConfigCountText").GetComponent<TMP_Text>();

        var bar = FindDeep(homeScreen, "FilterBar");
        ui.filterAllButton = FindDeep(bar, "AllChip").GetComponent<Button>();
        ui.filterAllText = FindDeep(bar, "AllChip").GetComponentInChildren<TMP_Text>(true);
        ui.filterRecentButton = FindDeep(bar, "RecentChip").GetComponent<Button>();
        ui.filterIdleButton = FindDeep(bar, "IdleChip").GetComponent<Button>();
        ui.searchButton = FindDeep(bar, "SearchButton").GetComponent<Button>();
        ui.searchInput = FindDeep(bar, "SearchInput").GetComponent<TMP_InputField>(); // inactive → FindDeep handles it

        EditorUtility.SetDirty(ui);
    }

    // ═════════════════════════════ UI helpers ═════════════════════════════

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    /// <summary>Anchored to the top edge, stretching horizontally. offMin=(left, -bottomFromTop) offMax=(right, -topFromTop)</summary>
    static void StretchTop(GameObject go, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    static void StretchBottom(GameObject go, Vector2 offMin, Vector2 offMax)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    static void Point(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void PlaceTop(Transform t, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void PlaceBottom(Transform t, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Image AddImg(GameObject go, Sprite sprite, Color color, Image.Type type, bool raycast = false)
    {
        var img = GetOrAdd<Image>(go);
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = raycast;
        return img;
    }

    static TextMeshProUGUI AddText(GameObject go, string text, TMP_FontAsset font, float size,
                                   Color color, TextAlignmentOptions align)
    {
        var t = GetOrAdd<TextMeshProUGUI>(go);
        t.text = text;
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static TMP_Text MonoLabel(Transform parent, string name, string text, Vector2 pos)
    {
        Kill(parent, name);
        var go = NewUI(name, parent);
        PlaceTop(go.transform, pos, new Vector2(290, 16));
        var t = AddText(go, text, monoFont, 10, UITheme.TextMuted, TextAlignmentOptions.MidlineLeft);
        t.characterSpacing = 5;
        return t;
    }

    static void StyleSmallButton(Transform btn, string label)
    {
        var img = btn.GetComponent<Image>();
        img.sprite = round6Fill;
        img.type = Image.Type.Sliced;
        img.color = UITheme.Card;
        var t = btn.GetComponentInChildren<TMP_Text>(true);
        t.text = label;
        t.fontSize = 12;
        t.color = UITheme.FromHex("4CC79A");
        t.alignment = TextAlignmentOptions.Center;
    }

    static void StyleBigButton(Transform btn, string label, Sprite icon, Color fg, Color bg, Color borderColor)
    {
        var img = btn.GetComponent<Image>();
        img.sprite = round6Fill;
        img.type = Image.Type.Sliced;
        img.color = bg;

        Kill(btn, "Border");
        var border = NewUI("Border", btn);
        Stretch(border, Vector2.zero, Vector2.zero);
        AddImg(border, round6Stroke, borderColor, Image.Type.Sliced);

        Kill(btn, "BtnIcon");
        var iconGo = NewUI("BtnIcon", btn);
        Point(iconGo, new Vector2(0.5f, 0.5f), new Vector2(-46, 0), new Vector2(14, 14));
        AddImg(iconGo, icon, fg, Image.Type.Simple);

        var t = btn.GetComponentInChildren<TMP_Text>(true);
        t.text = label;
        t.fontSize = 13;
        t.color = fg;
        t.alignment = TextAlignmentOptions.Center;
        var trt = (RectTransform)t.transform;
        trt.offsetMin = new Vector2(20, 0);
        trt.offsetMax = new Vector2(0, 0);
    }

    static void StyleDropdown(TMP_Dropdown dd)
    {
        var img = dd.GetComponent<Image>();
        img.sprite = round6Fill;
        img.type = Image.Type.Sliced;
        img.color = UITheme.Card;

        var label = dd.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null) { label.color = UITheme.TextPrimary; label.fontSize = 13.5f; }
        var arrow = dd.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow != null) arrow.color = UITheme.TextSecondary;

        var template = dd.transform.Find("Template");
        if (template != null)
        {
            template.GetComponent<Image>().color = UITheme.Panel;
            var itemBg = FindDeep(template, "Item Background");
            if (itemBg != null) itemBg.GetComponent<Image>().color = UITheme.Card;
            var itemLabel = FindDeep(template, "Item Label");
            if (itemLabel != null)
            {
                var ilt = itemLabel.GetComponent<TMP_Text>();
                ilt.color = UITheme.TextPrimary;
                ilt.fontSize = 13;
            }
            var check = FindDeep(template, "Item Checkmark");
            if (check != null) check.GetComponent<Image>().color = UITheme.Accent;
        }
    }

    static void SetColor(Transform root, string childName, Color c)
    {
        Transform t = childName == null ? root : FindDeep(root, childName);
        if (t != null)
        {
            var img = t.GetComponent<Image>();
            if (img != null) img.color = c;
        }
    }

    static void Kill(Transform parent, string name)
    {
        var t = FindDeep(parent, name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }
    static void Kill(GameObject parent, string name) => Kill(parent.transform, name);

    /// <summary>Moves listed children of a generated container back out before it is rebuilt.</summary>
    static void Evacuate(string containerName, string[] childNames, Transform to)
    {
        var container = FindDeep(configScene, containerName);
        if (container == null) return;
        foreach (string n in childNames)
        {
            var child = FindDeep(container, n);
            if (child != null) child.SetParent(to, false);
        }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// <summary>Recursive find by name (includes inactive children).</summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
    static Transform FindDeep(GameObject root, string name) => FindDeep(root.transform, name);

    // ═════════════════════════════ Painter ═════════════════════════════

    /// <summary>Tiny anti-aliased software rasterizer for generating UI sprites.</summary>
    class Painter
    {
        readonly int w, h;
        readonly float[] a;

        public Painter(int width, int height)
        {
            w = width; h = height;
            a = new float[w * h];
        }

        void Blend(int x, int y, float alpha)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (alpha > a[i]) a[i] = alpha;
        }

        public void StrokeSeg(Vector2 p0, Vector2 p1, float width, float maxAlpha = 1f)
        {
            float half = width * 0.5f;
            int minX = Mathf.FloorToInt(Mathf.Min(p0.x, p1.x) - half - 1);
            int maxX = Mathf.CeilToInt(Mathf.Max(p0.x, p1.x) + half + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(p0.y, p1.y) - half - 1);
            int maxY = Mathf.CeilToInt(Mathf.Max(p0.y, p1.y) + half + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d = DistToSeg(new Vector2(x + 0.5f, y + 0.5f), p0, p1);
                    float alpha = Mathf.Clamp01(half - d + 0.5f) * maxAlpha;
                    if (alpha > 0) Blend(x, y, alpha);
                }
        }

        public void StrokeCircle(Vector2 c, float r, float width)
        {
            float half = width * 0.5f;
            int minX = Mathf.FloorToInt(c.x - r - half - 1), maxX = Mathf.CeilToInt(c.x + r + half + 1);
            int minY = Mathf.FloorToInt(c.y - r - half - 1), maxY = Mathf.CeilToInt(c.y + r + half + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) - r);
                    float alpha = Mathf.Clamp01(half - d + 0.5f);
                    if (alpha > 0) Blend(x, y, alpha);
                }
        }

        public void FillCircle(Vector2 c, float r)
        {
            int minX = Mathf.FloorToInt(c.x - r - 1), maxX = Mathf.CeilToInt(c.x + r + 1);
            int minY = Mathf.FloorToInt(c.y - r - 1), maxY = Mathf.CeilToInt(c.y + r + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) - r;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha > 0) Blend(x, y, alpha);
                }
        }

        public void FillRect(Rect r)
        {
            for (int y = Mathf.FloorToInt(r.yMin); y < Mathf.CeilToInt(r.yMax); y++)
                for (int x = Mathf.FloorToInt(r.xMin); x < Mathf.CeilToInt(r.xMax); x++)
                    Blend(x, y, 1f);
        }

        static float RoundRectSDF(Vector2 p, Rect rect, float rad)
        {
            Vector2 center = rect.center;
            Vector2 half = new Vector2(rect.width * 0.5f - rad, rect.height * 0.5f - rad);
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
            Vector2 qc = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0));
            return qc.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rad;
        }

        public void FillRoundRect(Rect rect, float rad)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = RoundRectSDF(new Vector2(x + 0.5f, y + 0.5f), rect, rad);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha > 0) Blend(x, y, alpha);
                }
        }

        public void StrokeRoundRect(Rect rect, float rad, float width)
        {
            float half = width * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(RoundRectSDF(new Vector2(x + 0.5f, y + 0.5f), rect, rad));
                    float alpha = Mathf.Clamp01(half - d + 0.5f);
                    if (alpha > 0) Blend(x, y, alpha);
                }
        }

        /// <summary>Dashed straight edges + solid corner arcs (used for the New-gesture tile).</summary>
        public void DashedRoundRectBorder(Rect rect, float rad, float width, float dash, float gap)
        {
            // Corner arcs (solid) — approximate with short segments
            Vector2[] centers =
            {
                new Vector2(rect.xMin + rad, rect.yMin + rad),
                new Vector2(rect.xMax - rad, rect.yMin + rad),
                new Vector2(rect.xMax - rad, rect.yMax - rad),
                new Vector2(rect.xMin + rad, rect.yMax - rad),
            };
            float[] startAngles = { 180f, 270f, 0f, 90f };
            foreach (var (c, sa) in Zip(centers, startAngles))
            {
                const int seg = 6;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = (sa + 90f * i / seg) * Mathf.Deg2Rad;
                    float a1 = (sa + 90f * (i + 1) / seg) * Mathf.Deg2Rad;
                    StrokeSeg(c + rad * new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)),
                              c + rad * new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)), width);
                }
            }

            // Dashed edges
            DashedSeg(new Vector2(rect.xMin + rad, rect.yMin), new Vector2(rect.xMax - rad, rect.yMin), width, dash, gap);
            DashedSeg(new Vector2(rect.xMax, rect.yMin + rad), new Vector2(rect.xMax, rect.yMax - rad), width, dash, gap);
            DashedSeg(new Vector2(rect.xMax - rad, rect.yMax), new Vector2(rect.xMin + rad, rect.yMax), width, dash, gap);
            DashedSeg(new Vector2(rect.xMin, rect.yMax - rad), new Vector2(rect.xMin, rect.yMin + rad), width, dash, gap);
        }

        static IEnumerable<(Vector2, float)> Zip(Vector2[] a, float[] b)
        {
            for (int i = 0; i < a.Length; i++) yield return (a[i], b[i]);
        }

        void DashedSeg(Vector2 p0, Vector2 p1, float width, float dash, float gap)
        {
            float len = Vector2.Distance(p0, p1);
            Vector2 dir = (p1 - p0).normalized;
            float t = 0;
            while (t < len)
            {
                float end = Mathf.Min(t + dash, len);
                StrokeSeg(p0 + dir * t, p0 + dir * end, width);
                t = end + gap;
            }
        }

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = Vector2.Dot(ab, ab);
            float t = denom < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
            return Vector2.Distance(p, a + t * ab);
        }

        public byte[] EncodePNG()
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < a.Length; i++)
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a[i]) * 255));
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }
    }
}
