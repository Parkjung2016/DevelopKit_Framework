using System.Collections;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 컨테이너의 태그를 순회하는 열거자입니다.</summary>
    public struct GameplayTagEnumerator : IEnumerator<GameplayTag>, IEnumerable<GameplayTag>
    {
        public readonly GameplayTag Current
        {
            get
            {
                return GameplayTagManager.GetTagFromRuntimeIndex(indices[currentIndex]);
            }
        }

        readonly object IEnumerator.Current => Current;

        private readonly List<int> indices;
        private int currentIndex;

        internal GameplayTagEnumerator(List<int> indices)
        {
            this.indices = indices;
            currentIndex = -1;
        }

        public readonly void Dispose()
        {
        }

        public bool MoveNext()
        {
            currentIndex++;
            return indices != null && currentIndex < indices.Count;
        }

        public void Reset()
        {
            currentIndex = -1;
        }

        public readonly GameplayTagEnumerator GetEnumerator()
        {
            return this;
        }

        readonly IEnumerator<GameplayTag> IEnumerable<GameplayTag>.GetEnumerator()
        {
            return this;
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }
    }
}
