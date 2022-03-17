#if UNITY_2022_2_OR_NEWER
using System;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    [Overlay(typeof(GraphViewEditorWindow), idValue, "MiniMap",
        defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Bottom, defaultLayout = Layout.Panel)]
    [Icon( AssetHelper.AssetPath + "UI/Stylesheets/Icons/PanelsToolbar/MiniMap.png")]
    sealed class MiniMapOverlay : ResizableOverlay
    {
        public const string idValue = "gtf-minimap";

        protected override string Stylesheet => "MiniMapOverlay.uss";

        /// <inheritdoc />
        protected override VisualElement CreateResizablePanelContent()
        {
            var window = containerWindow as GraphViewEditorWindow;
            if (window != null && window.GraphView != null)
            {
                var content = window.CreateMiniMapView();
                content.AddToClassList("unity-theme-env-variables");
                content.RegisterCallback<TooltipEvent>((e) => e.StopPropagation());
                return content;
            }

            var placeholder = new VisualElement();
            placeholder.AddToClassList(MiniMapView.ussClassName);
            placeholder.AddStylesheet("MiniMapView.uss");
            return placeholder;
        }
    }
}
#endif
