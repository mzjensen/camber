﻿using System;
using System.Linq;
using System.Collections.Generic;
using ProtoCore.AST.AssociativeAST;
using Newtonsoft.Json;
using acDynNodes = Autodesk.AutoCAD.DynamoNodes;
using Camber.Civil.DataShortcuts;
using Dynamo.Graph.Nodes;

namespace Camber.UI
{
    [NodeName("Get Data Shortcut Working Folder")]
    [NodeDescription("Gets the current Data Shortcut Working Folder.")]
    [NodeCategory("Camber.Civil 3D.Data Shortcuts")]
    [InPortNames("document")]
    [InPortTypes("Autodesk.AutoCAD.DynamoNodes.Document")]
    [InPortDescriptions("document")]
    [OutPortNames("dataShortcutWorkingFolder")]
    [OutPortTypes("Camber.Civil.DataShortcuts.DataShortcutWorkingFolder")]
    [OutPortDescriptions("dataShortcutWorkingFolder")]

    [IsDesignScriptCompatible]
    public class GetWorkingFolder : NodeModel
    {
        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public GetWorkingFolder()
        {
            RegisterAllPorts();
        }

        /// <summary>
        /// JSON constructor
        /// </summary>
        /// <param name="inPorts"></param>
        /// <param name="outPorts"></param>
        [JsonConstructor]
        private GetWorkingFolder(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts) { }
        #endregion

        #region methods
        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAsNodes)
        {
            if (!InPorts[0].Connectors.Any())
            {
                return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), AstFactory.BuildNullNode()) };
            }

            var functionNode =
                AstFactory.BuildFunctionCall(
                    new Func<acDynNodes.Document, DataShortcutWorkingFolder>(DataShortcuts.GetWorkingFolder),
                    new List<AssociativeNode> { inputAsNodes[0] });

            return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), functionNode) };
        }
        #endregion
    }
}