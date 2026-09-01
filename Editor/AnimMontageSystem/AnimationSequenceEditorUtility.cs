using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class AnimationSequenceEditorUtility
    {
        /// <summary>Sequence Clip 변경에 필요한 Undo와 Dirty 처리를 한곳에서 수행합니다.</summary>
        public static bool SetClip(AnimSequenceSO sequence, AnimationClip clip)
        {
            if (sequence == null || sequence.Clip == clip)
                return false;

            Undo.RecordObject(sequence, "Set Sequence Clip");
            sequence.SetClip(clip);
            EditorUtility.SetDirty(sequence);
            return true;
        }
    }
}
