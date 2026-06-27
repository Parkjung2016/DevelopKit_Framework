using System.IO;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class FileGameplayTagSourceTests
  {
    private FileGameplayTagSource source;

    [SetUp]
    public void SetUp()
    {
      source = GameplayTagTestFixtures.CreateFileSource(
        @"{
  ""FileTest"": {},
  ""FileTest.Ability"": {},
  ""FileTest.Ability.Jump"": { ""Comment"": ""Jump"" },
  ""FileTest.Ability.Roll"": { ""Comment"": ""Roll"" }
}");
      GameplayTagManager.InitializeForTests(source);
    }

    [TearDown]
    public void TearDown() => GameplayTagTestFixtures.RestoreManager();

    [Test]
    public void RegisterTags_LoadsTagsIntoManager()
    {
      Assert.IsTrue(GameplayTagManager.RequestTag("FileTest.Ability.Jump", out GameplayTag tag));
      Assert.AreEqual("Jump", tag.Description);
    }

    [Test]
    public void TryAddTag_AddsTagToJson()
    {
      bool added = source.TryAddTag("FileTest.NewTag", "new", out string error);

      Assert.IsTrue(added, error);
      Assert.IsTrue(source.ContainsTag("FileTest.NewTag"));
      Assert.IsTrue(source.ContainsTag("FileTest"));
    }

    [Test]
    public void TryAddTag_DuplicateTag_ReturnsFalse()
    {
      Assert.IsFalse(source.TryAddTag("FileTest.Ability.Jump", null, out _));
    }

    [Test]
    public void TryUpdateComment_UpdatesDescription()
    {
      bool updated = source.TryUpdateComment("FileTest.Ability.Jump", "Updated jump", out string error);

      Assert.IsTrue(updated, error);
      GameplayTagManager.InitializeForTests(source);
      Assert.AreEqual("Updated jump", GameplayTagManager.RequestTag("FileTest.Ability.Jump").Description);
    }

    [Test]
    public void TryRenameTag_RenamesTagAndChildren()
    {
      bool renamed = source.TryRenameTag("FileTest.Ability", "FileTest.Skill", out string error);

      Assert.IsTrue(renamed, error);
      Assert.IsFalse(source.ContainsTag("FileTest.Ability.Jump"));
      Assert.IsTrue(source.ContainsTag("FileTest.Skill.Jump"));
    }

    [Test]
    public void TryDeleteTag_TagOnly_PromotesChildren()
    {
      bool deleted = source.TryDeleteTag("FileTest.Ability", GameplayTagDeleteMode.TagOnly, out string error);

      Assert.IsTrue(deleted, error);
      Assert.IsFalse(source.ContainsTag("FileTest.Ability"));
      Assert.IsFalse(source.ContainsTag("FileTest.Ability.Jump"));
      Assert.IsTrue(source.ContainsTag("Jump"));
      Assert.IsTrue(source.ContainsTag("Roll"));
    }

    [Test]
    public void TryDeleteTag_Hierarchy_RemovesSubtree()
    {
      bool deleted = source.TryDeleteTag("FileTest.Ability", GameplayTagDeleteMode.Hierarchy, out string error);

      Assert.IsTrue(deleted, error);
      Assert.IsFalse(source.ContainsTag("FileTest.Ability.Jump"));
      Assert.IsFalse(source.ContainsTag("FileTest.Ability.Roll"));
    }

    [Test]
    public void TryMoveTagHierarchyTo_MovesTagsBetweenFiles()
    {
      FileGameplayTagSource target = GameplayTagTestFixtures.CreateFileSource("{}", "TargetTags.json");

      bool moved = source.TryMoveTagHierarchyTo(target, "FileTest.Ability", out string error);

      Assert.IsTrue(moved, error);
      Assert.IsFalse(source.ContainsTag("FileTest.Ability.Jump"));
      Assert.IsTrue(target.ContainsTag("FileTest.Ability.Jump"));
    }

    [Test]
    public void TryValidateDelete_TagOnly_WhenPromotionConflicts_ReturnsFalse()
    {
      FileGameplayTagSource conflictSource = GameplayTagTestFixtures.CreateFileSource(
        @"{
  ""FileTest"": {},
  ""FileTest.Ability"": {},
  ""FileTest.Ability.Jump"": {},
  ""Jump"": {}
}");

      bool valid = conflictSource.TryValidateDelete("FileTest.Ability", GameplayTagDeleteMode.TagOnly, out string error);

      Assert.IsFalse(valid);
      Assert.IsNotEmpty(error);
    }
  }
}
