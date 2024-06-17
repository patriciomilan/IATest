using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Updating.Ai.TestDB.Services
{
    public class RepositoryDB : IRepositoryDB
    {
        public string Database { get; set; }
        public string StringConection { get; set; }

        private static Dictionary<string,string> modelos;

        public RepositoryDB(IConfiguration configuration)
        {
            if (modelos == null)
                modelos = new Dictionary<string, string>();

            StringConection = configuration["Configuracion:stringConexion"];
        }

        public string GetStringConection()
        {
            return StringConection.Replace("master", Database);
        }
        
        public DataTable GetDatatable(string sql)
        {
            if (string.IsNullOrEmpty(StringConection))
                throw new ArgumentException("No existe un string de conexión");
            if (string.IsNullOrEmpty(sql))
                throw new ArgumentException("Se debe enviar un string SQL");

            using (SqlConnection cnn = new SqlConnection(GetStringConection()))
            {
                cnn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, cnn))
                {
                    DataTable dt = new DataTable();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                    return dt;
                }
            }
        }

        public List<object> GetData(string sql)
        {
            var dt = GetDatatable(sql);
            Dictionary<string, object> dictionaryObject;
            var lista = new List<object>();
            foreach (DataRow row in dt.Rows)
            {
                dictionaryObject = new Dictionary<string, object>();
                foreach (DataColumn column in dt.Columns)
                {
                    dictionaryObject.Add(column.ColumnName, row[column.ColumnName]);
                }
                lista.Add(dictionaryObject);
            }
            return lista;
        }

        public string GetModel(string database)
        {
            Database = database;
            if (modelos.ContainsKey(Database))
                return modelos[Database];

            StringBuilder sqlTablas = new StringBuilder();
            StringBuilder sqlRelaciones = new StringBuilder();

            // Get table definitions
            DataTable tables = GetDatatable("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
            foreach (DataRow tableRow in tables.Rows)
            {
                string tableName = tableRow["TABLE_NAME"].ToString();
                if (tableName == "sysdiagrams")
                    continue;

                sqlTablas.AppendLine($"CREATE TABLE {tableName} (");

                // Obtener las columnas
                DataTable columns = GetDatatable($"SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'");
                foreach (DataRow columnRow in columns.Rows)
                {
                    string columnName = columnRow["COLUMN_NAME"].ToString();
                    string dataType = columnRow["DATA_TYPE"].ToString();
                    string nullableText = columnRow["IS_NULLABLE"].ToString() == "YES" ? "NULL" : "NOT NULL";
                    string largo = "";
                    if (dataType == "nvarchar" || dataType == "varchar" || dataType == "char" || dataType == "nchar")
                    {
                        if (columnRow["CHARACTER_MAXIMUM_LENGTH"].ToString() == "-1")
                            largo = "(MAX)";
                        else
                            largo = $"({columnRow["CHARACTER_MAXIMUM_LENGTH"].ToString()})";
                    }
                    string columnDefinition = $"{columnName} {dataType}{largo} {nullableText}, ";
                    sqlTablas.AppendLine(columnDefinition);
                }

                // Obtener las primary keys
                DataTable primeryKeys = GetDatatable(GetSqlPrimaryKeys(tableName));
                if (primeryKeys.Rows.Count > 0)
                {
                    string pkName = primeryKeys.Rows[0]["CONSTRAINT_NAME"].ToString();
                    sqlTablas.AppendLine($"CONSTRAINT [{pkName}] PRIMARY KEY (");
                    List<string> primaryKeyColumns = new List<string>(); 
                    foreach (DataRow keyRow in primeryKeys.Rows)
                    {
                        string constraintName = keyRow["CONSTRAINT_NAME"].ToString();
                        string columnName = keyRow["COLUMN_NAME"].ToString();
                        primaryKeyColumns.Add($"[{columnName}] ASC");
                    }
                    string primaryKeyColumnsString = string.Join(", ", primaryKeyColumns);
                    sqlTablas.AppendLine(primaryKeyColumnsString);
                    sqlTablas.AppendLine(")");
                }

                // Obtener Relaciones
                DataTable foreignKeys = GetDatatable(GetSqlRelaciones(tableName));
                foreach (DataRow foreignKeyRow in foreignKeys.Rows)
                {
                    string constraintName = foreignKeyRow["ForeignKeyName"].ToString();
                    string columnName = foreignKeyRow["ChildColumn"].ToString();
                    string referencedTableName = foreignKeyRow["ParentTable"].ToString();
                    string referencedColumnName = foreignKeyRow["ParentColumn"].ToString();
                    string foreignKeyDefinition = $"ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [{constraintName}] FOREIGN KEY([{columnName}]) REFERENCES [dbo].[{referencedTableName}] ([{referencedColumnName}])\r\n";
                    foreignKeyDefinition += $"ALTER TABLE [dbo].[{tableName}] CHECK CONSTRAINT [{constraintName}]\r\n";
                    sqlRelaciones.AppendLine(foreignKeyDefinition);
                }
                sqlTablas.AppendLine(");");
            }
            sqlTablas.AppendLine(sqlRelaciones.ToString());
            modelos[Database] = sqlTablas.ToString();
            return modelos[Database];
        }

        //Get SQL for list of databases
        public List<string> GetDatabases()
        {
            List<string> dbs = new List<string>();
            Database = "master";
            var sql = "SELECT name FROM dbo.sysdatabases";
            var databases = GetDatatable(sql);
            foreach (DataRow tableRow in databases.Rows)
            {
                string tableName = tableRow["name"].ToString();
                if (tableName == "master" || tableName == "tempdb" || tableName == "model" || tableName == "msdb")
                    continue;
                dbs.Add(tableName);
            }
            return dbs;
        }

        //Obtener lista de Tablas
        public List<string> GetAllTables(string database)
        {
            Database = database;
            List<string> tablas = new List<string>();
            DataTable tables = GetDatatable("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
            foreach (DataRow tableRow in tables.Rows)
            {
                string tableName = tableRow["TABLE_NAME"].ToString();
                if (tableName == "sysdiagrams")
                    continue;
                tablas.Add(tableName);
            }
            return tablas;
        }

        private string GetSqlRelaciones(string tableChild)
        {
            string sql = "SELECT fk.name AS ForeignKeyName, tp.name AS ParentTable, cp.name AS ParentColumn, tr.name AS ChildTable, cr.name AS ChildColumn ";
            sql += "FROM sys.foreign_keys AS fk INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id ";
            sql += "INNER JOIN sys.tables AS tp ON fk.referenced_object_id = tp.object_id ";
            sql += "INNER JOIN sys.columns AS cp ON tp.object_id = cp.object_id AND fkc.referenced_column_id = cp.column_id ";
            sql += "INNER JOIN sys.tables AS tr ON fk.parent_object_id = tr.object_id ";
            sql += "INNER JOIN sys.columns AS cr ON tr.object_id = cr.object_id AND fkc.parent_column_id = cr.column_id ";
            sql += $"where tr.name = '{tableChild}'";
            return sql;
        }

        private string GetSqlPrimaryKeys(string tableName)
        {
            string sql = "SELECT kcu.TABLE_NAME, kcu.COLUMN_NAME, kcu.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc ";
            sql += "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA ";
            sql += $"WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND kcu.TABLE_NAME = '{tableName}' AND kcu.TABLE_SCHEMA = 'dbo';";
            return sql;
        }
    }
}
