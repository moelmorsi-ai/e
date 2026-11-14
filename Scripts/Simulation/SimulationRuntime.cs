#nullable enable
using Godot;
using System;

namespace BronzeAge
{
    /// <summary>
    /// Godot-facing runtime host for the simulation. Attach this node to the game
    /// scene and call Initialize(WorldData) after procedural geography is generated.
    /// </summary>
    public partial class SimulationRuntime : Node
    {
        [Export] public double DaysPerSecond { get; set; } = 1.0;
        [Export] public bool StartPaused { get; set; } = false;
        [Export] public int InitialCivilizationCount { get; set; } = 8;
        public WorldState? State { get; private set; }
        private readonly SimulationClock _clock = new();

        public void Initialize(WorldData geography)
        {
            State = WorldBootstrap.CreateGame(geography, InitialCivilizationCount);
            State.Paused = StartPaused;
            _clock.DaysPerSecond = Math.Max(0.01, DaysPerSecond);
        }

        public override void _Process(double delta)
        {
            if (State == null) return;
            _clock.DaysPerSecond = Math.Max(0.01, DaysPerSecond);
            _clock.Advance(State, delta, () => { });
        }

        public void SetPaused(bool paused)
        {
            if (State != null) State.Paused = paused;
        }

        public string CreateSave() => State == null
            ? throw new InvalidOperationException("Simulation has not been initialized.")
            : SaveManager.Serialize(State);

        public void LoadSave(string json) => State = SaveManager.Deserialize(json);
    }
}
