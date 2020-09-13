﻿namespace Obsidian.Net.Packets.Play.Client.GameState
{
    public class DemoEventState : ChangeGameState<DemoEvent>
    {
        public override DemoEvent Value { get; set; }

        public DemoEventState(DemoEvent @event) => this.Value = @event;
    }

    public enum DemoEvent
    {
        ShowWelcomeScreen = 0,

        TellMovementControls = 101,
        TellJumpControl = 102,
        TellInventoryControl = 103,
        TellDemoOver = 104
    }
}
