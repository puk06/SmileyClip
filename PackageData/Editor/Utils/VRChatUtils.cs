using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace SmileyClip.Utils
{
    internal static class VRChatUtils
    {
        /// <summary>
        /// LipSync用のBlendShapeを全て取得します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        internal static string[] GetAllVisemeBlendShapes(VRCAvatarDescriptor avatar)
            => avatar.VisemeBlendShapes;

        /// <summary>
        /// Blink用のBlendShapeを全て取得します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        internal static string[] GetAllBlinkBlendShapes(VRCAvatarDescriptor avatar)
        {
            int[] eyeLidsBlendshapes = avatar.customEyeLookSettings.eyelidsBlendshapes;
            string[] blinkBlendShapes = new string[eyeLidsBlendshapes.Length];

            Mesh avatarMesh = avatar.VisemeSkinnedMesh.sharedMesh;
            for (int i = 0; i < blinkBlendShapes.Length; i++)
            {
                blinkBlendShapes[i] = avatarMesh.GetBlendShapeName(eyeLidsBlendshapes[i]);
            }

            return blinkBlendShapes;
        }

        /// <summary>
        /// オブジェクトからVRCAvatarDescriptorを取得します。
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        internal static VRCAvatarDescriptor GetAvatarDescriptor(GameObject gameObject)
            => gameObject.GetComponent<VRCAvatarDescriptor>();
    }
}
