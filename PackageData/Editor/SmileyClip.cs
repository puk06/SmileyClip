using SmileyClip.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace SmileyClip
{
    public class SmileyClip : EditorWindow
    {
        private const bool BETA_VERSION_BOOL = false;
        private const string CURRENT_VERSION = "v1.0.1";
        private const string AUTHOR = "ぷこるふ";

        private const string BLENDSHAPE_PREFIX = "blendShape.";
        private static readonly int BLENDSHAPE_PREFIX_LENGTH = BLENDSHAPE_PREFIX.Length;

        private GameObject targetObject;
        private AnimationClip baseAnimation;
        private bool loadAnimation = true;
        private bool fixAnimation = false;
        private bool writeDefaults = true;
        private bool vrcMode = true;
        private bool sameAsRoot = true;
        private VRCAvatarDescriptor avatarDescriptor;

        private SkinnedMeshRenderer[] skinnedMeshRenderers;
        private string[] skinnedMeshRendererNames;
        private int selectedRendererIndex = 0;
        private SkinnedMeshRenderer selectedRenderer;

        private bool isPreviewing = false;
        private bool isRecording = false;

        private readonly Dictionary<int, float> originalBlendShapes = new();
        private AnimationClip recordedClip;

        private int foundBlendShapes = 0;
        private int notFoundBlendShapes = 0;

        private float previewTime = 0f;
        private float animationLength = 0f;

        private GameObject prevTargetObject;
        private AnimationClip prevAnimationClip;

        private static readonly GUIContent TargetObjectLabel = GuiUtils.GenerateCustomLabel("ルートオブジェクト", "アニメーションを作成したいオブジェクトの親（Animatorがアタッチされているもの）を指定してください。このオブジェクトを基準にアニメーションのパスが生成されます。");
        private static readonly GUIContent SkinnedMeshRendererLabel = GuiUtils.GenerateCustomLabel("Skinned Mesh Renderer", "アニメーションを作成したいBlendShapeが入ったSkinned Mesh Rendererオブジェクトを指定してください。");
        private static readonly GUIContent BaseAnimationLabel = GuiUtils.GenerateCustomLabel("アニメーション(任意)", "録画を開始する際に、あらかじめ読み込んで適用しておくアニメーションです。既存のアニメーションを元に新しく作成する場合に使用します。");
        private static readonly GUIContent LoadAnimationLabel = GuiUtils.GenerateCustomLabel("録画時にアニメーションを使用", "録画時にベースアニメーションを読み込むかどうかを選択できます。プレビューのみ確認したい場合はオフにすると便利です。");
        private static readonly GUIContent WriteDefaultsLabel = GuiUtils.GenerateCustomLabel("未変更の値も保存", "未変更の項目もアニメーションに含めるかどうかを選択できます。");
        private static readonly GUIContent VRCModeLabel = GuiUtils.GenerateCustomLabel("VRChatモード", "VRChatの表情改変の時に使用するものです。");
        private static readonly GUIContent SameAsRootLabel = GuiUtils.GenerateCustomLabel("ルートオブジェクトと同じ", "ルートオブジェクト内のVRC Avatar Descriptorを使用します。");
        private static readonly GUIContent AvatarDescriptorLabel = GuiUtils.GenerateCustomLabel("Avatar Descriptor", "違うオブジェクト内のVRC Avatar Descriptorを使用します。");
        
        [MenuItem("Tools/ぷこのつーる/SmileyClip")]
        public static void ShowWindow()
        {
            GetWindow<SmileyClip>("SmileyClip");

            if (BETA_VERSION_BOOL)
            {
                GuiUtils.ShowDialog("このSmileyClipはベータ版です。\nアバターのBlendShapeの意図しない変更を防ぐため、ヒエラルキー上でアバターを複製し、バックアップを取っておくことをおすすめします。");
            }
        }

        private void OnGUI()
        {
            GuiUtils.DrawBigTitle(AUTHOR, CURRENT_VERSION, BETA_VERSION_BOOL);

            #region ルートオブジェクトとRendererの選択
            GuiUtils.DrawSection("ルートオブジェクトとRendererの選択", isFirst: true);
            bool isPlayingAnimation = isRecording || isPreviewing;

            using (new EditorGUI.DisabledScope(isPlayingAnimation))
            {
                targetObject = (GameObject)EditorGUILayout.ObjectField(TargetObjectLabel, targetObject, typeof(GameObject), true);

                if (targetObject != prevTargetObject)
                {
                    prevTargetObject = targetObject;

                    ResetkinnedMeshRenderers();
                    if (targetObject != null)
                    {
                        UpdateSkinnedMeshRenderers();
                    }
                }

                if (targetObject != null)
                {
                    if (skinnedMeshRendererNames != null && skinnedMeshRendererNames.Length > 0)
                    {
                        selectedRendererIndex = EditorGUILayout.Popup(SkinnedMeshRendererLabel, selectedRendererIndex, skinnedMeshRendererNames);
                        selectedRenderer = skinnedMeshRenderers[selectedRendererIndex];

                        if (!SkinnedMeshRendererUtils.IsNull(selectedRenderer))
                        {
                            EditorGUILayout.LabelField("選択されたRenderer", $"{selectedRenderer.name} → BlendShape数: {selectedRenderer.sharedMesh.blendShapeCount}");
                        }
                        else
                        {
                            EditorGUILayout.LabelField("SkinnedMeshRendererが見つかりません。");
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("SkinnedMeshRendererが見つかりません。");
                    }
                }
            }

            if (isPlayingAnimation)
            {
                EditorGUILayout.HelpBox("プレビュー、または録画中にオブジェクト、Rendererを変更することは出来ません。\nスクリプトを再読込するとBlendShapeが戻らずにそのままになるので注意してください。", MessageType.Warning);
            }
            #endregion

            #region ベースアニメーション設定
            GuiUtils.DrawSection("ベースアニメーション設定");
            baseAnimation = (AnimationClip)EditorGUILayout.ObjectField(BaseAnimationLabel, baseAnimation, typeof(AnimationClip), true);
            loadAnimation = EditorGUILayout.Toggle(LoadAnimationLabel, loadAnimation);

            if (isPlayingAnimation)
            {
                if (prevAnimationClip != baseAnimation)
                {
                    prevAnimationClip = baseAnimation;

                    previewTime = 0;
                    fixAnimation = false;

                    if (baseAnimation == null)
                    {
                        RestoreBlendShapeWeight(reset: false);
                    }
                    else
                    {
                        LoadAnimationBrendshapes(previewTime);
                    }
                }

                if (baseAnimation != null)
                {
                    animationLength = baseAnimation.length;
                    if (animationLength > 0)
                    {
                        GuiUtils.DrawSection("アニメーションの再生位置");

                        if (!fixAnimation)
                        {
                            float newPreviewTime = EditorGUILayout.Slider("再生時間", previewTime, 0f, animationLength);
                            if (Mathf.Abs(newPreviewTime - previewTime) > 0.001f)
                            {
                                previewTime = newPreviewTime;
                                LoadAnimationBrendshapes(previewTime);
                            }

                            EditorGUILayout.HelpBox("アニメーションの再生位置を変更すると、既に変更されていたBlendShapeも上書きされてしまいます。\n先にアニメーションを下のチェックボックスから固定してから変更を行うことをおすすめします。", MessageType.Warning);
                        }

                        fixAnimation = EditorGUILayout.Toggle("アニメーションを固定", fixAnimation);
                    }
                }
            }
            else
            {
                fixAnimation = false;
            }
            #endregion

            #region アニメーション設定
            GuiUtils.DrawSection("アニメーション設定");
            writeDefaults = EditorGUILayout.Toggle(WriteDefaultsLabel, writeDefaults);
            vrcMode = EditorGUILayout.Toggle(VRCModeLabel, vrcMode);
            #endregion

            #region VRC Avatar Descriptor
            if (vrcMode)
            {
                GuiUtils.DrawSection("VRC Avatar Descriptor");
                sameAsRoot = EditorGUILayout.Toggle(SameAsRootLabel, sameAsRoot);

                if (!sameAsRoot)
                {
                    avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(AvatarDescriptorLabel, avatarDescriptor, typeof(VRCAvatarDescriptor), true);
                }
            }
            #endregion

            #region 操作
            GuiUtils.DrawSection("操作");
            bool isValid = targetObject != null && !SkinnedMeshRendererUtils.IsNull(selectedRenderer);

            bool canStartPreview = !isPlayingAnimation && isValid && baseAnimation != null;
            bool canEndPreview = isPreviewing;

            bool canStartRecording = !isPlayingAnimation && isValid;
            bool canEndRecording = isRecording;

            if (canStartPreview || canEndPreview)
            {
                if (GUILayout.Button(isPreviewing ? "ベースアニメーションのプレビューを終了" : "ベースアニメーションのプレビューを開始"))
                {
                    if (!isPreviewing)
                    {
                        isPreviewing = SaveAndPlayAnimation(true);
                    }
                    else
                    {
                        RestoreBlendShapeWeight();
                    }
                }
            }

            if (canStartRecording || canEndRecording)
            {
                if (GUILayout.Button(isRecording ? "録画を終了し、保存する" : "録画を開始する"))
                {
                    if (!isRecording)
                    {
                        isRecording = SaveAndPlayAnimation(loadAnimation);
                    }
                    else
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "確認",
                            "現在のBlendShapeを保存して終了します。\nよろしいですか？",
                            "保存する",
                            "キャンセル"
                        );

                        if (confirm)
                        {
                            StopRecordingAndSave();
                        }
                    }
                }
            }

            if (isPlayingAnimation && baseAnimation != null)
            {
                if (GUILayout.Button("アニメーションを再度読み込む"))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "確認",
                        "現在のBlendShapeの変更を無視し、現在の再生位置のベースアニメーションを再度読み込みます。\nよろしいですか？",
                        "再度読み込む",
                        "キャンセル"
                    );

                    if (confirm)
                    {
                        LoadAnimationBrendshapes(previewTime);
                    }
                }
            }

            if (!(canStartRecording || canEndRecording) && !(canStartPreview || canEndPreview))
            {
                EditorGUILayout.HelpBox($"操作可能な項目がありません。", MessageType.Info);
            }

            if (isRecording)
            {
                if (GUILayout.Button("録画をキャンセル"))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "確認",
                        "現在のBlendShape変更内容は無視され削除されます。\n本当にキャンセルしますか？",
                        "キャンセルする",
                        "戻る"
                    );

                    if (confirm)
                    {
                        RestoreBlendShapeWeight();
                        isRecording = false;
                    }
                }
            }

            if (isPlayingAnimation && originalBlendShapes.Keys.Count() != 0 && !SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                int changedCount = 0;
                for (int i = 0; i < selectedRenderer.sharedMesh.blendShapeCount; i++)
                {
                    if (originalBlendShapes[i] != selectedRenderer.GetBlendShapeWeight(i)) changedCount++;
                }

                EditorGUILayout.HelpBox($"変更済みのBlendShape数: {changedCount}", MessageType.Info);

                if (notFoundBlendShapes > 0)
                {
                    var missingRate = (float)notFoundBlendShapes / (foundBlendShapes + notFoundBlendShapes);
                    EditorGUILayout.HelpBox($"アニメーション内の{Mathf.Round(missingRate * 100)}% ({notFoundBlendShapes}個) のBlendShapeが適用されませんでした。\nこの値が大きすぎる場合、対象のRendererが違う可能性があります。", MessageType.Error);
                }
            }
            #endregion

            #region リセット
            GuiUtils.DrawSection("リセット");
            using (new EditorGUI.DisabledScope(isPlayingAnimation))
            {
                if (GUILayout.Button("オブジェクトのリセット"))
                {
                    targetObject = null;
                }

                if (GUILayout.Button("ベースアニメーションのリセット"))
                {
                    baseAnimation = null;
                }
            }

            using (new EditorGUI.DisabledScope(SkinnedMeshRendererUtils.IsNull(selectedRenderer)))
            {
                if (GUILayout.Button("全てのBlendShapeを0にリセット"))
                {
                    SkinnedMeshRendererUtils.SetAllBlendShapesToDefault(selectedRenderer);
                }
            }
            #endregion
        }

        private void OnDestroy()
        {
            if (isRecording || isPreviewing)
            {
                RestoreBlendShapeWeight();
            }
        }

        private void SaveOriginalBlendShapes()
        {
            originalBlendShapes.Clear();
            if (SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                GuiUtils.ShowDialog("Rendererが見つからなかったため、BlendShapeの保存に失敗しました。");
            } 
            else
            {
                for (int i = 0; i < selectedRenderer.sharedMesh.blendShapeCount; i++)
                {
                    originalBlendShapes[i] = selectedRenderer.GetBlendShapeWeight(i);
                }
            }
        }

        private void RestoreBlendShapeWeight(bool reset = true)
        {
            if (SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                GuiUtils.ShowDialog("Rendererが見つからなかったため、BlendShapeの復元に失敗しました。");
            }
            else
            {
                foreach (var originalBlendShape in originalBlendShapes)
                {
                    selectedRenderer.SetBlendShapeWeight(originalBlendShape.Key, originalBlendShape.Value);
                }
            }

            if (reset)
            {
                isPreviewing = false;
                isRecording = false;

                originalBlendShapes.Clear();
            }
        }

        private void LoadAnimationBrendshapes(float time)
        {
            foundBlendShapes = 0;
            notFoundBlendShapes = 0;

            if (SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                GuiUtils.ShowDialog("Rendererが見つからなかったため、アニメーションの読み込みに失敗しました。");
                return;
            }

            RestoreBlendShapeWeight(reset: false);

            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(baseAnimation);

            foreach (var binding in curveBindings)
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;

                if (!binding.propertyName.StartsWith(BLENDSHAPE_PREFIX)) continue;

                string blendShapeName = binding.propertyName[BLENDSHAPE_PREFIX_LENGTH..];
                int index = selectedRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);

                if (index >= 0)
                {
                    var curve = AnimationUtility.GetEditorCurve(baseAnimation, binding);
                    if (curve != null && curve.length > 0)
                    {
                        float value = curve.Evaluate(time);
                        selectedRenderer.SetBlendShapeWeight(index, value);
                    }

                    foundBlendShapes++;
                }
                else
                {
                    notFoundBlendShapes++;
                }
            }
        }

        private bool SaveAndPlayAnimation(bool loadBaseAnimation)
        {
            if (SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                GuiUtils.ShowDialog("Rendererが見つからなかったため、操作の開始に失敗しました。");
                return false;
            }

            SaveOriginalBlendShapes();

            if (baseAnimation != null && loadBaseAnimation)
            {
                LoadAnimationBrendshapes(previewTime);
            }

            return true;
        }

        private void StopRecordingAndSave()
        {
            if (targetObject == null || SkinnedMeshRendererUtils.IsNull(selectedRenderer))
            {
                GuiUtils.ShowDialog("オブジェクトが見つからなかったため、BlendShapeの保存に失敗しました。");
            }
            else
            {
                recordedClip = new AnimationClip();
                string relativePath = AnimationUtility.CalculateTransformPath(selectedRenderer.transform, targetObject.transform);

                string[] visemeBlendShapes = null;

                if (vrcMode)
                {
                    var avatarDescriptorObject = sameAsRoot ? VRChatUtils.GetAvatarDescriptor(targetObject) : avatarDescriptor;
                    if (avatarDescriptorObject != null)
                    { 
                        visemeBlendShapes = VRChatUtils.GetAllVisemeBlendShapes(avatarDescriptorObject);
                    }
                }

                for (int i = 0; i < selectedRenderer.sharedMesh.blendShapeCount; i++)
                {
                    var blendShapeName = selectedRenderer.sharedMesh.GetBlendShapeName(i);
                    if (visemeBlendShapes != null && visemeBlendShapes.Contains(blendShapeName)) continue;

                    float weight = selectedRenderer.GetBlendShapeWeight(i);
                    if (!writeDefaults && originalBlendShapes[i] == weight) continue;

                    string propertyName = BLENDSHAPE_PREFIX + blendShapeName;

                    AnimationCurve curve = new();
                    curve.AddKey(0f, weight);

                    recordedClip.SetCurve(relativePath, typeof(SkinnedMeshRenderer), propertyName, curve);
                }

                string fileName = baseAnimation != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(baseAnimation)) + "_new" : "NewAnimationFile";
                string initialPath = baseAnimation != null ? new DirectoryInfo(AssetDatabase.GetAssetPath(baseAnimation)).Parent.FullName : "Assets";

                string path = EditorUtility.SaveFilePanelInProject("アニメーションファイルを保存する", fileName, "anim", "保存する", initialPath);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(recordedClip, path);
                    AssetDatabase.SaveAssets();

                    GuiUtils.ShowDialog($"アニメーションの保存が完了しました。\n{path}");

                    bool confirm = EditorUtility.DisplayDialog(
                        "確認",
                        "アニメーションの保存先をAssets内で開きますか？",
                        "はい",
                        "いいえ"
                    );

                    if (confirm)
                    {
                        UnityUtils.SelectAssetAtPath(path);
                    }
                }
            }

            recordedClip = null;
            RestoreBlendShapeWeight();
        }

        private void ResetkinnedMeshRenderers()
        {
            skinnedMeshRenderers = null;
            skinnedMeshRendererNames = null;
            selectedRendererIndex = 0;
            selectedRenderer = null;
        }

        private void UpdateSkinnedMeshRenderers()
        {
            skinnedMeshRenderers = SkinnedMeshRendererUtils.GetAllSkinnedMeshRenderers(targetObject);
            skinnedMeshRendererNames = skinnedMeshRenderers.Select(smr => smr.name).ToArray();
            selectedRendererIndex = SkinnedMeshRendererUtils.GetRendererWithMostBlendShapes(skinnedMeshRenderers);
        }
    }
}