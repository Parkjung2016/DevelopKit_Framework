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
    public void RegisterTag_DuplicateName_IsRejected()
    {
      GameplayTagRegistrationContext context = new();
      TestGameplayTagSource sourceA = new("A");
      TestGameplayTagSource sourceB = new("B");

      Assert.IsTrue(context.RegisterTag("Tag.One", "first", GameplayTagFlags.None, sourceA));
      Assert.IsFalse(context.RegisterTag("Tag.One", "duplicate", GameplayTagFlags.None, sourceB));

      GameplayTagDefinition[] definitions = context.GenerateDefinitions();
      GameplayTagDefinition definition = definitions.First(def => def.TagName == "Tag.One");

      Assert.AreEqual("first", definition.Description);
      Assert.AreSame(sourceA, definition.Source);
      Assert.AreEqual(1, context.GetRegistrationErrors().Count);
    }

    [Test]
    public void GenerateDefinitions_ConnectsHierarchyAcrossDifferentSources()
    {
      GameplayTagRegistrationContext context = new();
      TestGameplayTagSource parentSource = new("Parent");
      TestGameplayTagSource childSource = new("Child");

      context.RegisterTag("Tag", null, GameplayTagFlags.None, parentSource);
      context.RegisterTag("Tag.Child", null, GameplayTagFlags.None, childSource);

      GameplayTagDefinition[] definitions = context.GenerateDefinitions();
      GameplayTagDefinition parent = definitions.First(def => def.TagName == "Tag");
      GameplayTagDefinition child = definitions.First(def => def.TagName == "Tag.Child");

      Assert.AreSame(parent, child.ParentTagDefinition);
      Assert.AreSame(childSource, child.Source);
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
