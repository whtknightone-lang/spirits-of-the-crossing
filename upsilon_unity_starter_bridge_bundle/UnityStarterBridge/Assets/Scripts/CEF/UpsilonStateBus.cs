using System;
using System.Collections.Generic;

namespace Upsilon.CEF
{
    public static class UpsilonStateBus
    {
        private static readonly Dictionary<string, CEFEntityState> States = new Dictionary<string, CEFEntityState>();
        public static event Action<CEFEntityState> StateUpdated;

        public static bool TryGetState(string entityId, out CEFEntityState state)
        {
            return States.TryGetValue(entityId, out state);
        }

        public static void Push(CEFPacket packet)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.entity)) return;
            if (!States.TryGetValue(packet.entity, out var state))
            {
                state = new CEFEntityState { entityId = packet.entity };
                States[packet.entity] = state;
            }
            state.Apply(packet);
            StateUpdated?.Invoke(state);
        }
    }
}
