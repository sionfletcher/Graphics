﻿using System;
using UnityEditor.GraphToolsFoundation.Overdrive;
using UnityEditor.GraphToolsFoundation.Overdrive.BasicModel;
using UnityEngine;
using UnityEngine.GraphToolsFoundation.Overdrive;

namespace UnityEditor.VFX
{
    [Serializable]
    public class VFXNodeBaseModel : NodeModel
    {
    }

    public class VFXOperatorNode : VFXNodeBaseModel
    {
        private VFXOperator m_NodeModel;

        internal void SetOperator(VFXOperator model)
        {
            m_NodeModel = model;
            DefineNode();
        }

        protected override void OnDefineNode()
        {
            if (this.m_NodeModel == null)
            {
                return;
            }

            foreach (var inputSlot in m_NodeModel.inputSlots)
            {
                switch (inputSlot.property.type.Name)
                {
                    case nameof(AABox):
                        this.AddDataInputPort("Center", typeof(Vector3).GenerateTypeHandle());
                        this.AddDataInputPort("Size", typeof(Vector3).GenerateTypeHandle());
                        break;
                    default:
                        this.AddDataInputPort(inputSlot.name, inputSlot.property.type.GenerateTypeHandle());
                        break;
                }
            }

            foreach (var outputSlot in m_NodeModel.outputSlots)
            {
                this.AddDataOutputPort(outputSlot.name, outputSlot.property.type.GenerateTypeHandle(), options: PortModelOptions.NoEmbeddedConstant);
            }
        }
    }
}
