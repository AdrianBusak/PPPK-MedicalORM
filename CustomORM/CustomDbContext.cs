using System.Linq.Expressions;
using System.Reflection;

namespace CustomORM
{
    public class CustomDbContext
    {
        private readonly string _connectionString;
        private readonly QueryExecutor _executor;

        public CustomDbContext(string connectionString)
        {
            _connectionString = connectionString;
            _executor = new QueryExecutor(connectionString);
        }

        public async Task InitializeDatabaseAsync(params Type[] entityTypes)
        {
            foreach (var entityType in entityTypes)
            {
                var createTableSql = CreateTableGenerator.GenerateCreateTable(entityType);
                await _executor.ExecuteAsync(createTableSql);
                Console.WriteLine($"Tablica {entityType.Name} kreirana");
            }

            var foreignKeys = new List<(string table, string column, string refTable, string refColumn)>();

            foreach (var entityType in entityTypes)
            {
                var tableName = TableMapper.GetTableSchema(entityType).TableName;

                var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    var fkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
                    if (fkAttr != null)
                    {
                        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                        var colName = colAttr?.Name ?? prop.Name.ToSnakeCase();
                        foreignKeys.Add((tableName, colName, fkAttr.ReferencedTable, fkAttr.ReferencedColumn));
                    }
                }
            }

            // 3. Kreiraj FK-ove
            foreach (var (table, column, refTable, refColumn) in foreignKeys)
            {
                try
                {
                    var sql = CreateTableGenerator.GenerateAddForeignKey(table, column, refTable, refColumn);
                    await _executor.ExecuteAsync(sql);
                    Console.WriteLine($"FK {table}.{column} → {refTable}.{refColumn}");
                }
                catch
                {
                    Console.WriteLine($"FK već postoji: {table}.{column}");
                }
            }

            Console.WriteLine("Svi FK-ovi konfigurirani");
        }


        public int Create<T>(T entity) where T : new()
        {
            return _executor.ExecuteInsert(entity);
        }

        public T GetById<T>(int id) where T : new()
        {
            try
            {
                var (tableName, _) = TableMapper.GetTableSchema(typeof(T));
                var sql = $"SELECT * FROM {tableName} WHERE id = @id;";
                var result = _executor.ExecuteQuery<T>(sql, new Dictionary<string, object> { { "@id", id } });

                if (result.Count > 0)
                    return result[0];
                else
                    return default;
            }
            catch
            {
                return default;
            }
        }

        public List<T> GetAll<T>() where T : new()
        {
            var (tableName, _) = TableMapper.GetTableSchema(typeof(T));
            var sql = $"SELECT * FROM {tableName};";
            return _executor.ExecuteQuery<T>(sql);
        }

        public bool Update<T>(T entity, int id) where T : new()
        {
            return _executor.ExecuteUpdate(entity, id);
        }

        public bool Delete<T>(int id) where T : new()
        {
            return _executor.ExecuteDelete<T>(id);
        }


        public List<T> Where<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            try
            {
                var visitor = new SqlExpressionVisitor();
                var (whereClause, parameters) = visitor.Translate(predicate.Body);

                var (tableName, _) = TableMapper.GetTableSchema(typeof(T));
                var sql = $"SELECT * FROM {tableName} WHERE {whereClause};";

                return _executor.ExecuteQuery<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Where Error: {ex.Message}");
                return new List<T>();
            }
        }
        public QueryBuilder<T> Query<T>() where T : new()
        {
            return new QueryBuilder<T>(this);
        }
    }
}