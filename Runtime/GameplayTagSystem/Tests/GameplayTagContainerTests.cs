using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagContainerTests : GameplayTagTestBase
  {
    private GameplayTag jump;
    private GameplayTag roll;
    private GameplayTag ability;
    private GameplayTag stunned;

    [SetUp]
    public new void SetUp()
    {
      jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");
      ability = GameplayTagTestFixtures.Tag("Test.Ability");
      stunned = GameplayTagTestFixtures.Tag("Test.Status.Stunned");
    }

    [Test]
    public void AddTag_IncludesImplicitParents()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);

      Assert.AreEqual(1, container.ExplicitTagCount);
      Assert.Greater(container.TagCount, container.ExplicitTagCount);
      Assert.IsTrue(container.HasTag(ability));
      Assert.IsTrue(container.HasTag(jump));
      Assert.IsTrue(container.HasTagExact(jump));
      Assert.IsFalse(container.HasTagExact(ability));
    }

    [Test]
    public void AddTag_DuplicateExplicitTag_IsIgnored()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);
      container.AddTag(jump);

      Assert.AreEqual(1, container.ExplicitTagCount);
    }

    [Test]
    public void RemoveTag_RemovesExplicitAndRebuildsImplicit()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);
      container.RemoveTag(jump);

      Assert.IsTrue(container.IsEmpty);
      Assert.IsFalse(container.HasTag(jump));
    }

    [Test]
    public void AddTags_CopiesFromOtherContainer()
    {
      GameplayTagContainer source = new();
      source.AddTag(jump);
      source.AddTag(roll);

      GameplayTagContainer destination = new();
      destination.AddTags(source);

      Assert.AreEqual(2, destination.ExplicitTagCount);
      Assert.IsTrue(destination.HasTag(jump));
      Assert.IsTrue(destination.HasTag(roll));
    }

    [Test]
    public void Clone_CreatesEqualContainer()
    {
      GameplayTagContainer original = new();
      original.AddTag(jump);
      original.AddTag(stunned);

      GameplayTagContainer clone = original.Clone();

      Assert.AreEqual(original.ExplicitTagCount, clone.ExplicitTagCount);
      Assert.IsTrue(clone.HasAll(original));
    }

    [Test]
    public void CollectionInitializer_AddsTags()
    {
      GameplayTagContainer container = new()
      {
        jump,
        roll
      };

      Assert.AreEqual(2, container.ExplicitTagCount);
    }

    [Test]
    public void GetChildTags_ReturnsDirectChildren()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);
      container.AddTag(roll);

      List<GameplayTag> children = new();
      container.GetChildTags(ability, children);

      Assert.AreEqual(2, children.Count);
      CollectionAssert.Contains(children, jump);
      CollectionAssert.Contains(children, roll);
    }

    [Test]
    public void GetDiffExplicitTags_DetectsAddedAndRemoved()
    {
      GameplayTagContainer left = new();
      left.AddTag(jump);

      GameplayTagContainer right = new();
      right.AddTag(roll);

      List<GameplayTag> added = new();
      List<GameplayTag> removed = new();
      left.GetDiffExplicitTags(right, added, removed);

      CollectionAssert.Contains(added, jump);
      CollectionAssert.Contains(removed, roll);
    }

    [Test]
    public void OnAfterDeserialize_RestoresTagsFromSerializedNames()
    {
      GameplayTagContainer container = new();
      container.AddTag(jump);
      container.FillSerializedTags();

      List<string> serialized = container.GetType()
        .GetField("serializedExplicitTags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?.GetValue(container) as List<string>;

      Assert.IsNotNull(serialized);
      Assert.Contains("Test.Ability.Jump", serialized);

      GameplayTagContainer restored = new();
      restored.GetType()
        .GetField("serializedExplicitTags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?.SetValue(restored, new List<string>(serialized));

      ((UnityEngine.ISerializationCallbackReceiver)restored).OnAfterDeserialize();

      Assert.IsTrue(restored.HasTagExact(jump));
    }
  }
}
