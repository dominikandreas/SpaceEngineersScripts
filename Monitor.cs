﻿#region pre-script
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

namespace Monitor
{
    public class Program : MyGridProgram
    {
        #endregion
        const string LCD_PANEL_NAME_ENERGY = "LCD-Monitor-Energy";
        const string LCD_PANEL_NAME_INVENTORY = "LCD-Monitor-Inventory";
        const string GRID_NAME = "BaseMoon";

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

        public string MetricFormat(VRage.MyFixedPoint fp, string formatter = "{0:f2}")
        {
            //string postfix = "g";
            float K = 1000, T = 1000 * K, KT = 1000 * T, MT = 1000 * KT;
            float f = ((float)fp.ToIntSafe());

            var postfixes = new List<String> { "g", "kg", "T", "kT", "MT" };

            int i = 0;
            while (f / 1000 > 1)
            {
                i += 1;
                f /= 1000;
            }
            return fill(string.Format(formatter, f), 7) + " " + postfixes[i];
        }

        public void Main(string argument)
        {
            var lcd1 = GridTerminalSystem.GetBlockWithName(LCD_PANEL_NAME_INVENTORY) as IMyTextPanel;
            var lcd2 = GridTerminalSystem.GetBlockWithName(LCD_PANEL_NAME_ENERGY) as IMyTextPanel;
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
                            }
                        }
                    }
                }
            }

            bBlocks.ForEach(block => {
                E_current += block.CurrentStoredPower;
                E_max += block.MaxStoredPower;
                P_battery_in += block.CurrentInput;
                P_battery_out += block.CurrentOutput;
            });

            sBlocks.ForEach(block => P_solarpanel += block.CurrentOutput);

            foreach (IMyReactor block in rBlocks)
            {
                if (block.CubeGrid.Name == GRID_NAME)
                {
                    block.Enabled = P_solarpanel < 1 && E_current / E_max < 0.8;
                }
                P_reactor += block.CurrentOutput;
            }

            // Plot

            lcd1.WritePublicText(fill("Type", 10) + fill("Ore", 10) + " -> Ingot");

            foreach (KeyValuePair<string, Dictionary<string, VRage.MyFixedPoint>> entry in itemStatus)
            {
                if (entry.Key.ToString() == "Ice") continue;
                lcd1.WritePublicText("\n" + fill(entry.Key.ToString(), 10), append: true);
                lcd1.WritePublicText(fill(MetricFormat(entry.Value["ore"]), 10) + " -> ", append: true);
                lcd1.WritePublicText(fill(MetricFormat(entry.Value["ingot"]), 10), append: true);
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
            var pE = (float)E_current / E_max;

            //lcd2.SetValue("FontColor", new Color((int) System.Math.Max(1.0f, 1.0f-2*(pE-0.5f))*255, (int) System.Math.Max(2*pE, 1.0f)*255, 0));

            lcd2.WritePublicText("Capacity:   \n" + E_current.ToString() + " / " + E_max.ToString() + " MWh \n");
            lcd2.WritePublicText("@ " + dP + " MWh/min\n", append: true);
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

            E_lasttick = (3 * E_lasttick + E_current) / 4;
            T_lasttick = DateTime.Now;
        }
        //to this comment.
        #region post-script
    }
}
#endregion