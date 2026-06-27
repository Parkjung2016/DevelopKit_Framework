using System.Linq;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  [TestFixture]
  public sealed class GameplayTagRegistrationContextTests
  {
    [Test]
    public void GenerateDefinitions_CreatesMissingParents()
    {
      GameplayTagRegistrationContext context = new();
      TestGameplayTagSource source = new("TestSource");

      context.RegisterTag("A.B.C", "leaf", GameplayTagFlags.None, source);
      GameplayTagDefinition[] definitions = context.GenerateDefinitions();

      Assert.IsTrue(definitions.Any(definition => definition.TagName == "A"));
      Assert.IsTrue(definitions.Any(definition => definition.TagName == "A.B"));
      Assert.IsTrue(definitions.Any(definition => definition.TagName == "A.B.C"));
    }

    [Test]
    public void RegisterTag_InvalidName_RecordsError()
    {
      GameplayTagRegistrationContext context = new();
      TestGameplayTagSource source = new("TestSource");

      context.RegisterTag("bad..tag", null, GameplayTagFlags.None, source);
      GameplayTagDefinition[] definitions = context.GenerateDefinitions();

      Assert.IsTrue(context.GetRegistrationErrors().Any());
      Assert.IsFalse(definitions.Any(definition => definition.TagName == "bad..tag"));
    }

    [Test]
    public void RegisterTag_DuplicateName_MergesDescription()
    {
      GameplayTagRegistrationContext context = new();
      TestGameplayTagSource sourceA = new("A");
      TestGameplayTagSource sourceB = new("B");

      context.RegisterTag("Tag.One", null, GameplayTagFlags.None, sourceA);
      context.RegisterTag("Tag.One", "merged", GameplayTagFlags.None, sourceB);

      GameplayTagDefinition[] definitions = context.GenerateDefinitions();
      GameplayTagDefinition definition = definitions.First(def => def.TagName == "Tag.One");

      Assert.AreEqual("merged", definition.Description);
      Assert.AreEqual(2, definition.SourceCount);
    }

    private sealed class TestGameplayTagSource : IGameplayTagSource
    {
      public string Name { get; }

      public TestGameplayTagSource(string name) => Name = name;

      public void RegisterTags(GameplayTagRegistrationContext context)
      {
      }
    }
  }
}
