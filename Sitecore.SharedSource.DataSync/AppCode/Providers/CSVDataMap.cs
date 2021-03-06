﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Security.Accounts;
using Sitecore.SharedSource.Logger.Log;
using Sitecore.SharedSource.Logger.Log.Builder;

namespace Sitecore.SharedSource.DataSync.Providers
{
    public class CSVDataMap : BaseDataMap
    {
        protected const char DefaultColumnDelimiterCharacter = ';';
        protected const char DefaultQueryDelimiterCharacter = '=';

        #region Properties

        protected Dictionary<string, int> HeaderColumns { get; set; }
        protected IList<object> ImportData { get; set; }
        protected bool RemovedTrailingCharacter = false;
        protected char ColumnDelimiterCharacter { get; set; }


        #endregion Properties

        #region Constructor

        public CSVDataMap(Database db, Item importItem, LevelLogger logger)
            : base(db, importItem, logger)
        {
            
            Data = importItem[FieldNameData];
            
            HeaderColumns = new Dictionary<string, int>();
            ImportData = new List<object>();
            InitializeColumnDelimiterCharacter(importItem);
        }

        public CSVDataMap(Database db, Item importItem, LevelLogger logger, string data)
            : base(db, importItem, logger)
        {
            Data = data;
            HeaderColumns = new Dictionary<string, int>();
            ImportData = new List<object>();
            InitializeColumnDelimiterCharacter(importItem);
        }

        public override void ValidateImportData(IList<object> importData, ref LevelLogger logger)
        {
            var validateImportDataLogger = logger.CreateLevelLogger();
            foreach (var fieldDefinition in FieldDefinitions)
            {
                var newField = fieldDefinition.GetExistingFieldNames();
                foreach (var existingFieldName in fieldDefinition.GetExistingFieldNames())
                {
                    if (!HeaderColumns.ContainsKey(existingFieldName.ToLower()))
                    {
                        validateImportDataLogger.AddError("A column was expected but not found", String.Format("--- A column with name {0} was expected.\r\n", existingFieldName));
                    }
                }
            }
            if (!validateImportDataLogger.HasErrors())
            {   
                base.ValidateImportData(importData, ref validateImportDataLogger);
            }
        }

        private void InitializeColumnDelimiterCharacter(Item importItem)
        {
            var columnDelimiterCharacter = importItem.Fields["Column Delimiter Character"].Value;
            if (!String.IsNullOrEmpty(columnDelimiterCharacter))
            {
                char currentColumnDelimiterCharacter;
                if (!Char.TryParse(columnDelimiterCharacter, out currentColumnDelimiterCharacter))
                {
                    Logger.AddError("Error", String.Format(
                        "A wrong format was provided in the field '{0}' on the importItem. This field is used to set the Column Delimiter Character. Please correct to a valid char ie ;."
                        , columnDelimiterCharacter));
                    ColumnDelimiterCharacter = DefaultColumnDelimiterCharacter;
                }
                else
                {
                    ColumnDelimiterCharacter = currentColumnDelimiterCharacter;
                }
            }
            ColumnDelimiterCharacter = DefaultColumnDelimiterCharacter;
        }

        #endregion Constructor

        #region Override Methods
        
        public override IList<object> GetImportData()
        {
            if (ImportData.Count > 0)
            {
                return ImportData;
            }
            try
            {
                var csvData = !String.IsNullOrEmpty(Data)
                                  ? Data
                                  : CSVFileData;

                if (!String.IsNullOrEmpty(csvData))
                {
                    csvData = ReplaceWithUniversalNewLineCharacters(csvData);
                    string[] lines = Regex.Split(csvData, Environment.NewLine);
                    // Process the Header Columns
                    if (lines.Any())
                    {
                        var headerColumnLine = lines[0];
                        if (!String.IsNullOrEmpty(headerColumnLine))
                        {
                            // We want to remove the trailing ; from the header columns
                            if (headerColumnLine.EndsWith(ColumnDelimiterCharacter.ToString(CultureInfo.CurrentCulture)))
                            {
                                headerColumnLine = headerColumnLine.Substring(0, headerColumnLine.Length - 1);
                                RemovedTrailingCharacter = true;
                            }

                            string[] headerColumnArray = headerColumnLine.Split(ColumnDelimiterCharacter);
                            for (int i = 0; i < headerColumnArray.Length; i++)
                            {
                                string header = headerColumnArray[i];
                                if (!String.IsNullOrEmpty(header))
                                {
                                    header = header.ToLower();
                                    if (!HeaderColumns.ContainsKey(header))
                                    {
                                        HeaderColumns.Add(header, i);
                                    }
                                    else
                                    {
                                        Logger.AddError("Error", String.Format(
                                        "A columnHeader was identical to another columnHeader in the GetImportData in the CSVDataMap. Index: {0}. header: {1}. headerColumnLine: {2}", i, header, headerColumnLine));
                                    }
                                }
                                else
                                {
                                    Logger.AddError("Error", String.Format(
                                    "A columnHeader was null or empty in the GetImportData in the CSVDataMap. Index: {0}. headerColumnLine: {1}", i, headerColumnLine));
                                }
                            }
                        }
                        else
                        {
                            Logger.AddError("Error", String.Format(
                                    "The HeaderColumn in the GetImportData in the CSVDataMap was null or empty. Please make sure that the first line is has the fieldnames for the columns."));
                        }

                        if (lines.Length > 1)
                        {
                            // Process rest of the list
                            for (int i = 1; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                if (!String.IsNullOrEmpty(line) && !String.IsNullOrEmpty(line.Trim()))
                                {
                                    string errorMessage = String.Empty;

                                    var verifyConditionLogger = Logger.CreateLevelLogger();
                                    bool isVerified = VerifyConditionFromQuery(line, ref verifyConditionLogger);
                                    if (!String.IsNullOrEmpty(errorMessage))
                                    {
                                        verifyConditionLogger.AddError("Error", String.Format("A failure occured while verifying a line in the GetImportData method. The line was not added and the import was aborted. The method that failed was VerifyConditionFromQuery method. ErrorMessage: {0}.",
                                        errorMessage));
                                        break;
                                    }
                                    if (isVerified)
                                    {
                                        ImportData.Add(line);
                                    }
                                }
                                else
                                {
                                    Logger.AddError("Error", String.Format(
                                        "A row in the GetImportData in the CSVDataMap was null or empty. Line: {0}",
                                        line));
                                }
                            }
                        }
                        else
                        {
                            Logger.AddError("Error", String.Format("The importRows did only contain one line. The import was aborted."));
                        }
                    }
                    else
                    {
                        Logger.AddError("Error", String.Format(
                            "The GetImportData in the CSVDataMap failed because the provided CSVdata didn't contain any rows."));
                    }
                }
                else
                {
                    Logger.AddError("Error", String.Format("The GetImportData in the CSVDataMap failed because the provided data was empty."));
                }
            }
            catch (Exception ex)
            {
                Logger.AddError("Error", String.Format("The GetFieldValue method failed with an exception. Exception: {0}.", ex));
            }
            return ImportData;
        }

        protected static string ReplaceWithUniversalNewLineCharacters(string csvData)
        {
            if (!String.IsNullOrEmpty(csvData))
            {
                if (csvData.Contains("\r\n"))
                {
                    csvData = csvData.Replace("\r\n", Environment.NewLine);
                    return csvData;
                }
                if (csvData.Contains("\n"))
                {
                    csvData = csvData.Replace("\n", Environment.NewLine);
                    return csvData;
                }
                if (csvData.Contains("\r"))
                {
                    csvData = csvData.Replace("\r", Environment.NewLine);
                    return csvData;
                }
            }
            return csvData;
        }

        public virtual bool VerifyConditionFromQuery(string line, ref LevelLogger logger)
        {
            var verifyConditionLogger = logger.CreateLevelLogger();
            if (!String.IsNullOrEmpty(Query))
            {
                if (Query.Contains(DefaultQueryDelimiterCharacter))
                {
                    var queryValues = Query.Split(DefaultQueryDelimiterCharacter);
                    if (queryValues.Length == 2)
                    {
                        string key = queryValues[0];
                        string value = queryValues[1];
                        if (!String.IsNullOrEmpty(key) && !String.IsNullOrEmpty(value))
                        {
                            var retrievedValue = GetFieldValue(line, key, ref verifyConditionLogger);
                            return value.ToLower().Equals(retrievedValue.ToLower());
                        }
                        else
                        {
                            verifyConditionLogger.AddError("The Query was in wrong format. Either Key or value was null or empty", String.Format("The provided Query '{0}' was in wrong format. Either of the key or value was null or empty. Format should be 'key{1}value' ie 'Category{1}Summer'. " +
                                                          "Key: {2}. Value: {3}. Line: {4}.", Query, DefaultQueryDelimiterCharacter, key, value, line));
                        }
                    }
                    else
                    {
                        verifyConditionLogger.AddError("The Query was in wrong format.", String.Format("The provided Query '{0}' was in wrong format. Format should be 'key{1}value' ie 'Category{1}Summer'. " +
                                                      "Line: {2}. QueryValues.Length: {3}.", Query, DefaultQueryDelimiterCharacter, line, queryValues.Length));
                    }
                }
                else
                {
                    verifyConditionLogger.AddError("The Query didn't contain a valid delimiter character", String.Format("The provided Query '{0}' didn't contain a valid delimiter character. This character must be: '{1}'. Line: {2}.  he VerifyConditionFromQuery method failed because the Query was null or empty.",
                        Query, DefaultQueryDelimiterCharacter, line));
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// gets custom data from a DataRow
        /// </summary>
        /// <param name="importRow"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public override string GetFieldValue(object importRow, string fieldName, ref LevelLogger logger)
        {
            var getFieldValueLogger = logger.CreateLevelLogger();
            try
            {
                if (importRow != null)
                {
                    if (!String.IsNullOrEmpty(fieldName))
                    {
                        fieldName = fieldName.ToLower();
                        var semicolonSeperatedString = importRow + "";
                        if (!String.IsNullOrEmpty(semicolonSeperatedString))
                        {
                            if (RemovedTrailingCharacter)
                            {
                                if (
                                    semicolonSeperatedString.EndsWith(
                                        ColumnDelimiterCharacter.ToString(CultureInfo.CurrentCulture)))
                                {
                                    semicolonSeperatedString = semicolonSeperatedString.Substring(0,
                                                                                                  semicolonSeperatedString
                                                                                                      .Length - 1);
                                }
                            }
                            string[] values = semicolonSeperatedString.Split(ColumnDelimiterCharacter);
                            var count = values.Length;

                            if (count != HeaderColumns.Count())
                            {
                                getFieldValueLogger.AddError(CategoryConstants.NumberOfFieldsInRowWasDifferentFromNumberOfFieldsInHeader, String.Format(
                                        "The GetFieldValue method failed because the number of fields in row, was different from the number of fields in header. " +
                                        "ImportRow: {0}. fieldname: {1}. count: {2}. HeaderColumns.Count: {3}.",
                                        GetImportRowDebugInfo(importRow), fieldName, count, HeaderColumns.Count()));
                                return String.Empty;
                            }
                            if (HeaderColumns.ContainsKey(fieldName))
                            {
                                var index = HeaderColumns[fieldName];
                                if (index <= count - 1 && index >= 0)
                                {
                                    var value = values[index];
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        return value;
                                    }
                                }
                                else
                                {
                                    getFieldValueLogger.AddError(CategoryConstants.TheRetrievedIndexForTheColumnWasOutOfRange, String.Format(
                                            "The GetFieldValue method failed because the retrieved index for the column was out of range. ImportRow: {0}. fieldname: {1}. count: {2}. index: {3}.",
                                            GetImportRowDebugInfo(importRow), fieldName, count, index));
                                }
                            }
                            else
                            {
                                getFieldValueLogger.AddError(CategoryConstants.TheFieldDoesNotExistInTheImportrow, String.Format(
                                        "The GetFieldValue method failed because the field does not exist in the importRow. fieldname: {0}. count: {1}.",
                                        fieldName, count));
                            }
                        }
                        else
                        {
                            getFieldValueLogger.AddError(CategoryConstants.FailedToCastToStringBecauseValueWasNull, String.Format(
                                    "The GetFieldValue method failed because the importRow was cast to a string, but it was null. ImportRow: {0}. fieldname: {1}.",
                                    GetImportRowDebugInfo(importRow), fieldName));
                        }
                    }
                    else
                    {
                        getFieldValueLogger.AddError(CategoryConstants.TheFieldnameArgumentWasNullOrEmpty, String.Format(
                                "The GetFieldValue method failed because the 'fieldName' argument was null or empty. FieldName: {0}. ImportRow: {1}.",
                                fieldName, GetImportRowDebugInfo(importRow)));
                    }
                }
                else
                {
                    getFieldValueLogger.AddError(CategoryConstants.TheImportRowWasNull, String.Format(
                            "The GetFieldValue method failed because the Import Row was null. FieldName: {0}.",
                            fieldName));
                }
            }
            catch (Exception ex)
            {
                getFieldValueLogger.AddError(CategoryConstants.GetFieldValueFailed, String.Format("The GetFieldValue method failed with an exception. ImportRow: {0}. FieldName: {1}. Exception: {2}.", GetImportRowDebugInfo(importRow), fieldName, ex));
            }
            return String.Empty;
        }

        public override string GetImportRowDebugInfo(object importRow)
        {
            if (importRow != null)
            {
                return importRow.ToString();
            }
            return String.Empty;
        }

        protected string CSVFileData
        {
            get
            {
                var datasource = DataSourceString;
                if (File.Exists(datasource))
                {
                    StreamReader streamreader=null;
                    try
                    {
                        streamreader = new StreamReader(datasource,true);
                        var fileStream = streamreader.ReadToEnd();
                        return fileStream;
                    }
                    catch (Exception ex)
                    {
                        Logger.AddError("Error",String.Format("Reading the file failed with an exception. Exception: {0}.", ex));
                        if (streamreader != null)
                        {
                            streamreader.Close();
                        }
                    }
                    finally
                    {
                        if (streamreader != null)
                        {
                            streamreader.Close();
                        }
                    }
                }
                else
                {
                    Logger.AddError("Error",
                                   String.Format(
                                       "The DataSource filepath points to a file that doesnt exist. DataSource: '{0}'",
                                       DataSourceString));
                }
                return string.Empty;
            }
        }

        #endregion Override Methods

        #region Methods

        #endregion Methods
    }
}