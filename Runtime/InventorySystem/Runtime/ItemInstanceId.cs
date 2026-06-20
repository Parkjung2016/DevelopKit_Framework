namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct ItemInstanceId
    {
        public long Value { get; }

        public bool IsValid => Value > 0;

        public ItemInstanceId(long value) => Value = value;

        public static ItemInstanceId None => default;

        public override string ToString() => IsValid ? Value.ToString() : "None";
    }
}
