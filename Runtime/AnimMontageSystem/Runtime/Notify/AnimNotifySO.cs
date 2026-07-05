using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public abstract class AnimNotifySO : ScriptableObject
    {
        public virtual string DisplayName => name;
        public virtual Color EditorColor => new(0.35f, 0.75f, 1f, 1f);

        public abstract void OnNotify(AnimNotifyContext context);
    }
}
