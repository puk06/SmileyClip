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
        private const string AUTHOR = "�Ղ����";

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

        private static readonly GUIContent TargetObjectLabel = GuiUtils.GenerateCustomLabel("���[�g�I�u�W�F�N�g", "�A�j���[�V�������쐬�������I�u�W�F�N�g�̐e�iAnimator���A�^�b�`����Ă�����́j���w�肵�Ă��������B���̃I�u�W�F�N�g����ɃA�j���[�V�����̃p�X����������܂��B");
        private static readonly GUIContent SkinnedMeshRendererLabel = GuiUtils.GenerateCustomLabel("Skinned Mesh Renderer", "�A�j���[�V�������쐬������BlendShape��������Skinned Mesh Renderer�I�u�W�F�N�g���w�肵�Ă��������B");
        private static readonly GUIContent BaseAnimationLabel = GuiUtils.GenerateCustomLabel("�A�j���[�V����(�C��)", "�^����J�n����ۂɁA���炩���ߓǂݍ���œK�p���Ă����A�j���[�V�����ł��B�����̃A�j���[�V���������ɐV�����쐬����ꍇ�Ɏg�p���܂��B");
        private static readonly GUIContent LoadAnimationLabel = GuiUtils.GenerateCustomLabel("�^�掞�ɃA�j���[�V�������g�p", "�^�掞�Ƀx�[�X�A�j���[�V������ǂݍ��ނ��ǂ�����I���ł��܂��B�v���r���[�̂݊m�F�������ꍇ�̓I�t�ɂ���ƕ֗��ł��B");
        private static readonly GUIContent WriteDefaultsLabel = GuiUtils.GenerateCustomLabel("���ύX�̒l���ۑ�", "���ύX�̍��ڂ��A�j���[�V�����Ɋ܂߂邩�ǂ�����I���ł��܂��B");
        private static readonly GUIContent VRCModeLabel = GuiUtils.GenerateCustomLabel("VRChat���[�h", "VRChat�̕\����ς̎��Ɏg�p������̂ł��B");
        private static readonly GUIContent SameAsRootLabel = GuiUtils.GenerateCustomLabel("���[�g�I�u�W�F�N�g�Ɠ���", "���[�g�I�u�W�F�N�g����VRC Avatar Descriptor���g�p���܂��B");
        private static readonly GUIContent AvatarDescriptorLabel = GuiUtils.GenerateCustomLabel("Avatar Descriptor", "�Ⴄ�I�u�W�F�N�g����VRC Avatar Descriptor���g�p���܂��B");
        
        [MenuItem("Tools/�Ղ��̂[��/SmileyClip")]
        public static void ShowWindow()
        {
            GetWindow<SmileyClip>("SmileyClip");

            if (BETA_VERSION_BOOL)
            {
                GuiUtils.ShowDialog("����SmileyClip�̓x�[�^�łł��B\n�A�o�^�[��BlendShape�̈Ӑ}���Ȃ��ύX��h�����߁A�q�G�����L�[��ŃA�o�^�[�𕡐����A�o�b�N�A�b�v������Ă������Ƃ��������߂��܂��B");
            }
        }

        private void OnGUI()
        {
            GuiUtils.DrawBigTitle(AUTHOR, CURRENT_VERSION, BETA_VERSION_BOOL);

            #region ���[�g�I�u�W�F�N�g��Renderer�̑I��
            GuiUtils.DrawSection("���[�g�I�u�W�F�N�g��Renderer�̑I��", isFirst: true);
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
                            EditorGUILayout.LabelField("�I�����ꂽRenderer", $"{selectedRenderer.name} �� BlendShape��: {selectedRenderer.sharedMesh.blendShapeCount}");
                        }
                        else
                        {
                            EditorGUILayout.LabelField("SkinnedMeshRenderer��������܂���B");
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("SkinnedMeshRenderer��������܂���B");
                    }
                }
            }

            if (isPlayingAnimation)
            {
                EditorGUILayout.HelpBox("�v���r���[�A�܂��͘^�撆�ɃI�u�W�F�N�g�ARenderer��ύX���邱�Ƃ͏o���܂���B\n�X�N���v�g���ēǍ������BlendShape���߂炸�ɂ��̂܂܂ɂȂ�̂Œ��ӂ��Ă��������B", MessageType.Warning);
            }
            #endregion

            #region �x�[�X�A�j���[�V�����ݒ�
            GuiUtils.DrawSection("�x�[�X�A�j���[�V�����ݒ�");
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
                        GuiUtils.DrawSection("�A�j���[�V�����̍Đ��ʒu");

                        if (!fixAnimation)
                        {
                            float newPreviewTime = EditorGUILayout.Slider("�Đ�����", previewTime, 0f, animationLength);
                            if (Mathf.Abs(newPreviewTime - previewTime) > 0.001f)
                            {
                                previewTime = newPreviewTime;
                                LoadAnimationBrendshapes(previewTime);
                            }

                            EditorGUILayout.HelpBox("�A�j���[�V�����̍Đ��ʒu��ύX����ƁA���ɕύX����Ă���BlendShape���㏑������Ă��܂��܂��B\n��ɃA�j���[�V���������̃`�F�b�N�{�b�N�X����Œ肵�Ă���ύX���s�����Ƃ��������߂��܂��B", MessageType.Warning);
                        }

                        fixAnimation = EditorGUILayout.Toggle("�A�j���[�V�������Œ�", fixAnimation);
                    }
                }
            }
            else
            {
                fixAnimation = false;
            }
            #endregion

            #region �A�j���[�V�����ݒ�
            GuiUtils.DrawSection("�A�j���[�V�����ݒ�");
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

            #region ����
            GuiUtils.DrawSection("����");
            bool isValid = targetObject != null && !SkinnedMeshRendererUtils.IsNull(selectedRenderer);

            bool canStartPreview = !isPlayingAnimation && isValid && baseAnimation != null;
            bool canEndPreview = isPreviewing;

            bool canStartRecording = !isPlayingAnimation && isValid;
            bool canEndRecording = isRecording;

            if (canStartPreview || canEndPreview)
            {
                if (GUILayout.Button(isPreviewing ? "�x�[�X�A�j���[�V�����̃v���r���[���I��" : "�x�[�X�A�j���[�V�����̃v���r���[���J�n"))
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
                if (GUILayout.Button(isRecording ? "�^����I�����A�ۑ�����" : "�^����J�n����"))
                {
                    if (!isRecording)
                    {
                        isRecording = SaveAndPlayAnimation(loadAnimation);
                    }
                    else
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "�m�F",
                            "���݂�BlendShape��ۑ����ďI�����܂��B\n��낵���ł����H",
                            "�ۑ�����",
                            "�L�����Z��"
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
                if (GUILayout.Button("�A�j���[�V�������ēx�ǂݍ���"))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "�m�F",
                        "���݂�BlendShape�̕ύX�𖳎����A���݂̍Đ��ʒu�̃x�[�X�A�j���[�V�������ēx�ǂݍ��݂܂��B\n��낵���ł����H",
                        "�ēx�ǂݍ���",
                        "�L�����Z��"
                    );

                    if (confirm)
                    {
                        LoadAnimationBrendshapes(previewTime);
                    }
                }
            }

            if (!(canStartRecording || canEndRecording) && !(canStartPreview || canEndPreview))
            {
                EditorGUILayout.HelpBox($"����\�ȍ��ڂ�����܂���B", MessageType.Info);
            }

            if (isRecording)
            {
                if (GUILayout.Button("�^����L�����Z��"))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "�m�F",
                        "���݂�BlendShape�ύX���e�͖�������폜����܂��B\n�{���ɃL�����Z�����܂����H",
                        "�L�����Z������",
                        "�߂�"
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

                EditorGUILayout.HelpBox($"�ύX�ς݂�BlendShape��: {changedCount}", MessageType.Info);

                if (notFoundBlendShapes > 0)
                {
                    var missingRate = (float)notFoundBlendShapes / (foundBlendShapes + notFoundBlendShapes);
                    EditorGUILayout.HelpBox($"�A�j���[�V��������{Mathf.Round(missingRate * 100)}% ({notFoundBlendShapes}��) ��BlendShape���K�p����܂���ł����B\n���̒l���傫������ꍇ�A�Ώۂ�Renderer���Ⴄ�\��������܂��B", MessageType.Error);
                }
            }
            #endregion

            #region ���Z�b�g
            GuiUtils.DrawSection("���Z�b�g");
            using (new EditorGUI.DisabledScope(isPlayingAnimation))
            {
                if (GUILayout.Button("�I�u�W�F�N�g�̃��Z�b�g"))
                {
                    targetObject = null;
                }

                if (GUILayout.Button("�x�[�X�A�j���[�V�����̃��Z�b�g"))
                {
                    baseAnimation = null;
                }
            }

            using (new EditorGUI.DisabledScope(SkinnedMeshRendererUtils.IsNull(selectedRenderer)))
            {
                if (GUILayout.Button("�S�Ă�BlendShape��0�Ƀ��Z�b�g"))
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
                GuiUtils.ShowDialog("Renderer��������Ȃ��������߁ABlendShape�̕ۑ��Ɏ��s���܂����B");
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
                GuiUtils.ShowDialog("Renderer��������Ȃ��������߁ABlendShape�̕����Ɏ��s���܂����B");
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
                GuiUtils.ShowDialog("Renderer��������Ȃ��������߁A�A�j���[�V�����̓ǂݍ��݂Ɏ��s���܂����B");
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
                GuiUtils.ShowDialog("Renderer��������Ȃ��������߁A����̊J�n�Ɏ��s���܂����B");
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
                GuiUtils.ShowDialog("�I�u�W�F�N�g��������Ȃ��������߁ABlendShape�̕ۑ��Ɏ��s���܂����B");
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

                string path = EditorUtility.SaveFilePanelInProject("�A�j���[�V�����t�@�C����ۑ�����", fileName, "anim", "�ۑ�����", initialPath);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(recordedClip, path);
                    AssetDatabase.SaveAssets();

                    GuiUtils.ShowDialog($"�A�j���[�V�����̕ۑ����������܂����B\n{path}");

                    bool confirm = EditorUtility.DisplayDialog(
                        "�m�F",
                        "�A�j���[�V�����̕ۑ����Assets���ŊJ���܂����H",
                        "�͂�",
                        "������"
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