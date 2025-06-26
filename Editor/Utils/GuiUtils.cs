using UnityEditor;
using UnityEngine;

namespace SmileyClip.Utils
{
    internal static class GuiUtils
    {
        private const string TOOL_NAME = "SmileyClip";
        private const string TOOL_DESCRIPTION = "- BlendShapeアニメーション作成支援ツール -";

        /// <summary>
        /// Unityのダイアログを表示します。
        /// </summary>
        /// <param name="message"></param>
        internal static void ShowDialog(string message)
        {
            EditorUtility.DisplayDialog(TOOL_NAME, message, "OK");
        }

        /// <summary>
        /// UI上にタイトルを表示します。
        /// </summary>
        /// <param name="author"></param>
        /// <param name="version"></param>
        /// <param name="betaVersion"></param>
        internal static void DrawBigTitle(string author, string version, bool betaVersion)
        {
            Rect fullRect = EditorGUILayout.GetControlRect(false, 70);
            EditorGUI.DrawRect(fullRect, new Color(0.15f, 0.15f, 0.15f));

            Rect titleRect = new(fullRect.x, fullRect.y, fullRect.width, 28);
            GUIStyle titleStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(titleRect, TOOL_NAME, titleStyle);

            Rect descriptionRect = new(fullRect.x, fullRect.y + 28, fullRect.width, 18);
            GUIStyle descriptionStyle = new(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            EditorGUI.LabelField(descriptionRect, TOOL_DESCRIPTION, descriptionStyle);

            GUIStyle authorStyle = new()
            {
                fontSize = 14,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(fullRect, $"Script by {author}", authorStyle);

            GUIStyle versionStyle = new()
            {
                fontSize = 14,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(fullRect, version + (betaVersion ? " (Beta)" : ""), versionStyle);
        }

        /// <summary>
        /// 説明のついたカスタムラベルを作成します
        /// </summary>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        internal static GUIContent GenerateCustomLabel(string title, string description)
        {
            return new(title, description);
        }

        /// <summary>
        /// UnityのUI上にセクションを作成します。
        /// </summary>
        /// <param name="title"></param>
        /// <param name="isFirst"></param>
        internal static void DrawSection(string title, bool isFirst = false)
        {
            int firstSpaceSize = isFirst ? 8 : 15;
            GUILayout.Space(firstSpaceSize);

            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.white;

            EditorGUILayout.LabelField(title, style);

            GUILayout.Space(2);

            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Color.white);

            GUILayout.Space(3);
        }
    }
}
