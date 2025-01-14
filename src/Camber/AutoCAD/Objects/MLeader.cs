﻿#region references
using System;
using System.Linq;
using System.Collections.Generic;
using acDb = Autodesk.AutoCAD.DatabaseServices;
using acGeom = Autodesk.AutoCAD.Geometry;
using acDynApp = Autodesk.AutoCAD.DynamoApp.Services;
using acDynNodes = Autodesk.AutoCAD.DynamoNodes;
using AcMLeader = Autodesk.AutoCAD.DatabaseServices.MLeader;
using Autodesk.DesignScript.Geometry;
using DynamoServices;
using Autodesk.DesignScript.Runtime;
using Camber.Properties;
using Camber.Utilities.GeometryConversions;
#endregion

namespace Camber.AutoCAD.Objects
{
    [RegisterForTrace]
    public sealed class MLeader : Object
    {
        #region properties
        internal AcMLeader AcMLeader => AcObject as AcMLeader;

        /// <summary>
        /// Gets the number of leaders in the MLeader.
        /// </summary>
        public int LeaderCount => GetInt();

        /// <summary>
        /// Gets the text height of MLeader MText content.
        /// </summary>
        public double TextHeight => GetDouble();

        /// <summary>
        /// Gets the Attribute References of an MLeader with block content.
        /// </summary>
        private IList<acDb.AttributeReference> AttributeReferences
        {
            get
            {
                if (ContentType != acDb.ContentType.BlockContent.ToString())
                {
                    throw new InvalidOperationException("MLeader does not contain block content.");
                }

                acDynNodes.Document document = acDynNodes.Document.Current;
                try
                {
                    using (var ctx = new acDynApp.DocumentContext(document.AcDocument))
                    {
                        List<AttributeDefinition> attDefs = Objects.Block.AttributeDefinitions(Block);
                        List<acDb.AttributeReference> attRefs = new List<acDb.AttributeReference>();

                        AcMLeader acMld = (AcMLeader)ctx.Transaction.GetObject(InternalObjectId, acDb.OpenMode.ForRead);
                        foreach (AttributeDefinition attDef in attDefs)
                        {
                            attRefs.Add(acMld.GetBlockAttribute(attDef.InternalObjectId));
                        }
                        return attRefs;
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(e.Message);
                }
            }
        }
        #endregion

        #region constructors
        [SupressImportIntoVM]
        internal static MLeader GetByObjectId(acDb.ObjectId mLeaderId)
            => ObjectSupport.Get<MLeader, AcMLeader>
            (mLeaderId, (mLeader) => new MLeader(mLeader));

        internal MLeader(AcMLeader acMLeader, bool isDynamoOwned = false) : base(acMLeader, isDynamoOwned) { }
        #endregion

        #region methods
        public override string ToString() => $"MLeader(ContentType = {ContentType})";
        #endregion

        #region obsolete
        /// <summary>
        /// Gets the Block object of MLeader Block content.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.Block",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.SourceBlock")]
        public acDynNodes.Block Block
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.SourceBlock"));

                acDynNodes.Document document = acDynNodes.Document.Current;
                using (var ctx = new acDynApp.DocumentContext(document.AcDocument))
                {
                    acDb.ObjectId blockId = AcMLeader.BlockContentId;
                    acDb.BlockTableRecord acBlock = (acDb.BlockTableRecord)blockId.GetObject(acDb.OpenMode.ForRead);
                    return acDynNodes.Block.GetBlockByName(acDynNodes.Document.Current, acBlock.Name);
                }
            }
        }

        /// <summary>
        /// Gets the position of MLeader Block content.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.BlockPosition",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.BlockLocation")]
        public Point BlockPosition
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.BlockLocation"));
                return GeometryConversions.AcPointToDynPoint(AcMLeader.BlockPosition);
            }
        }

        /// <summary>
        /// Creates an MLeader with Block content.
        /// </summary>
        /// <param name="point">Arrowhead point</param>
        /// <param name="block"></param>
        /// <param name="draggedOffset"></param>
        /// <returns></returns>
        public static MLeader ByPointBlock(Point point, acDynNodes.Block block, Vector draggedOffset)
        {
            LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MESSAGE, "Multileader.ByPointsBlock"));

            acDynNodes.Document document = acDynNodes.Document.Current;

            acGeom.Point3d insertionPoint = (acGeom.Point3d)GeometryConversions.DynPointToAcPoint(point, true);
            acGeom.Point3d draggedPoint = new acGeom.Point3d(
                            insertionPoint.X + draggedOffset.X,
                            insertionPoint.Y + draggedOffset.Y,
                            insertionPoint.Z + draggedOffset.Z);

            using (var ctx = new acDynApp.DocumentContext(document.AcDocument))
            {
                acDb.Transaction t = ctx.GetTransaction(document.AcDocument);
                acDb.BlockTable bt = (acDb.BlockTable)t.GetObject(ctx.Database.BlockTableId, acDb.OpenMode.ForRead);
                acDb.BlockTableRecord model = (acDb.BlockTableRecord)t.GetObject(bt[acDb.BlockTableRecord.ModelSpace], acDb.OpenMode.ForWrite);
                acDb.ObjectId blockId = bt[block.Name];

                acDb.ObjectId mldId = acDynApp.ElementBinder.GetObjectIdFromTrace(ctx.Database);

                if (mldId.IsValid && !mldId.IsErased)
                {
                    AcMLeader acMld = (AcMLeader)mldId.GetObject(acDb.OpenMode.ForWrite);
                    if (acMld != null)
                    {
                        acGeom.Vector3d translation = acMld.GetLastVertex(0).GetVectorTo(draggedPoint);
                        acMld.SetFirstVertex(0, insertionPoint);
                        acMld.MoveMLeader(translation, acDb.MoveType.MoveAllExceptArrowHeaderPoints);
                        acMld.BlockContentId = blockId;
                    }
                }
                else
                {
                    // Create new MLeader
                    AcMLeader mld = new AcMLeader();
                    mld.SetDatabaseDefaults();
                    mld.AddLeader();
                    mld.AddLeaderLine(0);
                    mld.AddFirstVertex(0, insertionPoint);
                    mld.AddLastVertex(0, draggedPoint);
                    mld.ContentType = acDb.ContentType.BlockContent;
                    mld.BlockContentId = blockId;
                    model.AppendEntity(mld);
                    t.AddNewlyCreatedDBObject(mld, true);
                    mldId = mld.ObjectId;
                }

                AcMLeader createdMLeader = (AcMLeader)mldId.GetObject(acDb.OpenMode.ForRead);
                if (createdMLeader != null)
                {
                    return new MLeader(createdMLeader, true);
                }
                return null;
            }
        }

        /// <summary>
        /// Creates an MLeader with MText content.
        /// </summary>
        /// <param name="point">Arrowhead point</param>
        /// <param name="text">MText contents</param>
        /// <param name="draggedOffset"></param>
        /// <returns></returns>
        public static MLeader ByPointText(Point point, string text, Vector draggedOffset)
        {
            LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MESSAGE, "Multileader.ByPointsText"));

            acDynNodes.Document document = acDynNodes.Document.Current;

            acGeom.Point3d insertionPoint = (acGeom.Point3d)GeometryConversions.DynPointToAcPoint(point, true);
            acGeom.Point3d draggedPoint = new acGeom.Point3d(
                            insertionPoint.X + draggedOffset.X,
                            insertionPoint.Y + draggedOffset.Y,
                            insertionPoint.Z + draggedOffset.Z);

            using (var ctx = new acDynApp.DocumentContext(document.AcDocument))
            {
                acDb.Transaction t = ctx.GetTransaction(document.AcDocument);
                acDb.BlockTable bt = (acDb.BlockTable)t.GetObject(ctx.Database.BlockTableId, acDb.OpenMode.ForRead);
                acDb.BlockTableRecord model = (acDb.BlockTableRecord)t.GetObject(bt[acDb.BlockTableRecord.ModelSpace], acDb.OpenMode.ForWrite);

                acDb.ObjectId mldId = acDynApp.ElementBinder.GetObjectIdFromTrace(ctx.Database);

                if (mldId.IsValid && !mldId.IsErased)
                {
                    AcMLeader acMld = (AcMLeader)mldId.GetObject(acDb.OpenMode.ForWrite);
                    if (acMld != null)
                    {
                        acGeom.Vector3d translation = acMld.GetLastVertex(0).GetVectorTo(draggedPoint);
                        acMld.SetFirstVertex(0, insertionPoint);
                        acMld.MoveMLeader(translation, acDb.MoveType.MoveAllExceptArrowHeaderPoints);
                        acDb.MText mtext = acMld.MText;
                        mtext.Contents = text;
                        acMld.MText = mtext;
                    }
                }
                else
                {
                    // Create new MLeader
                    AcMLeader mld = new AcMLeader();
                    mld.SetDatabaseDefaults();
                    mld.AddLeader();
                    mld.AddLeaderLine(0);
                    mld.AddFirstVertex(0, insertionPoint);
                    mld.AddLastVertex(0, draggedPoint);
                    mld.ContentType = acDb.ContentType.MTextContent;
                    acDb.MText mText = new acDb.MText();
                    mText.SetDatabaseDefaults();
                    mText.Contents = text;
                    mld.MText = mText;
                    model.AppendEntity(mld);
                    t.AddNewlyCreatedDBObject(mld, true);
                    mldId = mld.ObjectId;
                }

                AcMLeader createdMLeader = (AcMLeader)mldId.GetObject(acDb.OpenMode.ForRead);
                if (createdMLeader != null)
                {
                    return new MLeader(createdMLeader, true);
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the MLeader's content type.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.ContentType",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.ContentType")]
        public string ContentType
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.ContentType"));
                return GetString();
            }
        }

        /// <summary>
        /// Gets the value of a Block Attribute Reference in an MLeader by tag.
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns></returns>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.GetAttributeValueByTag",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.BlockAttributeValueByTag")]
        public string GetAttributeValueByTag(string tagName)
        {
            LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.BlockAttributeValueByTag"));

            if (string.IsNullOrEmpty(tagName)) { throw new ArgumentNullException("tagName"); }

            if (ContentType != acDb.ContentType.BlockContent.ToString())
            {
                throw new InvalidOperationException("MLeader does not contain block content.");
            }

            var match = AttributeReferences
                .FirstOrDefault(attRef => attRef.Tag.Equals
                (tagName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                throw new InvalidOperationException($"The MLeader Block does not contain an attribute named {tagName}.");
            }

            return match.TextString;
        }

        /// <summary>
        /// Sets the value of a Block Reference Attribute in an MLeader by tag.
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.SetAttributeValueByTag",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.SetBlockAttributeValueByTag")]
        public MLeader SetAttributeValueByTag(string tagName, string value)
        {
            LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.SetBlockAttributeValueByTag"));

            if (string.IsNullOrEmpty(tagName)) { throw new ArgumentNullException("tagName"); }

            if (ContentType != acDb.ContentType.BlockContent.ToString())
            {
                throw new InvalidOperationException("MLeader does not contain block content.");
            }

            List<AttributeDefinition> attDefs = Objects.Block.AttributeDefinitions(Block);

            var match = attDefs
                .FirstOrDefault(x => x.Tag.Equals
                (tagName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                try
                {
                    using (var ctx = new acDynApp.DocumentContext(acDynNodes.Document.Current.AcDocument))
                    {
                        AcMLeader acMld = (AcMLeader)ctx.Transaction.GetObject(InternalObjectId, acDb.OpenMode.ForWrite);
                        acDb.AttributeReference attRef = acMld.GetBlockAttribute(match.InternalObjectId);
                        attRef.TextString = value;
                        acMld.SetBlockAttribute(match.InternalObjectId, attRef);
                        return this;
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(e.Message);
                }
            }
            else
            {
                throw new InvalidOperationException($"The MLeader Block does not contain an attribute named {tagName}.");
            }
        }

        /// <summary>
        /// Gets the first vertex of each leader line in an MLeader.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.LeaderPoints",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.ArrowheadLocations")]
        public IList<Point> LeaderPoints
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.ArrowheadLocations"));

                var pnts = new List<Point>();
                for (int i = 0; i < AcMLeader.LeaderLineCount; i++)
                {
                    var acPnt = AcMLeader.GetFirstVertex(i);
                    var dynPnt = GeometryConversions.AcPointToDynPoint(acPnt);
                    pnts.Add(dynPnt);
                }

                return pnts;
            }
        }

        /// <summary>
        /// Gets the scale of the MLeader.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.Scale",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.OverallScale")]
        public double Scale
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.OverallScale"));
                return GetDouble();
            }
        }

        /// <summary>
        /// Gets the location of MLeader MText content.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.TextLocation",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.TextLocation")]
        public Point TextLocation
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.TextLocation"));
                return GeometryConversions.AcPointToDynPoint(AcMLeader.TextLocation);
            }
        }

        /// <summary>
        /// Gets the number of leader lines in the MLeader.
        /// </summary>
        [NodeMigrationMapping(
            "Camber.AutoCAD.Objects.MLeader.LeaderLineCount",
            "Autodesk.AutoCAD.DynamoNodes.Multileader.LeaderCount")]
        public int LeaderLineCount
        {
            get
            {
                LogWarningMessageEvents.OnLogInfoMessage(string.Format(Resources.NODE_OBSOLETE_MIGRATION_MESSAGE, "Multileader.LeaderCount"));
                return GetInt();
            }
        }

        #endregion
    }
}
