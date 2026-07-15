namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>결정론 재생이나 저장에 사용하는 난수 생성기 상태입니다.</summary>
    public readonly struct RandomSnapshot
    {
        public RandomSnapshot(ulong state, ulong stream)
        {
            State = state;
            Stream = stream;
        }

        public ulong State { get; }
        public ulong Stream { get; }
    }
}
