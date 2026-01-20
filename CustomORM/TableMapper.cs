using System.Reflection;
using System.Text.Json;

namespace CustomORM
{
    public record ColumnInfo(string Name, string PgType, bool NotNull, bool IsPrimaryKey, bool AutoIncrement);

    public static class TableMapper
    {
        public static (string TableName, List<ColumnInfo> Columns) GetTableSchema(Type entityType)
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
            var tableName = tableAttr?.Name ?? entityType.Name.ToLowerInvariant() + "s";

            var columns = new List<ColumnInfo>();
            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var idAttr = prop.GetCustomAttribute<IdAttribute>();

                var colName = colAttr?.Name ?? prop.Name.ToSnakeCase();
                var pgType = GetPgType(prop.PropertyType);
                var notNull = colAttr?.NotNull ?? false;
                var isPk = idAttr != null;

                columns.Add(new(colName, pgType, notNull, isPk, idAttr?.AutoIncrement ?? false));
            }

            return (tableName, columns);
        }

        private static string GetPgType(Type type)
        {
            return type.Name switch
            {
                "Int32" or "int" => "integer",
                "Int64" or "long" => "bigint",
                "String" => "varchar(255)",
                "DateTime" => "timestamptz",
                "Decimal" => "numeric(12,2)",
                "Boolean" => "boolean",
                "Double" => "double precision",
                _ => "text"
            };
        }
    }

    public static class StringExtensions
    {
        public static string ToSnakeCase(this string str)
        {
            return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x)
                ? "_" + x
                : x.ToString())).ToLowerInvariant();
        }
    }
}
