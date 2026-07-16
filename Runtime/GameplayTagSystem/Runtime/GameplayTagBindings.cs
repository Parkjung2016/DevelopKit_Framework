using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 상태 콜백을 등록하고 한 번에 해제하는 수명 객체입니다.</summary>
    public sealed class GameplayTagBindings : IDisposable
    {
        private sealed class Binding : IDisposable
        {
            private readonly GameplayTagBindings owner;
            private readonly GameplayTag tag;
            private readonly Action<bool> callback;
            private bool isRegistered;

            public Binding(GameplayTagBindings owner, GameplayTag tag, Action<bool> callback)
            {
                this.owner = owner;
                this.tag = tag;
                this.callback = callback;
            }

            public void Register(bool invokeImmediately)
            {
                owner.container.RegisterTagEventCallback(
                    tag,
                    GameplayTagEventType.NewOrRemoved,
                    OnTagChanged);
                isRegistered = true;

                if (invokeImmediately)
                    callback(owner.container.GetTagCount(tag) > 0);
            }

            public void Dispose()
            {
                if (!Unregister())
                    return;

                owner.bindings.Remove(this);
            }

            public bool Unregister()
            {
                if (!isRegistered)
                    return false;

                isRegistered = false;
                owner.container.RemoveTagEventCallback(
                    tag,
                    GameplayTagEventType.NewOrRemoved,
                    OnTagChanged);
                return true;
            }

            private void OnTagChanged(GameplayTag _, int newCount)
            {
                callback(newCount > 0);
            }
        }

        private readonly GameplayTagCountContainer container;
        private readonly List<Binding> bindings = new();
        private bool isDisposed;

        public GameplayTagBindings(GameplayTagCountContainer container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public GameplayTagBindings(GameObject gameObject)
            : this(GetContainer(gameObject))
        {
        }

        /// <summary>
        /// 태그의 보유 상태가 바뀔 때 호출될 콜백을 등록합니다.
        /// 반환값을 Dispose하면 해당 콜백만 해제됩니다.
        /// </summary>
        public IDisposable Bind(GameplayTag tag, Action<bool> callback, bool invokeImmediately = true)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(GameplayTagBindings));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Binding binding = new(this, tag, callback);
            bindings.Add(binding);
            try
            {
                binding.Register(invokeImmediately);
                return binding;
            }
            catch
            {
                binding.Dispose();
                throw;
            }
        }

        /// <summary>이 객체로 등록한 모든 콜백을 해제합니다.</summary>
        public void UnbindAll()
        {
            for (int i = 0; i < bindings.Count; i++)
                bindings[i].Unregister();

            bindings.Clear();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            UnbindAll();
        }

        private static GameplayTagCountContainer GetContainer(GameObject gameObject)
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject));

            if (!gameObject.TryGetComponent(out ObjectGameplayTagContainer component))
            {
                throw new InvalidOperationException(
                    $"{gameObject.name}에 {nameof(ObjectGameplayTagContainer)} 컴포넌트가 없습니다.");
            }

            return component.Container;
        }
    }
}