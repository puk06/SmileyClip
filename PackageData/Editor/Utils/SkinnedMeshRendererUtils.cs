using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmileyClip.Utils
{
    internal static class SkinnedMeshRendererUtils
    {
        /// <summary>
        /// GameObject内の全てのSkinnedMeshRendererを取得します。
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        internal static SkinnedMeshRenderer[] GetAllSkinnedMeshRenderers(GameObject gameObject)
        {
            return gameObject
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(smr => !IsNull(smr) && smr.sharedMesh.blendShapeCount != 0)
                .ToArray();
        }

        /// <summary>
        /// BlendShapeの数が一番多いSkinnedMeshRendererのindexを取得します。
        /// </summary>
        /// <param name="renderers"></param>
        /// <returns></returns>
        internal static int GetRendererWithMostBlendShapes(SkinnedMeshRenderer[] renderers)
        {
            int maxIndex = 0;
            int maxCount = -1;

            for (int i = 0; i < renderers.Length; i++)
            {
                int count = !IsNull(renderers[i]) ? renderers[i].sharedMesh.blendShapeCount : -1;
                if (count > maxCount)
                {
                    maxCount = count;
                    maxIndex = i;
                }
            }

            return maxIndex;
        }

        /// <summary>
        /// 指定されたSkinnedMeshRenderer内の全てのBlendShapeを0に設定します。
        /// </summary>
        /// <param name="skinnedMeshRenderer"></param>
        internal static void SetAllBlendShapesToDefault(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            if (IsNull(skinnedMeshRenderer))
            {
                GuiUtils.ShowDialog("SkinnedMeshRendererが見つからなかったため、BlendShapeの設定に失敗しました。");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "確認",
                "選択されたSkinnedMeshRenderer内の全てのBlendShapeを0に設定しますか？",
                "はい",
                "いいえ"
            );

            if (confirm && !IsNull(skinnedMeshRenderer))
            {
                for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    skinnedMeshRenderer.SetBlendShapeWeight(i, 0f);
                }
            }
        }

        /// <summary>
        /// 指定されたSkinnedMeshRendererがNullかどうかをチェックします。
        /// </summary>
        /// <param name="skinnedMeshRenderer"></param>
        /// <returns></returns>
        internal static bool IsNull(SkinnedMeshRenderer skinnedMeshRenderer)
            => skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null;
    }
}
