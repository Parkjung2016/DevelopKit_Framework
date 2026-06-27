using System;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>
    /// 어셈블리 수준에서 게임플레이 태그를 정의하는 특성입니다.
    /// </summary>
    /// <example>
    /// <see cref="GameplayTagAttribute"/>로 어셈블리에 태그를 선언할 수 있습니다.
    ///
    /// 사용 예:
    /// <code>
    /// [assembly: GameplayTag("Character.Invincible", "Indicates that a character is invincible.")]
    /// [assembly: GameplayTag("Weapon.Reloading", "Indicates that a weapon is in the process of reloading.")]
    /// [assembly: GameplayTag("Interaction.Locked", "Indicates that an interaction is currently locked.")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class GameplayTagAttribute : Attribute
    {
        /// <summary>게임 내에서 태그를 식별하는 이름입니다.</summary>
        public string TagName { get; set; }

        /// <summary>에디터에 표시되는 태그 설명입니다.</summary>
        public string Description { get; set; }

        /// <summary>태그에 연결된 플래그입니다.</summary>
        public GameplayTagFlags Flags { get; set; }

        /// <summary>새 <see cref="GameplayTagAttribute"/> 인스턴스를 초기화합니다.</summary>
        /// <param name="tagName">태그 이름입니다.</param>
        /// <param name="description">태그 설명(선택)입니다.</param>
        /// <param name="flags">태그 플래그(선택)입니다.</param>
        public GameplayTagAttribute(string tagName, string description = null, GameplayTagFlags flags = GameplayTagFlags.None)
        {
            TagName = tagName;
            Description = description;
            Flags = flags;
        }
    }
}
