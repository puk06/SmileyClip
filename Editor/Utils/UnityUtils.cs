using UnityEditor;
using UnityEngine;

namespace SmileyClip.Utils
{
    internal static class UnityUtils
    {
        /// <summary>
        /// UnityのAssets内で、指定されたパスのファイルを開きます。
        /// </summary>
        /// <param name="assetPath"></param>
        internal static void SelectAssetAtPath(string assetPath)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                GuiUtils.ShowDialog($"指定されたパスのアセットが見つかりません:\n{assetPath}");
            }
        }
    }
}