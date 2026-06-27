using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagSourceUtilityTests : GameplayTagTestBase
  {
    [Test]
    public void GetPrimaryFileSourceName_ReturnsJsonFileName()
    {
      GameplayTag tag = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      string fileName = GameplayTagSourceUtility.GetPrimaryFileSourceName(tag);

      Assert.AreEqual("TestTags.json", fileName);
    }

    [Test]
    public void TryGetFileSourcePath_ReturnsAbsolutePath()
    {
      GameplayTag tag = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      bool found = GameplayTagSourceUtility.TryGetFileSourcePath(tag, out string filePath);

      Assert.IsTrue(found);
      Assert.IsTrue(filePath.EndsWith("TestTags.json"));
    }

    [Test]
    public void IsTagInFile_MatchesSourceFileName()
    {
      GameplayTag tag = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      Assert.IsTrue(GameplayTagSourceUtility.IsTagInFile(tag, "TestTags.json"));
      Assert.IsFalse(GameplayTagSourceUtility.IsTagInFile(tag, "Other.json"));
    }

    [Test]
    public void SharesFileSource_SameFileTags_ReturnsTrue()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");

      Assert.IsTrue(GameplayTagSourceUtility.SharesFileSource(jump, roll));
    }
  }
}
