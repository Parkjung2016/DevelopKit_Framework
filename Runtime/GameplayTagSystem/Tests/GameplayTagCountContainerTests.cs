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

    [Test]
    public void Clear_FiresAnyCountChangeCallbacks()
    {
      GameplayTagCountContainer container = new();
      int tagCallbackCount = -1;
      int globalCallbackCount = -1;

      container.RegisterTagEventCallback(
        jump,
        GameplayTagEventType.AnyCountChange,
        (_, count) => tagCallbackCount = count);
      container.OnAnyTagCountChange += (tag, count) =>
      {
        if (tag == jump)
          globalCallbackCount = count;
      };

      container.AddTag(jump);
      container.Clear();

      Assert.AreEqual(0, tagCallbackCount);
      Assert.AreEqual(0, globalCallbackCount);
    }
    [Test]
    public void Bindings_InvokeImmediatelyAndTrackTagState()
    {
      GameplayTagCountContainer container = new();
      GameplayTagBindings bindings = new(container);
      List<bool> states = new();

      bindings.Bind(jump, states.Add);
      container.AddTag(jump);
      container.RemoveTag(jump);

      CollectionAssert.AreEqual(new[] { false, true, false }, states);
      bindings.Dispose();
    }

    [Test]
    public void Binding_DisposeStopsOnlyThatCallback()
    {
      GameplayTagCountContainer container = new();
      GameplayTagBindings bindings = new(container);
      int firstCalls = 0;
      int secondCalls = 0;

      System.IDisposable first = bindings.Bind(jump, _ => firstCalls++, invokeImmediately: false);
      bindings.Bind(jump, _ => secondCalls++, invokeImmediately: false);
      first.Dispose();

      container.AddTag(jump);

      Assert.AreEqual(0, firstCalls);
      Assert.AreEqual(1, secondCalls);
      bindings.Dispose();
    }
    [Test]
    public void Reload_CountContainerRebuildsCountsAndKeepsCallbacks()
    {
      GameplayTagCountContainer container = new();
      int callbackCount = -1;
      container.AddTag(jump);
      container.AddTag(jump);
      container.RegisterTagEventCallback(
        jump,
        GameplayTagEventType.AnyCountChange,
        (_, count) => callbackCount = count);

      GameplayTagManager.ReloadForTests(GameplayTagTestFixtures.CreateFileSource(
        @"{
  ""BeforeTest"": {},
  ""Test"": {},
  ""Test.Ability"": {},
  ""Test.Ability.Jump"": {}
}"));

      Assert.AreEqual(2, container.GetTagCount(jump));
      Assert.AreEqual(2, container.GetTagCount(GameplayTagTestFixtures.Tag("Test.Ability")));

      container.RemoveTag(jump);
      Assert.AreEqual(1, callbackCount);
    }
    [Test]
    public void UpdateTags_RemovesAndAddsInSingleOperation()
    {
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");
      GameplayTagCountContainer container = new();
      container.AddTag(jump);

      GameplayTagContainer added = new();
      added.AddTag(roll);
      GameplayTagContainer removed = new();
      removed.AddTag(jump);

      container.UpdateTags(added, removed);

      Assert.AreEqual(0, container.GetTagCount(jump));
      Assert.AreEqual(1, container.GetTagCount(roll));
      Assert.AreEqual(1, container.GetTagCount(ability));
    }
  }
}

