﻿#region references
using acDb = Autodesk.AutoCAD.DatabaseServices;
using acGeom = Autodesk.AutoCAD.Geometry;
using acDynApp = Autodesk.AutoCAD.DynamoApp.Services;
using acDynNodes = Autodesk.AutoCAD.DynamoNodes;
using AcMLeader = Autodesk.AutoCAD.DatabaseServices.MLeader;
using Autodesk.DesignScript.Geometry;
using DynamoServices;
using Camber.Utils;
using Autodesk.DesignScript.Runtime;
#endregion

namespace Camber.AutoCAD
{
    [RegisterForTrace]
    public sealed class MLeader : ObjectExtensions
    {
        #region properties
        internal AcMLeader AcMLeader => AcObject as AcMLeader;

        /// <summary>
        /// Gets the position of MLeader Block content.
        /// </summary>
        public Point BlockPosition => GeometryConversions.AcPointToDynPoint(AcMLeader.BlockPosition);

        /// <summary>
        /// Gets the Block object of MLeader Block content.
        /// </summary>
        public acDynNodes.Block Block
        {
            get
            {
                acDynNodes.Document document = acDynNodes.Document.Current;
                using (var ctx = new acDynApp.DocumentContext(document.AcDocument))
                {
                    acDb.ObjectId blockId = AcMLeader.BlockContentId;
                    acDb.BlockTableRecord acBlock = (acDb.BlockTableRecord)blockId.GetObject(acDb.OpenMode.ForRead);
                    return acDynNodes.Document.Current.BlockByName(acBlock.Name);
                }
            }
        }

        /// <summary>
        /// Gets the MLeader's content type.
        /// </summary>
        public string ContentType => GetString();

        /// <summary>
        /// Gets the number of leaders in the MLeader.
        /// </summary>
        public int LeaderCount => GetInt();

        /// <summary>
        /// Gets the number of leader lines in the MLeader.
        /// </summary>
        public int LeaderLineCount => GetInt();

        /// <summary>
        /// Gets the scale of the MLeader.
        /// </summary>
        public double Scale => GetDouble();

        /// <summary>
        /// Gets the text height of MLeader MText content.
        /// </summary>
        public double TextHeight => GetDouble();

        /// <summary>
        /// Gets the location of MLeader MText content.
        /// </summary>
        public Point TextLocation => GeometryConversions.AcPointToDynPoint(AcMLeader.TextLocation);
        #endregion

        #region constructors
        [SupressImportIntoVM]
        internal static MLeader GetByObjectId(acDb.ObjectId mLeaderId)
            => ObjectSupport.Get<MLeader, AcMLeader>
            (mLeaderId, (mLeader) => new MLeader(mLeader));

        internal MLeader(AcMLeader acMLeader, bool isDynamoOwned = false) : base(acMLeader, isDynamoOwned) { }

        /// <summary>
        /// Creates an MLeader with MText content.
        /// </summary>
        /// <param name="point">Arrowhead point</param>
        /// <param name="text">MText contents</param>
        /// <param name="draggedOffset"></param>
        /// <returns></returns>
        public static MLeader ByPointText(Point point, string text, Vector draggedOffset)
        {
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
                        mtext.SetContentsRtf(text);
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
                    mText.SetContentsRtf(text);
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
        /// Creates an MLeader with Block content.
        /// </summary>
        /// <param name="point">Arrowhead point</param>
        /// <param name="block"></param>
        /// <param name="draggedOffset"></param>
        /// <returns></returns>
        public static MLeader ByPointBlock(Point point, acDynNodes.Block block, Vector draggedOffset)
        {
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
        #endregion

        #region methods
        public override string ToString() => $"MLeader(ContentType = {ContentType})";
        #endregion
    }
}
