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

namespace SortingScript2
{
    public class Program : MyGridProgram
    {
        #endregion
        //To put your code in a PB copy from this comment...

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        // block types to search in
        List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> assemblers = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> connectors = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> oxyGenerators = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> refineries = new List<IMyTerminalBlock>();

        // variable list to hold target containers
        List<IMyTerminalBlock> targetContainers = new List<IMyTerminalBlock>();

        IMyTextPanel lcdPanel;
        string lcdPanelName = "Sorting-Panel";
        //contains current lcd content (all of it)
        string lcdContent = "";
        bool debugLogging = false;
        int maxLCDLines = 200;  // maximum number of lines (FIFO - earlier lines get removed)

        public T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public void log(string message, bool clear = false, bool endLine = true, bool debug = false, bool addTime = true)
        {
            if (debug && !debugLogging)
                return;
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

            if (addTime)
            {
                var dt = DateTime.Now;
                message = String.Format("{0:T}", dt) + " " + message;
            }

            lcdContent = lcdContent + message + (endLine ? "\n" : "");

            if (lcdPanel != null)
                lcdPanel.WritePublicText(lcdContent);
            else
                Echo(message);
        }

        void Main()
        {
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers);
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(oxyGenerators);
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries);

            lcdPanel = GridTerminalSystem.GetBlockWithName(lcdPanelName) as IMyTextPanel;

            log("initialized", debug: true);

            SortContainerComponents(containers, containers, 0);
            // Only process the second inventory of assemblers
            SortContainerComponents(assemblers, containers, 1);
            SortContainerComponents(refineries, containers, 1);
            SortContainerComponents(connectors, containers, 0);
            SortContainerComponents(oxyGenerators, containers, 0);

        }

        IMyTerminalBlock GetContainerWithSpaceLeft(List<IMyTerminalBlock> containers)
        {
            foreach (IMyTerminalBlock container in containers)
            {
                if (!container.GetInventory().IsFull)
                    return container;
            }

            // Either containers list is empty or all containers are full
            log("no target containers found or all containers are full!");
            return null;
        }

        List<String> GetAcceptedItems(IMyTerminalBlock container)
        {
            List<String> items = new List<string>();
            foreach (var line in container.CustomData.Trim().Split('\n'))
            {
                if (!line.Trim().StartsWith("-"))
                    items.Add(line.Trim());
            }
            return items;
        }

        List<String> GetBlacklistedItems(IMyTerminalBlock container)
        {
            List<String> items = new List<string>();
            foreach (var line in container.CustomData.Trim().Split('\n'))
            {
                if (line.Trim().StartsWith("-"))
                    items.Add(line.Trim().Substring(1));
            }
            return items;
        }

        List<IMyTerminalBlock> FilterSortingContainers(List<IMyTerminalBlock> containers, int inventoryIndex = 0)
        {
            List<IMyTerminalBlock> filtered = new List<IMyTerminalBlock>();
            foreach (IMyTerminalBlock container in containers)
            {
                if (!container.GetInventory(inventoryIndex).IsFull && container.CustomData.Length > 0)
                    filtered.Add(container);
            }
            return filtered;
        }

        List<String> GetItemTypesForTransfer(IMyTerminalBlock source, IMyTerminalBlock target)
        {
            List<String> typesForTransfer = new List<string>();
            var sourceItemTypes = GetAcceptedItems(source);
            var targetItemTypes = GetAcceptedItems(target);
            foreach (var targetItemType in targetItemTypes)
            {
                if (!sourceItemTypes.Contains(targetItemType))
                {
                    typesForTransfer.Add(targetItemType);
                }
            }
            return typesForTransfer;
        }

        bool CheckOccurrence(string itemName, List<string> listOfStrings)
        {
            foreach (var entry in listOfStrings)
                if (itemName.Contains(entry))
                    return true;
            return false;
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

            var postfixes = new List<String> { "", "k", "M", "G", "P" };

            int i = 0;
            while (f / 1000 > 1)
            {
                i += 1;
                f /= 1000;
            }
            return fill(string.Format(formatter, f), 5) + postfixes[i];
        }

        void SortContainerComponents(List<IMyTerminalBlock> sourceBlocks, List<IMyTerminalBlock> targetContainers, int inventoryIndex)
        {
            foreach (var source in sourceBlocks)
            {
                foreach (var target in FilterSortingContainers(targetContainers))
                {
                    if (source == null || target == null || source == target || source.GetInventory(inventoryIndex) == null || target.GetInventory() == null)
                        continue;

                    var typesForTransfer = GetItemTypesForTransfer(source, target);
                    var targetIgnoredItemTypes = GetBlacklistedItems(target);

                    log("transferring " + string.Join(", ", typesForTransfer) + " between " + source.CustomName + " and " + target.CustomName, debug: true);

                    int itemCount = 0;

                    var sourceItems = source.GetInventory(inventoryIndex).GetItems();

                    List<string> transferredItems = new List<string>();

                    for (int si = 0; si < sourceItems.Count; si++)
                    {
                        var item = sourceItems[si];
                        if (item.Amount < 100)
                            continue;

                        log("checking " + item.Content.ToString(), debug: true);

                        string itemClassName = item.Content.ToString(), itemName = item.Content.SubtypeId.ToString();
                        bool allowed = CheckOccurrence(itemClassName, typesForTransfer) || CheckOccurrence(itemName, typesForTransfer);
                        allowed = allowed && !(CheckOccurrence(itemClassName, targetIgnoredItemTypes) || CheckOccurrence(itemName, targetIgnoredItemTypes));

                        if (allowed)
                        {
                            transferredItems.Add(itemName + " " + MetricFormat(item.Amount, formatter: "{0:f1}"));
                            itemCount += 1;
                            source.GetInventory(inventoryIndex).TransferItemTo(target.GetInventory(), si, null, true, null);
                        }

                    }
                    if (itemCount > 0)
                        log(source.CustomName + " > " + target.CustomName + ": " + string.Join(",", transferredItems));
                }
            }

        }

        //to this comment.
        #region post-script
    }
}
#endregion