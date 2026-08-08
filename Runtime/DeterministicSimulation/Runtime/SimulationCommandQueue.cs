using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>틱별 명령을 입력한 순서대로 보관합니다.</summary>
    public sealed class SimulationCommandQueue<TCommand> where TCommand : struct
    {
        private readonly SortedDictionary<int, List<TCommand>> commandsByTick = new();
        private readonly List<int> keysToRemove = new();
        private TCommand[] readBuffer = Array.Empty<TCommand>();

        /// <summary>명령이 등록된 틱의 개수입니다. 전체 명령 개수와는 다릅니다.</summary>
        public int TickCount => commandsByTick.Count;

        /// <summary>지정한 틱에 실행할 명령을 입력 순서대로 추가합니다.</summary>
        public void Enqueue(int tick, TCommand command)
        {
            if (!commandsByTick.TryGetValue(tick, out List<TCommand> commands))
            {
                commands = new List<TCommand>();
                commandsByTick.Add(tick, commands);
            }

            commands.Add(command);
        }

        /// <summary>등록된 모든 명령을 제거합니다.</summary>
        public void Clear() => commandsByTick.Clear();

        /// <summary>지정한 틱부터 이후에 등록된 명령을 제거합니다. 롤백 후 입력을 다시 구성할 때 사용합니다.</summary>
        public void ClearFromTick(int tick)
        {
            keysToRemove.Clear();
            foreach (KeyValuePair<int, List<TCommand>> pair in commandsByTick)
            {
                if (pair.Key >= tick)
                    keysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
                commandsByTick.Remove(keysToRemove[i]);
        }

        /// <summary>해당 틱의 명령을 반환합니다. 반환값은 다음 호출 전까지만 사용해야 합니다.</summary>
        public ReadOnlySpan<TCommand> GetCommands(int tick)
        {
            if (!commandsByTick.TryGetValue(tick, out List<TCommand> commands) || commands.Count == 0)
                return ReadOnlySpan<TCommand>.Empty;

            if (readBuffer.Length < commands.Count)
                Array.Resize(ref readBuffer, commands.Count);

            commands.CopyTo(readBuffer, 0);
            return new ReadOnlySpan<TCommand>(readBuffer, 0, commands.Count);
        }

        /// <summary>지정한 틱에 등록된 명령 개수를 반환합니다.</summary>
        public int GetCommandCount(int tick) =>
            commandsByTick.TryGetValue(tick, out List<TCommand> commands) ? commands.Count : 0;
    }
}
