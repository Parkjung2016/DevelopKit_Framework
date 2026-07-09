using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class LogAnimNotify : AnimNotify
    {
        [SerializeField] private string message = "AnimNotify";

        public override string DisplayName => "Log";
        public override Color EditorColor => new(0.6f, 0.9f, 0.5f, 1f);

        public override void OnNotify(AnimNotifyContext context)
        {
            CDebug.Log(message);
        }
    }
}
