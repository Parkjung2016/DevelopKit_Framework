using System;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>어셈블리에 게임플레이 태그를 선언합니다.</summary>
    /// <example>
    /// <code>
    /// [assembly: GameplayTag("Character.Invincible", "캐릭터가 무적 상태입니다.")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class GameplayTagAttribute : Attribute
    {
        public GameplayTagAttribute(string tagName, string description = null)
        {
            TagName = tagName;
            Description = description;
        }

        public string TagName { get; }
        public string Description { get; }
    }
}