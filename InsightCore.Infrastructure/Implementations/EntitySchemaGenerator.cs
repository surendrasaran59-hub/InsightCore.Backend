using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using InsightCore.Application.Interfaces;
using InsightCore.Infrastructure;
using InsightCore.Shared;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InsightCore.Infrastructure.Implementations
{
    public class EntitySchemaGenerator: IEntitySchemaGenerator
    {
        private readonly IConfiguration _config;
        private readonly IDBExecution _dBExecution;

        public EntitySchemaGenerator(IConfiguration config, IDBExecution dBExecution)
        {
            _config = config;
            _dBExecution = dBExecution;
        }

        public string GenerateSchema(int clientId, int userId, string blobName)
        {
            StringBuilder message = new StringBuilder();

            try
            {
                _dBExecution.ExecuteScript($"EXEC usp_UpdateClientDataModel {clientId},'{blobName}',{userId},'InProgress'");

                var errors = new List<string>();
                string containerName = _config["BlobStorage:ContainerName"];
                string connectionString = _config["AzureStorage:ConnectionString"];

                var memoryStream = ReadAzureBlob.BlobMemoryStream(connectionString, containerName, blobName);

                using (SpreadsheetDocument document = SpreadsheetDocument.Open(memoryStream, false))
                {
                    Validate(document, errors);

                    if (!errors.Any())
                    {
                        var excelData = ReadExcel(document);
                        string sqlScript = GenerateSql(excelData, clientId, userId);

                        _dBExecution.ExecuteScript(sqlScript);

                        message.AppendLine($"File processed successfully. Database schema has been created.\n");
                        _dBExecution.ExecuteScript($"EXEC usp_UpdateClientDataModel {clientId},'{blobName}',{userId},'Success'");
                    }
                    else
                    {
                        message.AppendLine($"File processing failed due to data validation issues.\n");

                        foreach (var item in errors)
                        {
                            message.AppendLine($"\n" + item);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _dBExecution.ExecuteScript($"EXEC usp_UpdateClientDataModel {clientId},'{blobName}',{userId},'Failed'");
                throw new Exception($"File uploaded successfully, but processing failed. Please review the file and try again.\n");
            }

            return message.ToString();
        }

        private List<DataModelExcelRow> ReadExcel(SpreadsheetDocument doc)
        {
            var list = new List<DataModelExcelRow>();

            var workbookPart = doc.WorkbookPart;
            var sheet = workbookPart.Workbook.Descendants<Sheet>().First();
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            foreach (var row in sheetData.Elements<Row>().Skip(1))
            {
                var cells = row.Elements<Cell>().ToList();
                if (cells.Count < 10) continue;

                list.Add(new DataModelExcelRow
                {
                    SN = GetCellValueByHeaderName("S/N", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    EntityName = GetCellValueByHeaderName("Entity Name", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    EntityDisplayName = GetCellValueByHeaderName("Entity Display Name", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    Module = GetCellValueByHeaderName("Module", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    SubjectArea = GetCellValueByHeaderName("Subject Area", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    AttributeName = GetCellValueByHeaderName("Attribute Name", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    AttributeDisplayName = GetCellValueByHeaderName("Attribute Display Name", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    DataType = GetCellValueByHeaderName("Data Type", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    Length = int.TryParse(GetCellValueByHeaderName("Len", doc, worksheetPart, sheetData, row.RowIndex.Value), out int len) ? len : (int?)null,
                    IsNullable = GetCellValueByHeaderName("Null?", doc, worksheetPart, sheetData, row.RowIndex.Value).ToLower() == "yes",
                    PKType = GetCellValueByHeaderName("PK?", doc, worksheetPart, sheetData, row.RowIndex.Value),
                    Definition = cells.Count > 11 ? GetCellValueByHeaderName("Attribute Definition", doc, worksheetPart, sheetData, row.RowIndex.Value) : ""
                });

            }

            return list;
        }

        private string GenerateSql(List<DataModelExcelRow> data, int clientId, int userID)
        {
            var sql = new StringBuilder();
            sql.AppendLine("BEGIN TRANSACTION;");

            // 1. POPULATE DimensionSchema
            var dims = data.GroupBy(x => new { x.Module, x.SubjectArea });
            foreach (var d in dims)
            {
                sql.AppendLine($"DECLARE @NewDimensionSchemaID INT");
                sql.AppendLine($"IF NOT EXISTS (SELECT 1 FROM MMP_Core.DimensionSchema WHERE DimensionDisplayName = '{d.Key.SubjectArea}' AND ClientID = {clientId})");
                sql.AppendLine($"INSERT INTO MMP_Core.DimensionSchema (ClientID, DimensionSchemaName, DimensionDisplayName) VALUES ({clientId}, '{d.Key.Module}', '{d.Key.SubjectArea}');");
                sql.AppendLine($"SELECT @NewDimensionSchemaID = DimensionSchemaID FROM MMP_Core.DimensionSchema WHERE DimensionDisplayName = '{d.Key.SubjectArea}' AND ClientID = {clientId}");
            }

            LogSteps("SQL Script for Dimension Generated");

            // 2. POPULATE EntitySchema
            var entities = data.GroupBy(x => new { x.EntityName, x.EntityDisplayName, x.Module, x.SubjectArea });

            CreateLandingTableSchema(data);

            foreach (var e in entities)
            {
                //sql.AppendLine($"INSERT INTO MMP_Core.EntitySchema (ClientID, DimensionSchemaID, EntitySchemaName, EntitySchemaDisplayName, DisplayOrder) ");
                //sql.AppendLine($"SELECT {clientId}, DimensionSchemaID, '{e.Key.EntityName}', '{e.Key.EntityDisplayName}', 0 FROM MMP_Core.DimensionSchema ds WHERE DimensionDisplayName = '{e.Key.SubjectArea}' AND ClientID = {clientId} AND NOT EXISTS (SELECT 1 FROM MMP_Core.EntitySchema es WHERE es.ClientID = {clientId} AND es.DimensionSchemaID = ds.DimensionSchemaID AND es.EntitySchemaName = '{e.Key.EntityName}');");

                sql.AppendLine($"MERGE MMP_Core.EntitySchema AS target");
                sql.AppendLine($"USING(");
                sql.AppendLine($"SELECT {clientId} AS ClientID, ds.DimensionSchemaID, '{e.Key.EntityName}' AS EntitySchemaName, '{e.Key.EntityDisplayName}' AS EntitySchemaDisplayName, 0 AS DisplayOrder");
                sql.AppendLine($"FROM MMP_Core.DimensionSchema ds");
                sql.AppendLine($"WHERE ds.DimensionDisplayName = '{e.Key.SubjectArea}' AND ds.ClientID = {clientId}");
                sql.AppendLine($") AS source");
                sql.AppendLine($"ON target.ClientID = source.ClientID");
                sql.AppendLine($"AND target.DimensionSchemaID = source.DimensionSchemaID");
                sql.AppendLine($"AND target.EntitySchemaName = source.EntitySchemaName");
                sql.AppendLine($"WHEN MATCHED THEN");
                sql.AppendLine($"UPDATE SET");
                sql.AppendLine($"EntitySchemaDisplayName = source.EntitySchemaDisplayName,");
                sql.AppendLine($"DisplayOrder = source.DisplayOrder");
                sql.AppendLine($"WHEN NOT MATCHED THEN");
                sql.AppendLine($"INSERT(ClientID, DimensionSchemaID, EntitySchemaName, EntitySchemaDisplayName, DisplayOrder)");
                sql.AppendLine($"VALUES(source.ClientID, source.DimensionSchemaID, source.EntitySchemaName, source.EntitySchemaDisplayName, source.DisplayOrder); ");
            }

            LogSteps("SQL Script for EntitySchema Generated");

            // 3. POPULATE AttributeSchema
            foreach (var row in data)
            {
                //sql.AppendLine($"INSERT INTO MMP_Core.AttributeSchema (ClientID, DimensionSchemaID, EntitySchemaID, AttributeName, AttributeDisplayName, DataType, DataTypeLength, IsNullable, PKType, AttributeDefinition, IsMandatory, ControlType) ");
                //sql.AppendLine($"SELECT {clientId}, ds.DimensionSchemaID, es.EntitySchemaID, '{row.AttributeName}', '{row.AttributeDisplayName}', '{GetDataTypeValue(row.PKType, row.DataType)}', {(row.Length.HasValue ? row.Length.Value.ToString() : "NULL")}, {(row.IsNullable ? 1 : 0)}, '{row.PKType}', '{row.Definition.Replace("'", "''")}', {(row.IsNullable ? 0 : 1)}, 'TextBox'");
                //sql.AppendLine($"FROM MMP_Core.EntitySchema es JOIN MMP_Core.DimensionSchema ds ON es.DimensionSchemaID = ds.DimensionSchemaID ");
                //sql.AppendLine($"WHERE es.EntitySchemaName = '{row.EntityName}' AND ds.DimensionDisplayName = '{row.SubjectArea}' AND es.ClientID = {clientId} AND NOT EXISTS (SELECT 1 FROM MMP_Core.AttributeSchema ats WHERE ats.ClientID = {clientId} AND ats.EntitySchemaID = es.EntitySchemaID AND ats.DimensionSchemaID = ds.DimensionSchemaID AND ats.AttributeName = '{row.AttributeName}');");

                sql.AppendLine($"MERGE MMP_Core.AttributeSchema AS target ");
                sql.AppendLine($"USING( ");
                sql.AppendLine($"SELECT {clientId} AS ClientID, ds.DimensionSchemaID, es.EntitySchemaID, ");
                sql.AppendLine($"'{row.AttributeName}' AS AttributeName, '{row.AttributeDisplayName}' AS AttributeDisplayName, ");
                sql.AppendLine($"'{GetDataTypeValue(row.PKType, row.DataType)}' AS DataType, {(row.Length.HasValue ? row.Length.Value.ToString() : "NULL")} AS DataTypeLength, {(row.IsNullable ? 1 : 0)} AS IsNullable, ");
                sql.AppendLine($"'{row.PKType}' AS PKType, '{row.Definition.Replace("'", "''")}' AS AttributeDefinition, ");
                sql.AppendLine($" {(row.IsNullable ? 0 : 1)} AS IsMandatory, 'TextBox' AS ControlType ");
                sql.AppendLine($"FROM MMP_Core.EntitySchema es ");
                sql.AppendLine($"JOIN MMP_Core.DimensionSchema ds ON es.DimensionSchemaID = ds.DimensionSchemaID ");
                sql.AppendLine($"WHERE es.EntitySchemaName = '{row.EntityName}' ");
                sql.AppendLine($"AND ds.DimensionDisplayName = '{row.SubjectArea}' ");
                sql.AppendLine($"AND es.ClientID = {clientId} ");
                sql.AppendLine($") AS source ");
                sql.AppendLine($"ON target.ClientID = source.ClientID ");
                sql.AppendLine($"AND target.EntitySchemaID = source.EntitySchemaID ");
                sql.AppendLine($"AND target.AttributeName = source.AttributeName ");
                sql.AppendLine($"WHEN MATCHED THEN ");
                sql.AppendLine($"UPDATE SET ");
                sql.AppendLine($"AttributeDisplayName = source.AttributeDisplayName, ");
                sql.AppendLine($"DataType = source.DataType, ");
                sql.AppendLine($"DataTypeLength = source.DataTypeLength, ");
                sql.AppendLine($"IsNullable = source.IsNullable, ");
                sql.AppendLine($"PKType = source.PKType, ");
                sql.AppendLine($"AttributeDefinition = source.AttributeDefinition, ");
                sql.AppendLine($"IsMandatory = source.IsMandatory, ");
                sql.AppendLine($"ControlType = source.ControlType ");
                sql.AppendLine($"WHEN NOT MATCHED THEN ");
                sql.AppendLine($"INSERT( ");
                sql.AppendLine($"ClientID, DimensionSchemaID, EntitySchemaID, AttributeName, AttributeDisplayName, ");
                sql.AppendLine($"DataType, DataTypeLength, IsNullable, PKType, AttributeDefinition, IsMandatory, ControlType ");
                sql.AppendLine($") ");
                sql.AppendLine($"VALUES( ");
                sql.AppendLine($"source.ClientID, source.DimensionSchemaID, source.EntitySchemaID, source.AttributeName, source.AttributeDisplayName, ");
                sql.AppendLine($"source.DataType, source.DataTypeLength, source.IsNullable, source.PKType, source.AttributeDefinition, source.IsMandatory, source.ControlType ");
                sql.AppendLine($"); ");
            }

            LogSteps("SQL Script for AttributeSchema Generated");

            /*
            // 4. INITIALIZE Attribute Data (The Data Instances)
            sql.AppendLine("\n-- Initializing Data Instances in Attribute Table");
            sql.AppendLine("INSERT INTO Attribute (OID, AttributeSchemaID, ClientID, DimensionSchemaID, EntitySchemaID, StartDate, EndDate)");
            sql.AppendLine("SELECT NEXT VALUE FOR dbo.MySequence, ads.AttributeSchemaID, ads.ClientID, ads.DimensionSchemaID, ads.EntitySchemaID, CAST(GETDATE() AS DATE), '9999-12-31'");
            sql.AppendLine($"FROM AttributeSchema ads WHERE ads.ClientID = {clientId};");
            */

            sql.AppendLine("\n------------------------------------------");

            // Final Call for Hierarchy Resolution
            sql.AppendLine($"EXEC dbo.usp_ResolveMDMHierarchy @ClientID = {clientId}, @DimensionSchemaID = @NewDimensionSchemaID;");
            sql.AppendLine("\n------------------------------------------");

            sql.AppendLine("COMMIT TRANSACTION;");

            LogSteps("Resolving Entity Hierarchy");

            return sql.ToString();
        }

        private void CreateLandingTableSchema(List<DataModelExcelRow> data)
        {
            LogSteps("SQL Script Generation for Landing Tables Start");

            var lendingTableScript = new StringBuilder();
            var entities = data.GroupBy(x => new { x.EntityName, x.EntityDisplayName, x.Module, x.SubjectArea });

            foreach (var entity in entities)
            {
                string currentEntity = "Landing" + ToPascalCase(entity.Key.EntityName);

                lendingTableScript.AppendLine($"\n");
                lendingTableScript.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{currentEntity}')\nBEGIN");
                lendingTableScript.AppendLine($"\tCREATE TABLE MMP_App." + currentEntity + "\n\t(");

                foreach (var entityColumn in entity)
                {
                    string columnName = entityColumn.AttributeName;
                    string datatype = GetActualDataType(entityColumn.DataType);
                    //string nullable = entityColumn.IsNullable ? "NULL" : "NOT NULL";
                    string nullable = "NULL";
                    int length = (int)entityColumn.Length;
                    //string pktype = entityColumn.PKType;

                    List<string> withLength = new List<string> { "varchar", "nvarchar", "char", "nchar" };

                    if (columnName.Contains("_OID"))
                        lendingTableScript.AppendLine($"\t\t{columnName} BIGINT IDENTITY(1, 1),");
                    else if (withLength.Contains(datatype))
                        lendingTableScript.AppendLine($"\t\t{columnName} {datatype.ToUpper()} ({length}) {nullable},");
                    else
                        lendingTableScript.AppendLine($"\t\t{columnName} {datatype.ToUpper()} {nullable},");
                }

                lendingTableScript.AppendLine($"\t\tValidationStatus NVARCHAR(100) NULL,");
                lendingTableScript.AppendLine($"\t\tErrorMessage NVARCHAR(MAX) NULL");

                //string ltstr = lendingTableScript.ToString();
                //lendingTableScript = new StringBuilder(ltstr.Substring(0, ltstr.Length - 3));

                lendingTableScript.AppendLine($"\t);\nEND");
            }

            LogSteps("SQL Script Generation for Landing Tables Completed Successfully");
            LogSteps("Creating Landing Tables in Database Start");

            //DBExecution.ExecuteScript(lendingTableScript.ToString());
            _dBExecution.ExecuteScript(lendingTableScript.ToString());

            LogSteps("Creating Landing Tables in Database Completed Successfully");
        }

        private Cell GetCell(SheetData sheetData, string columnName, uint rowIndex)
        {
            var row = sheetData.Elements<Row>()
                               .FirstOrDefault(r => r.RowIndex == rowIndex);

            if (row == null)
                return null;

            string cellRef = columnName + rowIndex;

            return row.Elements<Cell>().FirstOrDefault(c => c.CellReference.Value == cellRef);
        }

        private string GetVal(SpreadsheetDocument doc, Cell c)
        {
            if (c.CellValue == null) return "";
            string v = c.CellValue.InnerText;
            if (c.DataType != null && c.DataType.Value == CellValues.SharedString)
                return doc.WorkbookPart.SharedStringTablePart.SharedStringTable.ChildElements[int.Parse(v)].InnerText;
            return v;
        }

        private string GetColumnNameByHeader(SpreadsheetDocument doc, WorksheetPart worksheetPart, string headerName)
        {
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
            var headerRow = sheetData.Elements<Row>().First(); // assuming row 1

            foreach (var cell in headerRow.Elements<Cell>())
            {
                string cellValue = GetVal(doc, cell);

                if (cellValue == headerName)
                {
                    // Extract column name from CellReference (e.g., "F1" -> "F")
                    return new string(cell.CellReference.Value
                                        .Where(c => Char.IsLetter(c))
                                        .ToArray());
                }
            }

            return null;
        }

        private string GetCellValueByHeaderName(string columnHeaderName, SpreadsheetDocument doc, WorksheetPart worksheetPart, SheetData sheetData, uint rowIndex)
        {
            string column = GetColumnNameByHeader(doc, worksheetPart, columnHeaderName);
            var cell = GetCell(sheetData, column, rowIndex);
            var value = GetVal(doc, cell);

            return value.Trim();
        }

        private string GetDataTypeValue(string pktype, string datatype)
        {
            if (pktype == "BK1")
                return "SuppliedCode";
            else if (pktype == "FK")
                return "ParentCode";
            else
                return datatype;
        }

        private string GetActualDataType(string datatype)
        {
            if (datatype == "SuppliedCode" || datatype == "SuppliedCode")
                return "varchar";
            else
                return datatype;
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;

            string titleCase = textInfo.ToTitleCase(input.ToLower().Replace("_", " ").Replace("-", " "));

            return titleCase.Replace(" ", "");
        }

        private void LogSteps(string message)
        {
            //Console.WriteLine(message);
        }

        /*********************** VALIDATION ***************************/

        private void Validate(SpreadsheetDocument document, List<string> errors)
        {
            ValidateRequiredColumns(document, errors);

            if (!errors.Contains("Missing required columns:"))
            {
                var rows = ReadExcel(document);

                //ValidateDuplicateEntities(rows, errors);
                ValidateDuplicateAttributes(rows, errors);
                ValidateDataTypes(rows, errors);
                ValidateForeignKeys(rows, errors);
                ValidateInvalidCharacters(rows, errors);
            }
        }

        private List<string> ValidateRequiredColumns(SpreadsheetDocument doc, List<string> errors)
        {
            var requiredColumns = new List<string>
            {
                "S/N",
                "Entity Name",
                "Entity Display Name",
                "Module",
                "Subject Area",
                "Attribute Name",
                "Attribute Display Name",
                "Data Type",
                "Len",
                "Null?",
                "PK?",
                "Attribute Definition"
            };

            var wbPart = doc.WorkbookPart;
            var sheet = wbPart.Workbook.Sheets.Elements<Sheet>().First();
            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id);
            var sheetData = wsPart.Worksheet.Elements<SheetData>().First();

            var headerRow = sheetData.Elements<Row>().First();

            var actualColumns = new List<string>();

            foreach (var cell in headerRow.Elements<Cell>())
            {
                string value = GetVal(doc, cell)?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    actualColumns.Add(value);
            }

            // Check missing columns
            var missing = requiredColumns
                .Where(rc => !actualColumns
                    .Any(ac => ac.Equals(rc, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missing.Any())
            {
                errors.Add("Missing required columns:");
                errors.AddRange(missing.Select(m => $" - {m}"));
            }

            // Optional: detect unexpected columns
            var extra = actualColumns
                .Where(ac => !requiredColumns
                    .Any(rc => rc.Equals(ac, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (extra.Any())
            {
                errors.Add("Unexpected columns found:");
                errors.AddRange(extra.Select(e => $" - {e}"));
            }

            return errors;
        }

        private void ValidateDuplicateAttributes(List<DataModelExcelRow> rows, List<string> errors)
        {
            var duplicates = rows
                .GroupBy(r => new { r.EntityName, r.AttributeName })
                .Where(g => g.Count() > 1);

            foreach (var dup in duplicates)
            {
                errors.Add($"Duplicate Attribute '{dup.Key.AttributeName}' in Entity: '{dup.Key.EntityName}'");
            }
        }

        private void ValidateDataTypes(List<DataModelExcelRow> rows, List<string> errors)
        {
            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "int","bigint","smallint","tinyint",
                "bit",
                "decimal","numeric","money","smallmoney",
                "float","real",
                "date","datetime","datetime2","smalldatetime","time",
                "char","varchar","text",
                "nchar","nvarchar","ntext",
                "uniqueidentifier"
            };

            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.DataType))
                {
                    string normalized = row.DataType.Split('(')[0].Trim();

                    if (!validTypes.Contains(normalized))
                    {
                        //errors.Add($"Invalid DataType '{row.DataType}' at Row {row.RowNumber}");
                        errors.Add($"Invalid DataType '{row.DataType}' in Dimension: '{row.SubjectArea}', Entity Name: '{row.EntityName}', Attribute Name: '{row.AttributeName}'");
                    }
                }
            }
        }

        private void ValidateForeignKeys(List<DataModelExcelRow> rows, List<string> errors)
        {
            var bkSet = rows
                .Where(r => r.PKType.Equals("BK1", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.AttributeName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var fkRows = rows
                .Where(r => r.PKType.Equals("FK", StringComparison.OrdinalIgnoreCase));

            foreach (var fk in fkRows)
            {
                if (!bkSet.Contains(fk.AttributeName))
                {
                    //errors.Add($"FK '{fk.Attribute}' at Row {fk.RowNumber} does not match any BK1");
                    errors.Add($"FK '{fk.AttributeName}' does not match any Parent Code - Dimension: '{fk.SubjectArea}', Entity Name: '{fk.EntityName}'");
                }
            }
        }

        private void ValidateInvalidCharacters(List<DataModelExcelRow> rows, List<string> errors)
        {
            foreach (var row in rows)
            {
                if (!ValidateInvalidCharacters(row.SubjectArea))
                    errors.Add($"Dimension Name contains invalid character(s) - Dimension: '{row.SubjectArea}'");

                if (!ValidateInvalidCharacters(row.EntityName))
                    errors.Add($"Entity Name contains invalid character(s) - Dimension: '{row.SubjectArea}', Entity Name: '{row.EntityName}'");

                if (!ValidateInvalidCharacters(row.AttributeName))
                    errors.Add($"Attribute Name contains invalid character(s) - Dimension: '{row.SubjectArea}', Entity Name: '{row.EntityName}', Attribute Name: {row.AttributeName}");
            }
        }

        private bool ValidateInvalidCharacters(string input)
        {
            return Regex.IsMatch(input, @"^[A-Za-z0-9_]+$");
        }
    }
}