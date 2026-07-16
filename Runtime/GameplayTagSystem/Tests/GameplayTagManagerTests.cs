using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagManagerTests : GameplayTagTestBase
  {
    [Test]
    public void RequestTag_KnownTag_ReturnsValidTag()
    {
      GameplayTag tag = GameplayTagManager.RequestTag("Test.Ability.Jump", logWarningIfNotFound: false);

      Assert.IsTrue(tag.IsValid);
      Assert.AreEqual("Test.Ability.Jump", tag.Name);
      Assert.AreEqual("Jump ability", tag.Description);
    }

    [Test]
    public void RequestTag_UnknownTag_ReturnsInvalidTag()
    {
      GameplayTag tag = GameplayTagManager.RequestTag("Missing.Tag", logWarningIfNotFound: false);

      Assert.IsFalse(tag.IsValid);
      Assert.AreEqual("Missing.Tag", tag.Name);
    }

    [Test]
    public void RequestTag_OutParameter_ReturnsFalseForMissingTag()
    {
      bool found = GameplayTagManager.RequestTag("Missing.Tag", out GameplayTag tag);

      Assert.IsFalse(found);
      Assert.IsFalse(tag.IsValid);
    }

    [Test]
    public void RequestTag_EmptyName_ReturnsNone()
    {
      GameplayTag tag = GameplayTagManager.RequestTag(string.Empty, logWarningIfNotFound: false);

      Assert.IsTrue(tag.IsNone);
    }

    [Test]
    public void GetTagFromRuntimeIndex_ReturnsRegisteredTag()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag fromIndex = GameplayTagManager.GetTagFromRuntimeIndex(jump.RuntimeIndex);

      Assert.AreEqual(jump, fromIndex);
    }

    [Test]
    public void GetTagFromRuntimeIndex_InvalidIndex_ReturnsNone()
    {
      Assert.IsTrue(GameplayTagManager.GetTagFromRuntimeIndex(-1).IsNone);
      Assert.IsTrue(GameplayTagManager.GetTagFromRuntimeIndex(99999).IsNone);
    }

    [Test]
    public void GetAllTags_ExcludesNoneTag()
    {
      foreach (GameplayTag tag in GameplayTagManager.GetAllTags())
        Assert.IsFalse(tag.IsNone);
    }

    [Test]
    public void ReloadTags_SetsHasBeenReloadedFlag()
    {
      GameplayTagManager.ReloadTags();

      Assert.IsTrue(GameplayTagManager.HasBeenReloaded);
    }

    [Test]
    public void Reload_ExistingTagResolvesCurrentDefinition()
    {
      GameplayTag beforeReload = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      int previousIndex = beforeReload.RuntimeIndex;
      FileGameplayTagSource source = GameplayTagTestFixtures.CreateFileSource(
        @"{
  ""BeforeTest"": {},
  ""Test"": {},
  ""Test.Ability"": {},
  ""Test.Ability.Jump"": { ""Comment"": ""Reloaded"" }
}");

      GameplayTagManager.ReloadForTests(source);
      GameplayTag current = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      Assert.IsTrue(beforeReload.IsValid);
      Assert.AreEqual(current, beforeReload);
      Assert.AreEqual(current.RuntimeIndex, beforeReload.RuntimeIndex);
      Assert.AreNotEqual(previousIndex, beforeReload.RuntimeIndex);
      Assert.AreEqual("Reloaded", beforeReload.Description);
    }

    [Test]
    public void Reload_RemovedTagBecomesInvalidWithoutChangingItsName()
    {
      GameplayTag removed = GameplayTagTestFixtures.Tag("Test.Ability.Roll");
      FileGameplayTagSource source = GameplayTagTestFixtures.CreateFileSource(
        @"{
  ""Test"": {},
  ""Test.Ability"": {},
  ""Test.Ability.Jump"": {}
}");

      GameplayTagManager.ReloadForTests(source);

      Assert.IsFalse(removed.IsValid);
      Assert.IsFalse(removed.IsNone);
      Assert.AreEqual("Test.Ability.Roll", removed.Name);
      Assert.AreEqual(-1, removed.RuntimeIndex);
    }  }
}
