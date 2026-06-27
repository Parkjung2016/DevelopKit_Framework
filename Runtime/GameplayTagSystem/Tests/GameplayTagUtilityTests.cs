using System;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagUtilityTests
  {
    [Test]
    public void GetHeirarchyNames_ReturnsFullChain()
    {
      string[] names = GameplayTagUtility.GetHeirarchyNames("A.B.C");

      Assert.AreEqual(new[] { "A", "A.B", "A.B.C" }, names);
    }

    [Test]
    public void TryGetParentName_ReturnsParent()
    {
      Assert.IsTrue(GameplayTagUtility.TryGetParentName("A.B.C", out string parent));
      Assert.AreEqual("A.B", parent);
    }

    [Test]
    public void TryGetParentName_RootTag_ReturnsFalse()
    {
      Assert.IsFalse(GameplayTagUtility.TryGetParentName("Root", out _));
    }

    [Test]
    public void GetHeirarchyLevelFromName_CountsSegments()
    {
      Assert.AreEqual(1, GameplayTagUtility.GetHeirarchyLevelFromName("Root"));
      Assert.AreEqual(3, GameplayTagUtility.GetHeirarchyLevelFromName("A.B.C"));
    }

    [Test]
    public void GetLabel_ReturnsLastSegment()
    {
      Assert.AreEqual("Jump", GameplayTagUtility.GetLabel("Ability.Jump"));
      Assert.AreEqual("Ability", GameplayTagUtility.GetLabel("Ability"));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("Valid.Tag_1", true)]
    [TestCase("Invalid..Tag", false)]
    [TestCase(".Leading", false)]
    [TestCase("Trailing.", false)]
    public void IsNameValid_ValidatesFormat(string name, bool expectedValid)
    {
      bool isValid = GameplayTagUtility.IsNameValid(name, out string errorMessage);

      Assert.AreEqual(expectedValid, isValid);
      if (!expectedValid)
        Assert.IsNotEmpty(errorMessage);
    }

    [Test]
    public void ValidateName_InvalidName_Throws()
    {
      Assert.Throws<ArgumentException>(() => GameplayTagUtility.ValidateName("bad..name"));
    }
  }
}
