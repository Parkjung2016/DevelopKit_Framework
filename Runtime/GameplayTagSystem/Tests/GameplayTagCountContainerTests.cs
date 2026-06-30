using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagCountContainerTests : GameplayTagTestBase
  {
    private GameplayTag jump;
    private GameplayTag ability;

    [SetUp]
    public new void SetUp()
    {
      jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      ability = GameplayTagTestFixtures.Tag("Test.Ability");
    }

    [Test]
    public void AddTag_IncrementsExplicitAndImplicitCounts()
    {
      GameplayTagCountContainer container = new();
      container.AddTag(jump);

      Assert.AreEqual(1, container.GetExplicitTagCount(jump));
      Assert.AreEqual(1, container.GetTagCount(jump));
      Assert.AreEqual(1, container.GetTagCount(ability));
    }

    [Test]
    public void AddTag_Twice_IncrementsCounts()
    {
      GameplayTagCountContainer container = new();
      container.AddTag(jump);
      container.AddTag(jump);

      Assert.AreEqual(2, container.GetExplicitTagCount(jump));
      Assert.AreEqual(2, container.GetTagCount(jump));
      Assert.AreEqual(2, container.GetTagCount(ability));
    }

    [Test]
    public void RemoveTag_DecrementsCounts()
    {
      GameplayTagCountContainer container = new();
      container.AddTag(jump);
      container.AddTag(jump);
      container.RemoveTag(jump);

      Assert.AreEqual(1, container.GetExplicitTagCount(jump));
      Assert.AreEqual(1, container.GetTagCount(ability));
    }

    [Test]
    public void RemoveTag_LastReference_RemovesImplicitTags()
    {
      GameplayTagCountContainer container = new();
      container.AddTag(jump);
      container.RemoveTag(jump);

      Assert.AreEqual(0, container.GetTagCount(jump));
      Assert.AreEqual(0, container.GetTagCount(ability));
      Assert.IsTrue(container.IsEmpty);
    }

    [Test]
    public void RegisterTagEventCallback_FiresOnAddAndRemove()
    {
      GameplayTagCountContainer container = new();
      List<(GameplayTag tag, int count)> events = new();

      container.RegisterTagEventCallback(
        jump,
        GameplayTagEventType.NewOrRemoved,
        (tag, count) => events.Add((tag, count)));

      container.AddTag(jump);
      container.RemoveTag(jump);

      Assert.AreEqual(2, events.Count);
      Assert.AreEqual((jump, 1), events[0]);
      Assert.AreEqual((jump, 0), events[1]);
    }

    [Test]
    public void RegisterTagEventCallback_AnyCountChange_FiresOnEachIncrement()
    {
      GameplayTagCountContainer container = new();
      List<int> counts = new();

      container.RegisterTagEventCallback(
        jump,
        GameplayTagEventType.AnyCountChange,
        (_, count) => counts.Add(count));

      container.AddTag(jump);
      container.AddTag(jump);
      container.RemoveTag(jump);

      CollectionAssert.AreEqual(new[] { 1, 2, 1 }, counts);
    }

    [Test]
    public void Clear_RemovesAllTagsAndFiresRemoveCallbacks()
    {
      GameplayTagCountContainer container = new();
      int removeEvents = 0;

      container.OnAnyTagNewOrRemove += (_, count) =>
      {
        if (count == 0)
          removeEvents++;
      };

      container.AddTag(jump);
      container.Clear();

      Assert.IsTrue(container.IsEmpty);
      Assert.Greater(removeEvents, 0);
    }
  }
}
