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
    public class Program : MyGridProgram
    {
        #endregion
        //To put your code in a PB copy from this comment...
        List<IMyTerminalBlock> tBlocks = new List<IMyTerminalBlock>();
        List<IMyBatteryBlock> bBlocks = new List<IMyBatteryBlock>();
        List<IMySolarPanel> sBlocks = new List<IMySolarPanel>();
        List<IMyReactor> rBlocks = new List<IMyReactor>();
        float E_lasttick = 0.0f;
        DateTime T_lasttick = DateTime.Now;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {

        }

        public string fill(string s, int l)
        {
            return s.PadRight(l, ' ');
        }

        public void Main(string argument)
        {
            var lcd1 = GridTerminalSystem.GetBlockWithName("MVI-Panel-1") as IMyTextPanel;
            var lcd2 = GridTerminalSystem.GetBlockWithName("MVI-Panel-2") as IMyTextPanel;
            var refinery = GridTerminalSystem.GetBlockWithName("MVI-Refinery-1") as IMyRefinery;
            var itemStatus = new SortedDictionary<string, Dictionary<string, VRage.MyFixedPoint>>();

            var E_current = 0.0f;
            var E_max = 0.0f;
            var P_battery_in = 0.0f;
            var P_battery_out = 0.0f;
            var P_solarpanel = 0.0f;
            var P_reactor = 0.0f;

            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tBlocks);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(bBlocks);
            GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(sBlocks);
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(rBlocks);

            foreach (IMyTerminalBlock block in tBlocks)
            {
                if (block.HasInventory)
                {
                    for (int i = 0; i < block.InventoryCount; i++)
                    {
                        var inventory = block.GetInventory(i);
                        var items = inventory.GetItems();

                        foreach (IMyInventoryItem item in items)
                        {
                            string type = item.Content.TypeId.ToString();

                            // If not ore nor ingot, continue with next item
                            if (type != "MyObjectBuilder_Ingot" && type != "MyObjectBuilder_Ore") continue;

                            // Read the SubtypeId create new dictionary entry with zero initialized vals
                            var k = item.Content.SubtypeId.ToString();
                            if (!itemStatus.ContainsKey(k))
                            {
                                itemStatus.Add(k, new Dictionary<string, VRage.MyFixedPoint>());
                                itemStatus[k].Add("ingot", 0);
                                itemStatus[k].Add("ore", 0);
                            }

                            // Add the item amount to the specific field
                            if (type == "MyObjectBuilder_Ingot")
                            {
                                itemStatus[k]["ingot"] += item.Amount;
                            }
                            else
                            {
                                itemStatus[k]["ore"] += item.Amount;
                                if(k != "Ice" && k != "Stone"){
                                    if(inventory != refinery.GetInventory(0)){
                                        // Move every ore except ice and stone into refinery
                                        inventory.TransferItemTo(refinery.GetInventory(0), (int)item.ItemId, stackIfPossible:true);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (IMyBatteryBlock block in bBlocks)
            {
                E_current += block.CurrentStoredPower;
                E_max += block.MaxStoredPower;
                P_battery_in += block.CurrentInput;
                P_battery_out += block.CurrentOutput;
            }

            foreach (IMySolarPanel block in sBlocks)
            {
                P_solarpanel += block.CurrentOutput;
            }

            foreach (IMyReactor block in rBlocks)
            {
                if(block.CubeGrid.Name == "EarthEasyStation"){
                    block.Enabled = P_solarpanel < 1 && E_current/E_max < 0.8;
                }
                P_reactor += block.CurrentOutput;
            }

            // Plot

            lcd1.WritePublicText(fill("Type", 10) + fill("Ore", 15) + " -> Ingot");

            foreach (KeyValuePair<string, Dictionary<string, VRage.MyFixedPoint>> entry in itemStatus)
            {
                if(entry.Key.ToString() == "Ice") continue;
                lcd1.WritePublicText("\n" + fill(entry.Key.ToString(), 10), append: true);
                lcd1.WritePublicText(fill(entry.Value["ore"].ToString(), 15) + " -> ", append: true);
                lcd1.WritePublicText(fill(entry.Value["ingot"].ToString(), 15), append: true);
                if (entry.Value["ingot"] == 0)
                {
                    lcd1.WritePublicText("\uE002", append: true);
                }
            }

            if (E_lasttick == 0.0f)
            {
                E_lasttick = E_current;
            }

            var dE = E_current - E_lasttick;
            var dT = (DateTime.Now - T_lasttick).Milliseconds;

            var dP = dE * 30000 / dT;

            lcd2.WritePublicText("Capacity:   \n" + E_current.ToString() + " / " + E_max.ToString() + " MWh \n");
            lcd2.WritePublicText("@ " + dP + " MWh per minute\n", append: true);
            if (dP > 0)
            {
                lcd2.WritePublicText("Full in " + (E_max - E_current) / dP + " min!\n\n", append: true);
            }
            if (dP < 0)
            {
                lcd2.WritePublicText("Empty in " + E_current / dP * (-1) + " min!\n\n", append: true);
            }
            lcd2.WritePublicText("Generation:  " + (P_solarpanel + P_reactor).ToString() + " MW\n", append: true);
            lcd2.WritePublicText("Consumption: " + P_battery_out.ToString() + " MW\n", append: true);
            lcd2.WritePublicText("Balance:     " + (P_solarpanel + P_reactor - P_battery_out).ToString() + " MW\n", append: true);

            E_lasttick = (3*E_lasttick + E_current)/4;
            T_lasttick = DateTime.Now;
        }
        //to this comment.
        #region post-script
    }
}
#endregion