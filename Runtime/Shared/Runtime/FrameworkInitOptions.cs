namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    /// <summary>액터/모듈 Init 시 전역 Catalog 등록 여부.</summary>
    public sealed class FrameworkInitOptions
    {
        public bool RegisterGlobalCatalogs { get; set; } = true;

        public static FrameworkInitOptions Default { get; } = new();

        public static FrameworkInitOptions SkipGlobalCatalogs { get; } = new() { RegisterGlobalCatalogs = false };
    }
}
