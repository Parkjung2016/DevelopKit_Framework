using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Tests
{
  internal static class GameplayTagTestFixtures
  {
    public const string StandardJson =
      @"{
  ""Test"": {},
  ""Test.Ability"": {},
  ""Test.Ability.Jump"": { ""Comment"": ""Jump ability"" },
  ""Test.Ability.Roll"": { ""Comment"": ""Roll ability"" },
  ""Test.Status"": {},
  ""Test.Status.Stunned"": { ""Comment"": ""Stunned"" }
}";

    private static readonly List<string> TempDirectories = new();

    public static FileGameplayTagSource CreateFileSource(string json, string fileName = "TestTags.json")
    {
      string directory = Path.Combine(Path.GetTempPath(), "PJDevGameplayTagTests", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(directory);
      TempDirectories.Add(directory);

      string filePath = Path.Combine(directory, fileName);
      File.WriteAllText(filePath, json);
      FileGameplayTagSource source = new(filePath);
      if (!source.TryLoad())
        throw new InvalidOperationException($"Failed to load test tag file: {filePath}");

      return source;
    }

    public static void LoadStandardTags()
    {
      FileGameplayTagSource source = CreateFileSource(StandardJson);
      GameplayTagManager.InitializeForTests(source);
    }

    public static GameplayTag Tag(string name) => GameplayTagManager.RequestTag(name, logWarningIfNotFound: false);

    public static void RestoreManager()
    {
      GameplayTagManager.RestoreDefaultInitialization();
      CleanupTempDirectories();
    }

    public static void CleanupTempDirectories()
    {
      foreach (string directory in TempDirectories)
      {
        try
        {
          if (Directory.Exists(directory))
            Directory.Delete(directory, true);
        }
        catch
        {
          // 테스트 정리 실패는 무시합니다.
        }
      }

      TempDirectories.Clear();
    }
  }

  public abstract class GameplayTagTestBase
  {
    [NUnit.Framework.SetUp]
    public void SetUp() => GameplayTagTestFixtures.LoadStandardTags();

    [NUnit.Framework.TearDown]
    public void TearDown() => GameplayTagTestFixtures.RestoreManager();
  }
}
