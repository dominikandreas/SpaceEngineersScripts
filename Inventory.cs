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

namespace InventoryScript
{
    public class Program : MyGridProgram
    {
        #endregion
        //To put your code in a PB copy from this comment...
        const string PANEL_NAME = "Comp Panel";
        const string CONTAINER_NAME = "Components";
        const int PANEL_LINES = 22;
        int lineOffset = 0;
        Dictionary<String, float> minimums = new Dictionary<String, float>();

        void Main()
        {
            InitDebug();

            if (minimums.Count == 0)
            {
                minimums.Add("Bulletproof Glass", 100000);
                minimums.Add("Computer", 30000);
                minimums.Add("Construction Component", 360000);
                minimums.Add("Detector Component", 1000);
                minimums.Add("Display", 30000);
                minimums.Add("Girder", 6000);
                minimums.Add("GravGen Component", 1000);
                minimums.Add("Interior Plate", 1500000);
                minimums.Add("Large Steel Tube", 180000);
                minimums.Add("Metal Grid", 300000);
                minimums.Add("Motor", 50000);
                minimums.Add("Radio Component", 1000);
                minimums.Add("Reactor Component", 8000);
                minimums.Add("Small Steel Tube", 300000);
                minimums.Add("Steel Plate", 3000000);
                minimums.Add("Thruster Component", 10000);
                minimums.Add("Solar Cell", 1000);
                minimums.Add("Power Cell", 1000);
            }
            List<IMyTerminalBlock> work = new List<IMyTerminalBlock>();
            Dictionary<String, float> consolidated = new Dictionary<String, float>();
            GridTerminalSystem.SearchBlocksOfName(PANEL_NAME, work);
            IMyTextPanel panel = null;
            for (int i = 0; i < work.Count; i++)
            {
                if (work[i] is IMyTextPanel)
                {
                    panel = (IMyTextPanel)work[i];
                    break;
                }
            }
            List<IMyTerminalBlock> containerList = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(CONTAINER_NAME, containerList);

            float maxVolume = 0.0f;
            float currentVolume = 0.0f;
            for (int i = 0; i < containerList.Count; i++)
            {
                if (containerList[i] is IMyCargoContainer)
                {
                    var containerInvOwner = containerList[i] as VRage.ModAPI.Ingame.IMyInventoryOwner;
                    var containerInventory = containerInvOwner.GetInventory(0);
                    maxVolume += (float)containerInventory.MaxVolume;
                    currentVolume += (float)containerInventory.CurrentVolume;
                    var containerItems = containerInventory.GetItems();
                    for (int j = containerItems.Count - 1; j >= 0; j--)
                    {
                        String itemName = decodeItemName(containerItems[j].Content.SubtypeName,
                                          containerItems[j].Content.TypeId.ToString()) + "|" +
                                          containerItems[j].Content.TypeId.ToString();
                        float amount = (float)containerItems[j].Amount;
                        if (!consolidated.ContainsKey(itemName))
                        {
                            consolidated.Add(itemName, amount);
                        }
                        else
                        {
                            consolidated[itemName] += amount;
                        }
                    }
                }
            }

            Dictionary<String, float> working = new Dictionary<String, float>();
            working.Clear();
            var enumerator3 = minimums.GetEnumerator();
            while (enumerator3.MoveNext())
            {
                var pair = enumerator3.Current;
                String itemKey = pair.Key;
                working.Add(itemKey, 0);
            }

            List<String> list = new List<String>();
            var enumerator = consolidated.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var pair = enumerator.Current;
                String itemKey = pair.Key;
                float itemValue = pair.Value;
                checkStock(itemKey.Split('|')[0], itemValue);
                String txt = itemKey.Split('|')[0] + "  -  ";
                String amt = amountFormatter(itemValue, itemKey.Split('|')[1]);
                working[txt] += itemValue;
                txt += amt;
                list.Add(txt);
            }
            var enumerator2 = minimums.GetEnumerator();
            while (enumerator2.MoveNext())
            {
                var pair = enumerator2.Current;
                String itemKey = pair.Key;
                float itemValue = pair.Value;
                checkStock(itemKey, working[itemKey]);
            }
            list.Sort();
            list.Insert(0, "------------------------------------------------------");
            float percentageFull = (float)Math.Round(currentVolume / maxVolume * 100, 2);
            list.Insert(0, CONTAINER_NAME + " Inventory       " + percentageFull.ToString("##0.00") + "%k full");
            for (int o = 0; o < lineOffset; o++)
            {
                String shiftedItem = list[0];
                list.RemoveAt(0);
                list.Add(shiftedItem);
            }
            panel.WritePublicText(String.Join("\n", list.ToArray()), false);

            panel.ShowTextureOnScreen();
            panel.ShowPublicTextOnScreen();
            if (list.Count > PANEL_LINES)
            {
                lineOffset++;
                if (list.Count - lineOffset < PANEL_LINES)
                {
                    lineOffset = 0;
                }
            }
        }

        private IMyTextPanel debugScreen;
        public void InitDebug()
        {
            debugScreen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("Debug");
            if (debugScreen == null) return;
            debugScreen.WritePublicText(String.Format("{0:dd MMM HH:mm}\n", DateTime.Now));
        }

        public void WriteDebug(string message, params object[] args)
        {
            if (debugScreen == null) return;
            debugScreen.WritePublicText(String.Format(message, args), true);
            debugScreen.WritePublicText("\n", true);
        }

        void checkStock(String item, float qty)
        {
            if (minimums.ContainsKey(item))
            {
                List<IMyTerminalBlock> assemblerList = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName(item + " Assembler", assemblerList);
                if (minimums[item] > qty)
                {
                    WriteDebug("{0}: {1} > {2}", item, minimums[item], qty);
                    for (int i = 0; i < assemblerList.Count; i++)
                    {
                        if (assemblerList[i] is IMyAssembler)
                        {
                            ((IMyAssembler)assemblerList[i]).GetActionWithName("OnOff_On").Apply(((IMyAssembler)assemblerList[i]));
                        }
                    }
                }
                else
                {
                    WriteDebug("{0}: {1} <= {2}", item, minimums[item], qty);
                    for (int i = 0; i < assemblerList.Count; i++)
                    {
                        if (assemblerList[i] is IMyAssembler)
                        {
                            ((IMyAssembler)assemblerList[i]).GetActionWithName("OnOff_Off").Apply(((IMyAssembler)assemblerList[i]));
                        }
                    }
                }
            }
        }

        String amountFormatter(float amt, String typeId)
        {
            if (typeId.EndsWith("_Ore") || typeId.EndsWith("_Ingot"))
            {
                if (amt > 1000.0f)
                {
                    return "" + Math.Round((float)amt / 1000, 2).ToString("###,###,##0.00") + "K";
                }
                else
                {
                    return "" + Math.Round((float)amt, 2).ToString("###,###,##0.00");
                }
            }
            return "" + Math.Round((float)amt, 0).ToString("###,###,##0");
        }

        String decodeItemName(String name, String typeId)
        {
            if (name.Equals("Construction")) { return "Construction Component"; }
            if (name.Equals("MetalGrid")) { return "Metal Grid"; }
            if (name.Equals("InteriorPlate")) { return "Interior Plate"; }
            if (name.Equals("SteelPlate")) { return "Steel Plate"; }
            if (name.Equals("SmallTube")) { return "Small Steel Tube"; }
            if (name.Equals("LargeTube")) { return "Large Steel Tube"; }
            if (name.Equals("BulletproofGlass")) { return "Bulletproof Glass"; }
            if (name.Equals("Reactor")) { return "Reactor Component"; }
            if (name.Equals("Thrust")) { return "Thruster Component"; }
            if (name.Equals("GravityGenerator")) { return "GravGen Component"; }
            if (name.Equals("Medical")) { return "Medical Component"; }
            if (name.Equals("RadioCommunication")) { return "Radio Component"; }
            if (name.Equals("Detector")) { return "Detector Component"; }
            if (name.Equals("SolarCell")) { return "Solar Cell"; }
            if (name.Equals("PowerCell")) { return "Power Cell"; }
            if (name.Equals("AutomaticRifleItem")) { return "Rifle"; }
            if (name.Equals("AutomaticRocketLauncher")) { return "Rocket Launcher"; }
            if (name.Equals("WelderItem")) { return "Welder"; }
            if (name.Equals("AngleGrinderItem")) { return "Grinder"; }
            if (name.Equals("HandDrillItem")) { return "Hand Drill"; }
            if (typeId.EndsWith("_Ore"))
            {
                if (name.Equals("Stone"))
                {
                    return name;
                }
                return name + " Ore";
            }
            if (typeId.EndsWith("_Ingot"))
            {
                if (name.Equals("Stone"))
                {
                    return "Gravel";
                }
                if (name.Equals("Magnesium"))
                {
                    return name + " Powder";
                }
                if (name.Equals("Silicon"))
                {
                    return name + " Wafer";
                }
                return name + " Ingot";
            }
            return name;
        }
    }
}
