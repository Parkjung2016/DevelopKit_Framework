using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.UI
{
    /// <summary>알림 프리팹 등록과 해제를 컴포넌트 수명에 맞춰 자동으로 관리합니다.</summary>
    public abstract class NotificationDotViewInstaller : MonoBehaviour
    {
        private readonly List<IDisposable> registrations = new();

        protected void OnEnable()
        {
            DisposeRegistrations();

            try
            {
                RegisterViews();
            }
            catch
            {
                DisposeRegistrations();
                throw;
            }
        }

        protected void OnDisable()
        {
            DisposeRegistrations();
        }

        /// <summary>이 컴포넌트가 활성화될 때 필요한 알림 프리팹을 등록합니다.</summary>
        protected abstract void RegisterViews();

        protected void Register(string key, GameObject prefab)
        {
            if (prefab != null)
                registrations.Add(NotificationDotViews.Register(key, prefab));
        }

        protected void Register<TEnum>(TEnum key, GameObject prefab)
            where TEnum : struct, Enum
        {
            if (prefab != null)
                registrations.Add(NotificationDotViews.Register(key, prefab));
        }
        private void DisposeRegistrations()
        {
            for (int i = registrations.Count - 1; i >= 0; i--)
                registrations[i]?.Dispose();

            registrations.Clear();
        }
    }
}