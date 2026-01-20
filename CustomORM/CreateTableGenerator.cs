using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomORM
{
    public static class CreateTableGenerator
    {
        public static string GenerateCreateTable(Type entityType)
        {
            var (tableName, columns) = TableMapper.GetTableSchema(entityType);
            var sb = new StringBuilder();

            sb.AppendLine($"CREATE TABLE IF NOT EXISTS {tableName} (");

            var columnDefs = new List<string>();

            foreach (var col in columns)
            {
                var def = $"  {col.Name} {col.PgType}";

                if (col.IsPrimaryKey)
                {
                    if (col.AutoIncrement)
                        def += " GENERATED ALWAYS AS IDENTITY";
                    def += " PRIMARY KEY";
                }

                if (col.NotNull && !col.IsPrimaryKey)
                    def += " NOT NULL";

                columnDefs.Add(def);
            }

            sb.AppendLine(string.Join(",\n", columnDefs));
            sb.AppendLine(");");

            return sb.ToString();
        }

        public static string GenerateAddForeignKey(string tableName, string columnName, string referencedTable, string referencedColumn = "id")
        {
            return $"DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'fk_{tableName}_{columnName}') THEN ALTER TABLE {tableName} ADD CONSTRAINT fk_{tableName}_{columnName} FOREIGN KEY ({columnName}) REFERENCES {referencedTable}({referencedColumn}); END IF; END $$;";
        }

    }
}
