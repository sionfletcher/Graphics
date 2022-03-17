using System;
using UnityEditor.GraphToolsFoundation.Overdrive.BasicModel;
using UnityEngine.GraphToolsFoundation.Overdrive;

namespace UnityEditor.GraphToolsFoundation.Overdrive.Tests.TestModels
{
    class TestStencil : Stencil
    {
        Action m_OnGetSearcherDatabaseProviderCallback;
        ISearcherDatabaseProvider m_DefaultSearcherProvider;

        public TestStencil()
        {
            m_DefaultSearcherProvider = new ClassSearcherDatabaseProvider(this);
        }

        public override Type GetConstantNodeValueType(TypeHandle typeHandle)
        {
            return TypeToConstantMapper.GetConstantNodeType(typeHandle);
        }

        public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
        {
            m_OnGetSearcherDatabaseProviderCallback?.Invoke();
            return m_DefaultSearcherProvider;
        }

        public void SetOnGetSearcherDatabaseProviderCallback(Action callback)
        {
            m_OnGetSearcherDatabaseProviderCallback = callback;
        }

        public override IBlackboardGraphModel CreateBlackboardGraphModel(IGraphAssetModel graphAssetModel)
        {
            return new BlackboardGraphModel(graphAssetModel);
        }

        /// <inheritdoc />
        public override IInspectorModel CreateInspectorModel(IModel inspectedModel)
        {
            return null;
        }

        /// <inheritdoc />
        public override bool CanPasteNode(INodeModel originalModel, IGraphModel graph)
        {
            return true;
        }

        public override  bool CanPasteVariable(IVariableDeclarationModel originalModel, IGraphModel graph)
        {
            return true;
        }
    }
}
