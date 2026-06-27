using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagContainerSetOperationTests : GameplayTagTestBase
  {
    private GameplayTag jump;
    private GameplayTag roll;
    private GameplayTag stunned;

    [SetUp]
    public void SetUp()
    {
      jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");
      stunned = GameplayTagTestFixtures.Tag("Test.Status.Stunned");
    }

    [Test]
    public void Intersection_ReturnsSharedTags()
    {
      GameplayTagContainer left = new();
      left.AddTag(jump);
      left.AddTag(roll);

      GameplayTagContainer right = new();
      right.AddTag(roll);
      right.AddTag(stunned);

      GameplayTagContainer intersection = GameplayTagContainer.Intersection(left, right);

      Assert.AreEqual(1, intersection.ExplicitTagCount);
      Assert.IsTrue(intersection.HasTagExact(roll));
      Assert.IsFalse(intersection.HasTagExact(jump));
    }

    [Test]
    public void Union_MergesExplicitTags()
    {
      GameplayTagContainer left = new();
      left.AddTag(jump);

      GameplayTagContainer right = new();
      right.AddTag(roll);

      GameplayTagContainer union = GameplayTagContainer.Union(left, right);

      Assert.AreEqual(2, union.ExplicitTagCount);
      Assert.IsTrue(union.HasTagExact(jump));
      Assert.IsTrue(union.HasTagExact(roll));
    }

    [Test]
    public void HasAny_DetectsOverlap()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);

      GameplayTagContainer other = new();
      other.AddTag(roll);
      other.AddTag(jump);

      Assert.IsTrue(container.HasAny(other));
    }

    [Test]
    public void HasAll_RequiresAllRequiredTags()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);
      container.AddTag(roll);

      GameplayTagContainer required = new();
      required.AddTag(jump);

      Assert.IsTrue(container.HasAll(required));
      Assert.IsFalse(required.HasAll(container));
    }

    [Test]
    public void HasAllExact_RequiresExplicitTagsOnly()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);

      GameplayTagContainer required = new();
      required.AddTag(GameplayTagTestFixtures.Tag("Test.Ability"));

      Assert.IsTrue(container.HasAll(required));
      Assert.IsFalse(container.HasAllExact(required));
    }
  }
}
