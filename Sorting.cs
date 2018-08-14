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

namespace SortingScript
{
    public class Program : MyGridProgram
    {
        #endregion
        //To put your code in a PB copy from this comment...

        DateTime T_SortingStarted;
        IMyTextPanel lcdPanel;
        IMyConveyorSorter sorterIn, sorterOut;
        IMyTimerBlock sorterTimer;
        int maxLCDLines = 10;
        int sortDurationMs = 300;

        string lcdContent = "";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            SetBlockReferences();
            log("Program initialized\n\n", clear: true);
        }

        public void Save()
        {
            //setSorterMode(false);
        }

        public string fill(string s, int l)
        {
            return s.PadRight(l, ' ');
        }

        public T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public void log(string message, bool clear = false, bool endLine = true)
        {
            if (clear)
                lcdContent = "";
            var lines = lcdContent.Split('\n');

            int linesStartOffset = Math.Max(lines.Length - maxLCDLines, 0);
            int linesToCopy = lines.Length - linesStartOffset;

            if (message[0] == '\r')
            {
                message = message.Substring(1);
                linesToCopy = lines.Length - linesStartOffset - 1;
            }

            lines = SubArray(lines, linesStartOffset, linesToCopy);
            lcdContent = string.Join("\n", lines);
            lcdContent = lcdContent + message + (endLine ? "\n" : "");

            lcdPanel.WritePublicText(lcdContent);
        }

        public void setSorterMode(bool enabled)
        {
            if (enabled)
            {
                T_SortingStarted = DateTime.Now;
                log("sorting components.. ", endLine: false);
                sorterOut.Enabled = false;
                sorterIn.DrainAll = true;
            }
            else
            {
                sorterIn.DrainAll = false;
                sorterOut.Enabled = true;
                log("done");
            }

        }

        public string checkBlockReferences(List<string> blockNames)
        {
            foreach (string name in blockNames)
                if (GridTerminalSystem.GetBlockWithName(name) == null)
                    throw new Exception(name + " is null!");
            return "";
        }

        public bool SetBlockReferences()
        {
            string res = checkBlockReferences(new List<string> { "Sorting-Panel-1", "Component-Sorter-In", "Component-Sorter-Out", "Component-Sorting-Timer" });
            lcdPanel = GridTerminalSystem.GetBlockWithName("Sorting-Panel-1") as IMyTextPanel;

            sorterIn = GridTerminalSystem.GetBlockWithName("Component-Sorter-In") as IMyConveyorSorter;
            sorterOut = GridTerminalSystem.GetBlockWithName("Component-Sorter-Out") as IMyConveyorSorter;
            sorterTimer = GridTerminalSystem.GetBlockWithName("Component-Sorting-Timer") as IMyTimerBlock;
            return true;
        }

        public void Main(string argument)
        {
            if (!SetBlockReferences())
                return;

            bool isSorting = sorterIn.DrainAll && (T_SortingStarted != null);

            int msPassed = (DateTime.Now - T_SortingStarted).Milliseconds;
            
            if (isSorting)
            {
                
                if (msPassed > sortDurationMs)
                {
                    setSorterMode(enabled: false);
                    if (sorterTimer.Enabled)
                    {
                        sorterTimer.StartCountdown();
                    }
                }
                    
            }
            else if (argument == "start")
                setSorterMode(enabled: true);

            if (argument == "auto")
            {
                sorterTimer.Enabled = true;
            }
            else if (argument == "stop")
            {
                setSorterMode(enabled: false);
                sorterTimer.Enabled = false;
            }

        }

        //to this comment.
        #region post-script
    }
}
#endregion