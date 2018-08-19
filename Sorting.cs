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
                items.Add(line.Trim());
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

        void SortContainerComponents(List<IMyTerminalBlock> sourceBlocks, List<IMyTerminalBlock> targetContainers, int inventoryIndex)
        {
            foreach (var source in sourceBlocks)
            {
                foreach (var target in FilterSortingContainers(targetContainers))
                {
                    if (source == null || target == null || source == target || source.GetInventory() == null || target.GetInventory() == null)
                        continue;

                    var typesForTransfer = GetItemTypesForTransfer(source, target);

                    debugLogging = string.Join(", ", typesForTransfer).Contains("Ingot");

                    log("transferring " + string.Join(", ", typesForTransfer) + " between " + source.CustomName + " and " + target.CustomName, debug: true);

                    int itemCount = 0;

                    var sourceItems = source.GetInventory().GetItems();
                    for (int si = 0; si < sourceItems.Count; si++)
                    {
                        var item = sourceItems[si];
                        log("checking " + item.Content.ToString(), debug: true);
                        foreach (var targetItemType in typesForTransfer)
                        {
                            if (item.Content.ToString().Contains(targetItemType) && item.Content.SubtypeId.ToString() != "Ice")
                            {
                                log(" transferring " + item.Amount + " " + item.Content.TypeId, debug: false);
                                itemCount += 1;
                                source.GetInventory().TransferItemTo(target.GetInventory(), si, null, true, null);
                            }
                        }
                    }
                    if (itemCount > 0)
                        log(" transferred " + itemCount.ToString() + " items from " + source.CustomName + " to " + target.CustomName);
                }
            }

        }

        //to this comment.
        #region post-script
    }
}
#endregion