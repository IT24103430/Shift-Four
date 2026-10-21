using System;

namespace ShiftFour
{
    public enum SecurityGate { Checkpoint = 0, Server = 1, Control = 2, Storage = 3 }

    // Scene-independent state. Identity, never discovery order, determines progress.
    public sealed class MissionProgress
    {
        private readonly bool[] consoles = new bool[4];
        private readonly bool[] drones = new bool[4];
        public static readonly string[] Rooms = { "Checkpoint", "Server", "Control", "Storage" };
        public static readonly string[] Agents = { "Scout", "Guard", "Interceptor", "Flanker" };
        public int ConsolesOnline { get; private set; }
        public int DronesDefeated { get; private set; }
        public bool Complete => ConsolesOnline == 4 && DronesDefeated == 4;
        public static bool Valid(int id) => id >= 0 && id < 4;
        public bool ConsoleOnline(int id) => Valid(id) && consoles[id];
        public bool DroneDefeated(int id) => Valid(id) && drones[id];
        public bool CanOpen(int id) => ConsoleOnline(id) && DroneDefeated(id);
        public bool ActivateConsole(int id)
        {
            if (!Valid(id) || consoles[id]) return false;
            consoles[id] = true;
            ConsolesOnline++;
            return true;
        }
        public bool DefeatDrone(int id)
        {
            if (!Valid(id) || drones[id]) return false;
            drones[id] = true;
            DronesDefeated++;
            return true;
        }
        public static int RoomId(string name) => Array.IndexOf(Rooms, name);
    }
}
