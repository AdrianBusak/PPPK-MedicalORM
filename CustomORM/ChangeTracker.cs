using CustomORM;
using System.Reflection;

public class ChangeTracker
{
    private readonly Dictionary<object, Dictionary<string, object>> _trackedEntities = new();
    private readonly QueryExecutor _executor;

    public ChangeTracker(QueryExecutor executor)
    {
        _executor = executor;
    }

    public void Track<T>(T entity) where T : new()
    {
        if (entity == null || _trackedEntities.ContainsKey(entity))
            return;

        var originalValues = new Dictionary<string, object>();

        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                continue;

            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                continue;

            try
            {
                originalValues[prop.Name] = prop.GetValue(entity);
            }
            catch { }
        }

        _trackedEntities[entity] = originalValues;
    }

    public int SaveChanges()
    {
        int affectedRows = 0;

        foreach (var entry in _trackedEntities)
        {
            var entity = entry.Key;
            var originalValues = entry.Value;
            var type = entity.GetType();

            var idProp = type.GetProperty("Id");
            if (idProp == null) continue;

            var idValue = idProp.GetValue(entity);
            if (idValue == null) continue;

            var changes = new List<string>();
            var parameters = new Dictionary<string, object>();
            int paramCounter = 0;

            foreach (var prop in type.GetProperties())
            {
                if (prop.Name == "Id") continue;
                if (prop.PropertyType.IsGenericType &&
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    continue;
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                    continue;

                if (!originalValues.ContainsKey(prop.Name))
                    continue;

                var currentValue = prop.GetValue(entity);
                var originalValue = originalValues[prop.Name];

                if (!Equals(currentValue, originalValue))
                {
                    var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
                    var columnName = columnAttr?.Name ?? prop.Name.ToSnakeCase();

                    var paramName = $"@p{paramCounter++}";
                    changes.Add($"{columnName} = {paramName}");
                    parameters[paramName] = currentValue ?? DBNull.Value;
                }
            }

            if (changes.Count == 0)
                continue;

            var (tableName, _) = TableMapper.GetTableSchema(type);
            parameters["@id"] = idValue;

            var sql = $"UPDATE {tableName} SET {string.Join(", ", changes)} WHERE id = @id;";

            try
            {
                _executor.ExecuteNonQuery(sql, parameters);
                affectedRows++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveChanges Error: {ex.Message}");
            }
        }

        _trackedEntities.Clear();
        return affectedRows;
    }

    public void Clear()
    {
        _trackedEntities.Clear();
    }
}
