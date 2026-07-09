using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public abstract class AnimNotify
    {
        [SerializeField] private bool triggerInEditorScrub;

        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(0.35f, 0.75f, 1f, 1f);
        public bool TriggerInEditorScrub => triggerInEditorScrub;

        public abstract void OnNotify(AnimNotifyContext context);
    }
}
