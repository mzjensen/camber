﻿#region references
using Dynamo.Graph.Nodes;
using System;
using acApp = Autodesk.AutoCAD.ApplicationServices;
using acDb = Autodesk.AutoCAD.DatabaseServices;
using acDynNodes = Autodesk.AutoCAD.DynamoNodes;
#endregion

namespace Camber.AutoCAD
{
    public static class Document
    {
        #region query methods
        /// <summary>
        /// Gets if a Document is a named file on the disk (as opposed to a new drawing that has not yet been saved).
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        [NodeCategory("Query")]
        public static bool IsNamedDrawing(acDynNodes.Document document)
        {
            return document.AcDocument.IsNamedDrawing;
        }

        /// <summary>
        /// Gets if a Document is read-only.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        [NodeCategory("Query")]
        public static bool IsReadOnly(acDynNodes.Document document)
        {
            return document.AcDocument.IsReadOnly;
        }

        /// <summary>
        /// Gets the version of a Document when it was last saved with the current session.
        /// Returns null if the Document has not been saved with the current session.
        /// Returns 0 for any version prior to 2004.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        [NodeCategory("Query")]
        public static int? LastSavedVersion(acDynNodes.Document document)
            => GetDatabaseVersionInfo(document.AcDocument.Database, true);

        /// <summary>
        /// Gets the version of a Document when it was first opened.
        /// Returns 0 for any version prior to 2004.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        [NodeCategory("Query")]
        public static int? OriginalVersion(acDynNodes.Document document)
            => GetDatabaseVersionInfo(document.AcDocument.Database, false);
        #endregion

        #region action methods
        /// <summary>
        /// Executes a particular command string. Returns true if the command was executed successfully and false otherwise.
        /// Ensure that a space or \n is included at the end of any string that you wish to be executed.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="echoInCommandLine">Show the command in the command line?</param>
        public static bool SendCommand(acDynNodes.Document document, string command, bool echoInCommandLine = true)
        {
            if (string.IsNullOrEmpty(command)) { throw new ArgumentException("Command string is null or empty."); }

            try
            {
                document.AcDocument.SendStringToExecute(command, false, false, echoInCommandLine);
                return true;
            }
            catch
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// Gets the value of a system variable.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="variableName">The name of the system variable.</param>
        /// <returns></returns>
        public static object GetSystemVariable(acDynNodes.Document document, string variableName)
        {
            return acApp.Application.GetSystemVariable(variableName);
        }

        /// <summary>
        /// Sets the value of a system variable.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="variableName">The name of the system variable.</param>
        /// <param name="newValue">The new value to assign.</param>
        /// <returns></returns>
        public static void SetSystemVariable(acDynNodes.Document document, string variableName, object newValue)
        {
            // AutoCAD needs 16-bit integers, but from Dynamo they come as 64-bit.
            // Without this check, an eInvalidInput exception will be thrown when trying to set integer values.
            if (newValue is long) { newValue = Convert.ToInt16(newValue); }
            
            try
            {
                acApp.Application.SetSystemVariable(variableName, newValue);
                var result = GetSystemVariable(document, variableName);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(e.Message);
            }
        }
        #endregion

        /// <summary>
        /// Gets the DWG version of a database.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="getLastSaved">
        /// True = get the version that was last saved by the current session.
        /// False = get the original file version when the drawing was opened and prior to saving.
        /// </param>
        /// <returns>
        /// Returns null if the database has not been saved in the current session.
        /// Returns 0 if the DWG version is prior to 2004.
        /// </returns>
        internal static int? GetDatabaseVersionInfo(acDb.Database database, bool getLastSaved)
        {
            acDb.DwgVersion dwgVersion;

            if (getLastSaved)
            {
                // The version that was last saved by the current session.
                // This will return MC0To0 if the drawing has not been saved in the current session,
                // even if the drawing has previously been saved to disk.
                dwgVersion = database.LastSavedAsVersion;
            }
            else
            {
                // The version of the file when it was opened.
                // This will return AC1032 if the database in the current session has never been saved.
                dwgVersion = database.OriginalFileVersion;
            }

            switch (dwgVersion)
            {
                case acDb.DwgVersion.AC1032:
                    return 2018;
                case acDb.DwgVersion.AC1027:
                    return 2013;
                case acDb.DwgVersion.AC1024:
                    return 2010;
                case acDb.DwgVersion.AC1021:
                    return 2007;
                case acDb.DwgVersion.AC1800:
                    return 2004;
                case acDb.DwgVersion.MC0To0:
                    return null;
                default:
                    return 0;
            }
        }
    }
}
