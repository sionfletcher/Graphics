using System;
using UnityEngine.GraphToolsFoundation.CommandStateObserver;

namespace UnityEditor.GraphToolsFoundation.Overdrive.Samples.Recipes
{
    public class SetDurationCommand : ModelCommand<BakeNodeModel, int>
    {
        const string k_UndoStringSingular = "Set Bake Node Duration";
        const string k_UndoStringPlural = "Set Bake Nodes Duration";

        public SetDurationCommand(int value, params BakeNodeModel[] nodes)
            : base(k_UndoStringSingular, k_UndoStringPlural, value, nodes)
        {
        }

        public static void DefaultHandler(UndoStateComponent undoState, GraphModelStateComponent graphModelState, SetDurationCommand command)
        {
            using (var undoStateUpdater = undoState.UpdateScope)
            {
                undoStateUpdater.SaveSingleState(graphModelState, command);
            }

            using (var graphUpdater = graphModelState.UpdateScope)
            {
                foreach (var nodeModel in command.Models)
                {
                    nodeModel.Duration = command.Value;
                    graphUpdater.MarkChanged(nodeModel, ChangeHint.Data);
                }
            }
        }
    }
}
