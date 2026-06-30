using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 개수 변경을 콜백에 바인딩하는 헬퍼입니다.</summary>
    public struct GameplayTagContainerBinds
    {
        private struct BindData
        {
            public OnTagCountChangedDelegate OnTagAddedOrRemved;
            public GameplayTag Tag;
        }

        private GameplayTagCountContainer container;
        private List<BindData> binds;

        public GameplayTagContainerBinds(GameplayTagCountContainer container)
        {
            this.container = container;
            binds = null;
        }

        public GameplayTagContainerBinds(GameObject gameObject)
        {
            ObjectGameplayTagContainer component = gameObject.GetComponent<ObjectGameplayTagContainer>();
            container = component.GameplayTagContainer;
            binds = null;
        }

        /// <summary>태그 추가·제거 시 bool 콜백을 등록합니다.</summary>
        public void Bind(GameplayTag tag, Action<bool> onTagAddedOrRemoved)
        {
            binds ??= new List<BindData>();

            void OnTagAddedOrRemoved(GameplayTag gameplayTag, int newCount)
            {
                onTagAddedOrRemoved(newCount > 0);
            }

            binds.Add(new BindData { Tag = tag, OnTagAddedOrRemved = OnTagAddedOrRemoved });
            container.RegisterTagEventCallback(tag, GameplayTagEventType.NewOrRemoved, OnTagAddedOrRemoved);

            int count = container.GetTagCount(tag);
            onTagAddedOrRemoved(count > 0);
        }

        /// <summary>등록된 모든 바인딩을 해제합니다.</summary>
        public void UnbindAll()
        {
            if (binds == null)
                return;

            foreach (BindData bind in binds)
                container.RemoveTagEventCallback(bind.Tag, GameplayTagEventType.NewOrRemoved, bind.OnTagAddedOrRemved);

            binds.Clear();
        }
    }
}
