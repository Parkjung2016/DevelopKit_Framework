using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.GamePlayTagSystem.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public class GamePlayTagManager : MonoBehaviour
    {
        public delegate void ChangedGamePlayTagEventHandler(GamePlayTagEnum previousTags, GamePlayTagEnum newTags);

        public event ChangedGamePlayTagEventHandler OnChangedGamePlayTag;
        private GamePlayTagEnum grantedGamePlayTag;
        
        private void Awake()
        { 
            IGamePlayTagManagerInstaller[] installers= transform.root.GetComponentsInChildren<IGamePlayTagManagerInstaller>();
            for(int i =0; i< installers.Length; i++)
            {
                installers[i].InitGamePlayTagManger(this);
            }
        }

        /// <summary>
        /// 지정된 GamePlayTag를 부여합니다.
        /// </summary>
        /// <param name="gamePlayTag">부여할 GamePlayTag입니다.</param>
        public void GrantGamePlayTag(GamePlayTagEnum gamePlayTag)
        {
            GamePlayTagEnum previousTags = grantedGamePlayTag;
            grantedGamePlayTag |= gamePlayTag;
            OnChangedGamePlayTag?.Invoke(previousTags, grantedGamePlayTag);
        }

        /// <summary>
        /// 지정된 GamePlayTag를 제거합니다.
        /// </summary>
        /// <param name="gamePlayTag">제거할 GamePlayTag입니다.</param>
        public void RemoveGamePlayTag(GamePlayTagEnum gamePlayTag)
        {
            GamePlayTagEnum previousTags = grantedGamePlayTag;
            grantedGamePlayTag  &= ~gamePlayTag;
            OnChangedGamePlayTag?.Invoke(previousTags, grantedGamePlayTag);
        }

        /// <summary>
        /// 지정된 GamePlayTag를 가지고 있는지 확인합니다.
        /// </summary>
        /// <param name="gamePlayTag">확인할 GamePlayTag입니다.</param>
        /// <returns>해당 GamePlayTag를 가지고 있으면 true, 아니면 false를 반환합니다.</returns>
        public bool HasGamePlayTag(GamePlayTagEnum gamePlayTag) => (grantedGamePlayTag & gamePlayTag) != 0;
    }
}