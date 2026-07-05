using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [CreateAssetMenu(fileName = "Notify_Log", menuName = "PJDev/Animation/Notify/Log")]
    public sealed class LogAnimNotifySO : AnimNotifySO
    {
        [SerializeField] private string message = "AnimNotify";

        public override Color EditorColor => new(0.6f, 0.9f, 0.5f, 1f);

        public override void OnNotify(AnimNotifyContext context)
        {
            Debug.Log($"[AnimNotify] {message} @ {context.MontageTime:0.###} ({context.Montage?.name})");
        }
    }
}
