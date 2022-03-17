namespace UnityEditor.GraphToolsFoundation.Overdrive.Samples.Recipes
{
    public class RecipeGraphView : GraphView
    {
        public RecipeGraphView(GraphViewEditorWindow window, BaseGraphTool graphTool, string graphViewName,
            GraphViewDisplayMode displayMode = GraphViewDisplayMode.Interactive)
            : base(window, graphTool, graphViewName, displayMode)
        {
            if (displayMode == GraphViewDisplayMode.Interactive)
            {
                this.RegisterCommandHandler<AddPortCommand>(AddPortCommand.DefaultHandler);
                this.RegisterCommandHandler<RemovePortCommand>(RemovePortCommand.DefaultHandler);

                this.RegisterCommandHandler<SetTemperatureCommand>(SetTemperatureCommand.DefaultHandler);
                this.RegisterCommandHandler<SetDurationCommand>(SetDurationCommand.DefaultHandler);
            }
        }
    }
}
