using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>틱별 입력·명령을 순서대로 보관합니다. 록스텝·리플레이에 사용합니다.</summary>
    public sealed class SimulationCommandQueue<TCommand> where TCommand : struct
    {
        private readonly SortedDictionary<int, List<TCommand>> commandsByTick = new();

        public void Enqueue(int tick, TCommand command)
        {
            if (!commandsByTick.TryGetValue(tick, out List<TCommand> list))
            {
                list = new List<TCommand>();
                commandsByTick[tick] = list;
            }

            list.Add(command);
        }

        public void Clear() => commandsByTick.Clear();

        public void ClearFromTick(int tick)
        {
            List<int> removeKeys = null;
            foreach (KeyValuePair<int, List<TCommand>> pair in commandsByTick)
            {
                if (pair.Key < tick)
                    continue;

                removeKeys ??= new List<int>();
                removeKeys.Add(pair.Key);
            }

            if (removeKeys == null)
                return;

            for (int i = 0; i < removeKeys.Count; i++)
                commandsByTick.Remove(removeKeys[i]);
        }

        public ReadOnlySpan<TCommand> GetCommands(int tick)
        {
            if (!commandsByTick.TryGetValue(tick, out List<TCommand> list) || list.Count == 0)
                return ReadOnlySpan<TCommand>.Empty;

            return list.ToArray();
        }

        public int GetCommandCount(int tick) =>
            commandsByTick.TryGetValue(tick, out List<TCommand> list) ? list.Count : 0;
    }
}
