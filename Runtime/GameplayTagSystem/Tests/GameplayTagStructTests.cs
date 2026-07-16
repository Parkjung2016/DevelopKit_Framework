using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagStructTests : GameplayTagTestBase
  {
    [Test]
    public void ImplicitConversion_FromString_Works()
    {
      GameplayTag tag = "Test.Ability.Jump";

      Assert.IsTrue(tag.IsValid);
      Assert.AreEqual("Test.Ability.Jump", tag.Name);
    }

    [Test]
    public void HierarchyTags_ContainsParents()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      Assert.GreaterOrEqual(jump.HierarchyTags.Length, 3);
      Assert.IsTrue(jump.IsChildOf(GameplayTagTestFixtures.Tag("Test.Ability")));
      Assert.IsTrue(jump.IsChildOf(GameplayTagTestFixtures.Tag("Test")));
      Assert.IsTrue(GameplayTagTestFixtures.Tag("Test.Ability").IsParentOf(jump));
    }

    [Test]
    public void ParentTag_ReturnsImmediateParent()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      Assert.AreEqual("Test.Ability", jump.ParentTag.Name);
    }

    [Test]
    public void Label_ReturnsLastSegment()
    {
      Assert.AreEqual("Jump", GameplayTagTestFixtures.Tag("Test.Ability.Jump").Label);
    }

    [Test]
    public void Equals_SameTag_ReturnsTrue()
    {
      GameplayTag a = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag b = GameplayTagTestFixtures.Tag("Test.Ability.Jump");

      Assert.IsTrue(a.Equals(b));
      Assert.IsTrue(a == b);
      Assert.IsFalse(a != b);
    }

    [Test]
    public void Equals_DifferentTags_ReturnsFalse()
    {
      GameplayTag jump = GameplayTagTestFixtures.Tag("Test.Ability.Jump");
      GameplayTag roll = GameplayTagTestFixtures.Tag("Test.Ability.Roll");

      Assert.IsFalse(jump.Equals(roll));
    }

    [Test]
    public void ToString_None_ReturnsNoneLabel()
    {
      Assert.AreEqual("<None>", GameplayTag.None.ToString());
    }

    [Test]
    public void None_EqualsDefaultValue()
    {
      GameplayTag defaultTag = default;

      Assert.IsTrue(GameplayTag.None.Equals(defaultTag));
      Assert.IsTrue(GameplayTag.None == defaultTag);
      Assert.AreEqual(GameplayTag.None.GetHashCode(), defaultTag.GetHashCode());
    }

    [Test]
    public void MissingTags_WithSameName_AreEqual()
    {
      GameplayTag first = GameplayTagManager.RequestTag("Missing.Tag", false);
      GameplayTag second = GameplayTagManager.RequestTag("Missing.Tag", false);

      Assert.IsFalse(first.IsNone);
      Assert.IsFalse(first.IsValid);
      Assert.AreEqual(first, second);
      Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Test]
    public void MissingTags_WithDifferentNames_AreNotEqual()
    {
      GameplayTag first = GameplayTagManager.RequestTag("Missing.First", false);
      GameplayTag second = GameplayTagManager.RequestTag("Missing.Second", false);

      Assert.AreNotEqual(first, second);
    }  }
}
