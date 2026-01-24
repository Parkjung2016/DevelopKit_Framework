using Skddkkkk.DevelopKit.Framework.GamePlayTagSystem.Runtime;
using System.Collections.Generic;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using Skddkkkk.DevelopKit.BasicTemplate.Runtime;

namespace Skddkkkk.DevelopKit.Framework.AbilitySystem.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public class AbilitySystem : MonoBehaviour, IGamePlayTagManagerInstaller
    {
#if ODIN_INSPECTOR
        [InfoBox("InputSystem 연동이 필요한 경우에만 사용")]
#else
        [Header("InputSystem 연동이 필요한 경우에만 사용")]
#endif
        [SerializeField] private AbilityInputBridgeSO abilityInputBridge;

#if ODIN_INSPECTOR
        [InfoBox("초기 Ability 등록이 필요한 경우에만 사용")]
#else
        [Header("초기 Ability 등록이 필요한 경우에만 사용")]
#endif
        [SerializeField] private AbilitySetupSO abilitySetup;

        private Dictionary<GamePlayTagEnum, AbilitySO> abilitieDic;
        private GamePlayTagManager gamePlayTagManagerCompo;

        private IAbilitySystemOwner owner;

        private void Reset()
        {
            Transform targetTrm = transform.parent;
            if (targetTrm == null) targetTrm = transform;
            if (targetTrm.GetComponentInChildren<GamePlayTagManager>() == null)
            {
                new GameObject("GamePlayTagManager", typeof(GamePlayTagManager)).transform
                    .SetParent(targetTrm);
            }
        }

        public void InitGamePlayTagManger(GamePlayTagManager gamePlayTagManager)
        {
            gamePlayTagManagerCompo = gamePlayTagManager;
        }

        private void Awake()
        {
            IAbilitySystemInstaller[] installers =
                transform.root.GetComponentsInChildren<IAbilitySystemInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                installers[i].InitAbilitySystem(this);
            }

            abilitieDic = new();
            CheckAbilitySetup();
            // AttributeInjector.Inject(_abilityInputBridge, SceneManager.GetActiveScene().GetSceneContainer());
            abilityInputBridge?.Init(this);
        }


        private void OnDestroy()
        {
            abilityInputBridge?.UnSubscribeEvent();
        }

        private void CheckAbilitySetup()
        {
            if (abilitySetup == null) return;
            foreach (AbilitySO ability in abilitySetup)
            {
                TryGiveAbility(ability);
            }
        }

        public void RegisterAbilitySystemOwner(IAbilitySystemOwner abilitySystemOwner)
        {
            owner = abilitySystemOwner;
        }

        /// <summary>
        /// Ability 추가하고, 해당 GamePlayTag도 부여합니다.
        /// </summary>
        /// <param name="ability">추가할 Ability 객체입니다.</param>
        public bool TryGiveAbility(AbilitySO ability)
        {
            if (abilitieDic.ContainsKey(ability.GrantedGamePlayTag)) return false;
            AbilitySO clonedAbility = ability.Clone();
            abilitieDic.Add(ability.GrantedGamePlayTag, clonedAbility);
            clonedAbility.RegisteredAbility(owner);
            if (clonedAbility.ActivateAbilityWhenRegistered)
                TryActivateAbility(clonedAbility);
            return true;
        }

        /// <summary>
        /// Ability 제거하고, 관련 GamePlayTag도 제거합니다.
        /// </summary>
        /// <param name="abilityTag">제거할 Ability의 GamePlayTag입니다.</param>
        /// <param name="destroyAfterRemoved">Ability 제거 후 해당 객체를 파괴할지 여부입니다.</param>
        public bool TryRemoveAbility(GamePlayTagEnum abilityTag, bool destroyAfterRemoved = true)
        {
            if (abilitieDic.Remove(abilityTag, out AbilitySO removedAbility))
            {
                gamePlayTagManagerCompo.RemoveGamePlayTag(removedAbility.GrantedGamePlayTag);
                removedAbility.UnRegisteredAbility();
                if (destroyAfterRemoved)
                    Destroy(removedAbility);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ability 제거하고, 관련 GamePlayTag도 제거합니다.
        /// </summary>
        /// <param name="ability">제거할 Ability입니다.</param>
        /// <param name="destroyAfterRemoved">Ability 제거 후 해당 객체를 파괴할지 여부입니다.</param>
        public bool TryRemoveAbility(AbilitySO ability, bool destroyAfterRemoved = true)
        {
            if (abilitieDic.Remove(ability.GrantedGamePlayTag, out AbilitySO removedAbility))
            {
                gamePlayTagManagerCompo.RemoveGamePlayTag(removedAbility.GrantedGamePlayTag);
                removedAbility.UnRegisteredAbility();
                if (destroyAfterRemoved)
                    Destroy(removedAbility);
                return true;
            }

            return false;
        }

        private bool TryActivateInternal(AbilitySO ability, InputAction.CallbackContext? context = null)
        {
            if (!TryGetAbility(ability, out AbilitySO foundAbility))
            {
#if ENABLE_LOG
                SkddkkkkDebug.LogWarning($"Can't find {ability} in registered abilities");
#else
                Debug.LogWarning($"Can't find {ability} in registered abilities");
#endif
                return false;
            }

            if (!CanActivateAbility(foundAbility))
            {
                return false;
            }

            gamePlayTagManagerCompo.GrantGamePlayTag(ability.GrantedGamePlayTag);

            if (context.HasValue)
                foundAbility.ActivateAbility(context.Value);
            else
                foundAbility.ActivateAbility();

            void OnEnded()
            {
                gamePlayTagManagerCompo.RemoveGamePlayTag(ability.GrantedGamePlayTag);
                foundAbility.OnAbilityEnded -= OnEnded;
            }

            foundAbility.OnAbilityEnded += OnEnded;

            return true;
        }

        /// <summary>
        /// AbilitySO 기준으로 Ability 활성화를 시도합니다.
        /// 활성화 이후에는 AbilitySO의 EndAbility를 호출해야 해당 Ability를 다시 활성화할 수 있습니다.
        /// </summary>
        /// <param name="ability">활성화하려는 Ability 객체입니다.</param>
        /// <returns>성공적으로 활성화되면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryActivateAbility(AbilitySO ability)
        {
            TryActivateInternal(ability);
            return true;
        }

        /// <summary>
        /// AbilitySO 기준으로 Ability 활성화를 시도합니다.
        /// 활성화 이후에는 AbilitySO의 EndAbility를 호출해야 해당 Ability를 다시 활성화할 수 있습니다.
        /// </summary>
        /// <param name="ability">활성화하려는 Ability 객체입니다.</param>
        /// <returns>성공적으로 활성화되면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryActivateAbility(AbilitySO ability, InputAction.CallbackContext context)
        {
            return TryActivateInternal(ability, context);
        }


        /// <summary>
        /// GamePlayTag 기준으로 Ability 활성화를 시도합니다.
        /// 활성화 이후에는 AbilitySO의 EndAbility를 호출해야 해당 Ability를 다시 활성화할 수 있습니다.
        /// </summary>
        /// <param name="abilityTag">활성화하려는 Ability의 GamePlayTag입니다.</param>
        /// <returns>성공적으로 활성화되면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryActivateAbility(GamePlayTagEnum abilityTag)
        {
            if (!TryGetAbility(abilityTag, out AbilitySO foundAbility)) return false;
            if (!CanActivateAbility(foundAbility))
                return false;

            foundAbility.ActivateAbility();
            return true;
        }

        /// <summary>
        /// 해당 Ability 활성화할 수 있는지 확인합니다.
        /// </summary>
        /// <param name="ability">
        /// TryGetAbility를 통해 가져온 AbilitySO.  
        /// </param>
        public bool CanActivateAbility(AbilitySO ability)
        {
            if (ability.IsActivating) return false;

            if (gamePlayTagManagerCompo.HasGamePlayTag(ability.BlockedGamePlayTags))
            {
#if ENABLE_LOG
                SkddkkkkDebug.LogWarning($"{ability.grantedGamePlayTag} tag could not be found.");
#else
                Debug.LogWarning($"{ability.GrantedGamePlayTag} tag could not be found.");
#endif
                return false;
            }

            return true;
        }

        /// <summary>
        /// AbilitySO 기준으로 Ability 보유 여부를 확인합니다.
        /// </summary>
        public bool HasAbility(AbilitySO ability)
        {
            foreach (var pair in abilitieDic)
            {
                if (pair.Value.name == ability.name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// GamePlayTag 기준으로 Ability 보유 여부를 확인합니다.
        /// </summary>
        public bool HasAbility(GamePlayTagEnum abilityTag)
        {
            foreach (var pair in abilitieDic)
            {
                if ((pair.Value.GrantedGamePlayTag & abilityTag) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// GamePlayTag 기준으로 Ability 가져오기를 시도합니다.
        /// </summary>
        /// <param name="abilityTag">찾고자 하는 Ability의 GamePlayTag입니다.</param>
        /// <param name="foundAbility">검색에 성공한 경우, 해당 AbilitySO가 할당됩니다.</param>
        /// <returns>해당 GamePlayTag를 가진 Ability가 존재하면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryGetAbility(GamePlayTagEnum abilityTag, out AbilitySO foundAbility)
        {
            return abilitieDic.TryGetValue(abilityTag, out foundAbility);
        }


        /// <summary>
        /// GamePlayTag 기준으로 Ability 가져오기를 시도합니다.
        /// </summary>
        /// <param name="abilityTag">찾고자 하는 Ability의 GamePlayTag입니다.</param>
        /// <param name="foundAbility">검색에 성공한 경우, 해당 AbilitySO가 할당됩니다.</param>
        /// <returns>해당 GamePlayTag를 가진 Ability가 존재하면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryGetAbility<T>(GamePlayTagEnum abilityTag, out T foundAbility) where T : AbilitySO
        {
            if (abilitieDic.TryGetValue(abilityTag, out AbilitySO ability))
            {
                foundAbility = ability as T;
                return true;
            }

            foundAbility = null;
            return false;
        }

        /// <summary>
        /// AbilitySO 기준으로 Ability 가져오기를 시도합니다.
        /// </summary>
        /// <param name="ability">기준이 되는 AbilitySO입니다. 이름을 비교하여 검색합니다.</param>
        /// <param name="foundAbility">검색에 성공한 경우, 해당 AbilitySO가 할당됩니다.</param>
        /// <returns>Ability를 찾았으면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryGetAbility(AbilitySO ability, out AbilitySO foundAbility)
        {
            foundAbility = null;
            foreach (var storedAbility in abilitieDic.Values)
            {
                if (storedAbility.name == ability.name)
                {
                    foundAbility = storedAbility;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// AbilitySO 기준으로 Ability 가져오기를 시도합니다.
        /// </summary>
        /// <param name="ability">기준이 되는 AbilitySO입니다. 이름을 비교하여 검색합니다.</param>
        /// <param name="foundAbility">검색에 성공한 경우, 해당 AbilitySO가 할당됩니다.</param>
        /// <returns>Ability를 찾았으면 true, 그렇지 않으면 false를 반환합니다.</returns>
        public bool TryGetAbility<T>(AbilitySO ability, out T foundAbility) where T : AbilitySO
        {
            if (TryGetAbility(ability, out AbilitySO foundBaseAbility))
            {
                foundAbility = foundBaseAbility as T;
                return true;
            }

            foundAbility = null;
            return false;
        }
    }
}