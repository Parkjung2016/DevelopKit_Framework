using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public abstract class AnimNotifyBase
    {
        [SerializeField, ShowIf("CanEditTriggerOnManualPreview()")]
        private bool triggerOnManualPreview;

        public bool TriggerOnManualPreview => triggerOnManualPreview;

        public virtual bool CanEditTriggerOnManualPreview() => true;
    }

    [Serializable]
    public abstract class AnimNotify : AnimNotifyBase
    {
        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(0.35f, 0.75f, 1f, 1f);

        public abstract void OnNotify(AnimNotifyContext context);
    }
}