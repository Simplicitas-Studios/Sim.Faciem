using System;
using Sim.Faciem.Commands;

namespace Sim.Faciem.CommandBinding
{
    [Serializable]
    public class SerializedCommand
    {
        public string Name;

        public Command Command { get; }
        
        public SerializedCommand()
        {
            
        }

        public SerializedCommand(Command command)
        {
            Command = command;
        }
    }
}