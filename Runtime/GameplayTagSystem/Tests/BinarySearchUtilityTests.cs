using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class BinarySearchUtilityTests
  {
    [Test]
    public void Search_FindsExistingValue()
    {
      List<int> values = new() { 1, 3, 5, 7 };

      Assert.AreEqual(2, BinarySearchUtility.Search(values, 5));
    }

    [Test]
    public void Search_MissingValue_ReturnsBitwiseComplementInsertIndex()
    {
      List<int> values = new() { 1, 3, 5, 7 };

      Assert.AreEqual(~2, BinarySearchUtility.Search(values, 4));
    }

    [Test]
    public void Search_EmptyList_ReturnsZeroComplement()
    {
      List<int> values = new();

      Assert.AreEqual(~0, BinarySearchUtility.Search(values, 10));
    }
  }
}
