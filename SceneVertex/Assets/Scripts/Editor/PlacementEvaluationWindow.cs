using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class PlacementEvaluationWindow : EditorWindow
{
    private readonly List<string> warnings = new();
    private Texture2D previewTexture;
    private Vector2 scrollPosition;
    private string lastError = string.Empty;

    [MenuItem("Tools/SceneVertex/Placement Evaluation")]
    public static void OpenWindow()
    {
        var window = GetWindow<PlacementEvaluationWindow>("Placement Evaluation");
        window.minSize = new Vector2(720f, 640f);
        window.LoadExistingPreview();
    }

    private void OnEnable()
    {
        minSize = new Vector2(720f, 640f);
        LoadExistingPreview();
    }

    private void OnDisable()
    {
        ReplacePreviewTexture(null);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Placement Evaluation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Reads authored JSON files from Assets/PlacementEvaluation, validates them, and renders a curated 1024x512 top-view PNG preview without touching the scene.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawPathRow("Elements JSON", PlacementEvaluationPaths.ElementsJsonAssetPath);
            DrawPathRow("Layout JSON", PlacementEvaluationPaths.LayoutJsonAssetPath);
            DrawPathRow("Rules MD", PlacementEvaluationPaths.RulesMarkdownAssetPath);
            DrawPathRow("Curation MD", PlacementEvaluationPaths.CurationMarkdownAssetPath);
            DrawPathRow("Preview PNG", PlacementEvaluationPaths.PreviewPngAssetPath);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Preview", GUILayout.Height(28f)))
            {
                RefreshPreview();
            }

            if (GUILayout.Button("Reveal JSON Folder", GUILayout.Height(28f)))
            {
                EditorUtility.RevealInFinder(PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.RootAssetFolder));
            }

            using (new EditorGUI.DisabledScope(!File.Exists(PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.PreviewPngAssetPath))))
            {
                if (GUILayout.Button("Reveal PNG", GUILayout.Height(28f)))
                {
                    EditorUtility.RevealInFinder(PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.PreviewPngAssetPath));
                }
            }
        }

        if (!string.IsNullOrEmpty(lastError))
        {
            EditorGUILayout.HelpBox(lastError, MessageType.Error);
        }

        if (warnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (previewTexture == null)
            {
                EditorGUILayout.HelpBox("No preview loaded. Use Refresh Preview to generate the PNG from the JSON files.", MessageType.None);
            }
            else
            {
                var rect = GUILayoutUtility.GetAspectRect(2f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField("Resolution", $"{previewTexture.width} x {previewTexture.height}");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawPathRow(string label, string assetPath)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            EditorGUILayout.SelectableLabel(assetPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    private void RefreshPreview()
    {
        if (!PlacementEvaluationPreviewUtility.TryRenderAndSavePreview(out var renderedPreview, out var renderWarnings, out var error))
        {
            lastError = error;
            warnings.Clear();
            Repaint();
            return;
        }

        lastError = string.Empty;
        warnings.Clear();
        warnings.AddRange(renderWarnings);
        ReplacePreviewTexture(renderedPreview);
        Repaint();
    }

    private void LoadExistingPreview()
    {
        if (previewTexture != null)
        {
            return;
        }

        if (!PlacementEvaluationPreviewUtility.TryLoadExistingPreview(out var loadedPreview, out _))
        {
            return;
        }

        ReplacePreviewTexture(loadedPreview);
    }

    private void ReplacePreviewTexture(Texture2D nextTexture)
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }

        previewTexture = nextTexture;
    }
}
