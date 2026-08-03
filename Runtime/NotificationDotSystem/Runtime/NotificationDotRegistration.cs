using System;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>런타임에 등록한 알림 정의의 수명을 관리합니다.</summary>
    internal sealed class NotificationDotRegistration : IDisposable
    {
        private NotificationDotSystem owner;
        private readonly string key;
        private readonly long id;
        private readonly int generation;

        internal NotificationDotRegistration(
            NotificationDotSystem owner,
            string key,
            long id,
            int generation)
        {
            this.owner = owner;
            this.key = key;
            this.id = id;
            this.generation = generation;
        }


        public void Dispose()
        {
            if (owner == null)
                return;

            NotificationDotSystem current = owner;
            owner = null;
            current.UnregisterDefinition(key, id, generation);
        }
    }
}