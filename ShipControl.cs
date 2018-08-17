#region pre-script
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    public class Program2 : MyGridProgram
    {
        #endregion
        //To put your code in a PB copy from this comment...
        List<IMyShipConnector> sBlocks = new List<IMyShipConnector>();
        List<IMyReactor> rBlocks = new List<IMyReactor>();
        List<IMyThrust> tBlocks = new List<IMyThrust>();
    
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }        
        public void Main(string argument, UpdateType updateSource)

        {
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(sBlocks);
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(rBlocks);
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(tBlocks);
           
            var isConnected = false;
            foreach (IMyShipConnector c in sBlocks){
                isConnected = isConnected || (c.Status == MyShipConnectorStatus.Connected);
            }

            if(isConnected){
                rBlocks.ForEach(r => r.Enabled = false);
                tBlocks.ForEach(t => t.Enabled = false);
            }else{
                rBlocks.ForEach(r => r.Enabled = true);
                tBlocks.ForEach(t => t.Enabled = true);
            }
        }


        //to this comment.
        #region post-script
    }
}
#endregion