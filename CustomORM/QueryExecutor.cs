using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;

namespace CustomORM
{
    public class QueryExecutor
    {
        private readonly string _connectionString;

        public QueryExecutor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task ExecuteAsync(string sql)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL Error: {ex.Message}");
            }
        }

        public List<T> ExecuteQuery<T>(string sql, Dictionary<string, object> parameters = null) where T : new()
        {
            var results = new List<T>();

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var obj = new T();
                    var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (var prop in properties)
                    {
                        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                        var colName = colAttr?.Name ?? prop.Name.ToSnakeCase();

                        try
                        {
                            if (reader.HasColumn(colName))
                            {
                                var value = reader[colName];
                                if (value != DBNull.Value)
                                {
                                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                    prop.SetValue(obj, Convert.ChangeType(value, targetType));
                                }
                            }
                        }
                        catch { }
                    }

                    results.Add(obj);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Query Error: {ex.Message}");
            }

            return results;
        }

        public int ExecuteInsert<T>(T entity) where T : new()
        {
            try
            {
                var (tableName, columns) = TableMapper.GetTableSchema(typeof(T));
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var columnNames = new List<string>();
                var paramNames = new List<string>();
                var parameters = new Dictionary<string, object>();
                int paramIndex = 0;

                foreach (var prop in properties)
                {
                    if (prop.PropertyType.IsGenericType &&
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                        continue;

                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                    var idAttr = prop.GetCustomAttribute<IdAttribute>();

                    if (idAttr != null && idAttr.AutoIncrement)
                        continue;

                    var colName = colAttr?.Name ?? prop.Name.ToSnakeCase();
                    var value = prop.GetValue(entity);

                    columnNames.Add(colName);
                    var paramName = $"@p{paramIndex}";
                    paramNames.Add(paramName);
                    parameters[paramName] = value ?? DBNull.Value;
                    paramIndex++;
                }

                var sql = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)}) RETURNING id;";

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);

                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Insert Error: {ex.Message}");
                return 0;
            }
        }


        public bool ExecuteUpdate<T>(T entity, int id) where T : new()
        {
            try
            {
                var (tableName, columns) = TableMapper.GetTableSchema(typeof(T));
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var setClause = new List<string>();
                var parameters = new Dictionary<string, object>();
                int paramIndex = 0;

                foreach (var prop in properties)
                {
                    var idAttr = prop.GetCustomAttribute<IdAttribute>();
                    if (idAttr != null) continue;

                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                    var colName = colAttr?.Name ?? prop.Name.ToSnakeCase();
                    var value = prop.GetValue(entity);

                    var paramName = $"@p{paramIndex}";
                    setClause.Add($"{colName} = {paramName}");
                    parameters[paramName] = value ?? DBNull.Value;
                    paramIndex++;
                }

                var sql = $"UPDATE {tableName} SET {string.Join(", ", setClause)} WHERE id = @id;";
                parameters["@id"] = id;

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);

                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                return false;
            }
        }

        public bool ExecuteDelete<T>(int id) where T : new()
        {
            try
            {
                var (tableName, _) = TableMapper.GetTableSchema(typeof(T));
                var sql = $"DELETE FROM {tableName} WHERE id = @id;";

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Error: {ex.Message}");
                return false;
            }
        }
    }

    public static class NpgsqlDataReaderExtensions
    {
        public static bool HasColumn(this NpgsqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
