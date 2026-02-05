#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(TextAsset))]
public class TextAssetHook : Editor
{
    public delegate VisualElement RenderFunction(string content, string assetPath);
    
    private static Dictionary<RenderFunction, string[]> s_renderedRoots = new();
    private static List<string> s_extensions = new();

    private ScrollView _scroll;

    private string _assetPath;
    private string _lastRenderedAssetPath;
    private int _lastRenderedTextHash;

    public static void Add(RenderFunction container, params string[] extensions)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));

        s_renderedRoots.Add(container, extensions);
        s_extensions.AddRange(extensions);
    }

    protected void OnEnable() =>
        EditorApplication.update += UpdateRequests;

    protected void OnDisable() =>
        EditorApplication.update -= UpdateRequests;

    private void UpdateRequests() =>
        Repaint();

    public override bool UseDefaultMargins() => false;

    public override VisualElement CreateInspectorGUI()
    {
        _assetPath = AssetDatabase.GetAssetPath(target);
        var ext = Path.GetExtension(_assetPath).ToLowerInvariant();

        if (!s_extensions.Contains(ext))
            return CreateDefaultInspectorContainer();

        var root = new VisualElement();
        root.style.flexGrow = 1;

        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.style.flexGrow = 1;
        root.Add(_scroll);

        // initial build
        RebuildIfNeeded(force: true);

        // Keep it updated when selection stays but content changes.
        root.schedule.Execute(() => RebuildIfNeeded(force: false)).Every(250);

        return root;
    }

    private void RebuildIfNeeded(bool force)
    {
        if (_scroll == null)
            return;

        _assetPath = AssetDatabase.GetAssetPath(target);
        var ext = Path.GetExtension(_assetPath).ToLowerInvariant();

        var ta = target as TextAsset;
        var content = ta != null ? ta.text : string.Empty;

        // Hash instead of full string compare to keep this cheap.
        var textHash = (content ?? string.Empty).GetHashCode();

        if (!force && _lastRenderedAssetPath == _assetPath && _lastRenderedTextHash == textHash)
            return;

        _lastRenderedAssetPath = _assetPath;
        _lastRenderedTextHash = textHash;

        _scroll.Clear();

        try
        {
            foreach (var renderRoot in s_renderedRoots)
                if (renderRoot.Value.ToList().Contains(ext))
                {
                    _scroll.Add(renderRoot.Key.Invoke(content, _assetPath));
                    break;
                }
        }
        catch
        {
            _scroll.Add(CreateDefaultInspectorContainer());
        }
    }

    public override void OnInspectorGUI()
    {
        // IMGUI fallback (shouldn't be used when CreateInspectorGUI is active, but keep for safety).
        DrawDefaultEditor();
    }

    private VisualElement CreateDefaultInspectorContainer()
    {
        var container = new VisualElement();
        var imgui = new IMGUIContainer(DrawDefaultEditor);
        imgui.style.flexGrow = 1;
        container.Add(imgui);
        return container;
    }

    private Editor _defaultEditor;
    private void DrawDefaultEditor()
    {
        if (_defaultEditor == null)
            _defaultEditor = CreateEditor(target, Type.GetType("UnityEditor.TextAssetInspector, UnityEditor"));

        if (_defaultEditor != null)
            _defaultEditor.OnInspectorGUI();
    }
}
#endif