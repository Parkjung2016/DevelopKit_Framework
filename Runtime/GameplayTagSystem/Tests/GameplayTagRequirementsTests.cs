using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagRequirementsTests : GameplayTagTestBase
  {
    [Test]
    public void Matches_WhenRequiredTagsPresent_ReturnsTrue()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");

      GameplayTagContainer required = new();
      required.AddTag(jump);

      GameplayTagContainer forbidden = new();
      forbidden.AddTag(roll);

      GameplayTagContainer owner = new();
      owner.AddTag(jump);

      GameplayTagRequirements requirements = new(forbidden, required);

      Assert.IsTrue(requirements.Matches(owner));
    }

    [Test]
    public void Matches_WhenForbiddenTagPresent_ReturnsFalse()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");

      GameplayTagContainer required = new();
      GameplayTagContainer forbidden = new();
      forbidden.AddTag(roll);

      GameplayTagContainer owner = new();
      owner.AddTag(jump);
      owner.AddTag(roll);

      GameplayTagRequirements requirements = new(forbidden, required);

      Assert.IsFalse(requirements.Matches(owner));
    }

    [Test]
    public void Matches_StaticAndDynamic_ContainersAreCombined()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");

      GameplayTagContainer required = new();
      required.AddTag(jump);
      required.AddTag(roll);

      GameplayTagContainer staticContainer = new();
      staticContainer.AddTag(jump);

      GameplayTagContainer dynamicContainer = new();
      dynamicContainer.AddTag(roll);

      GameplayTagRequirements requirements = new(new GameplayTagContainer(), required);

      Assert.IsTrue(requirements.Matches(staticContainer, dynamicContainer));
    }

    [Test]
    public void IsEmpty_WhenBothContainersEmpty_ReturnsTrue()
    {
      GameplayTagRequirements requirements = new(new GameplayTagContainer(), new GameplayTagContainer());

      Assert.IsTrue(requirements.IsEmpty);
    }
  }
}
