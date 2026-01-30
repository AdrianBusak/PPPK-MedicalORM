using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CustomORM.MigrationModels;
using Npgsql;

namespace CustomORM
{
    public class SchemaSnapshot
    {
        public Dictionary<string, TableSnapshot> Tables { get; set; } = new();
    }

    public class TableSnapshot
    {
        public Dictionary<string, ColumnSnapshot> Columns { get; set; } = new();
    }

    public class AutoMigrationEngine
    {
        private readonly string _connectionString;

        public AutoMigrationEngine(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task MigrateAsync(params Type[] entityTypes)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("   AUTO MIGRATION");
            Console.WriteLine("========================================\n");

            await EnsureMigrationsTableAsync();

            var dbSnap = await BuildSnapshotFromDatabaseAsync();
            var modelSnap = BuildSnapshotFromEntitiesAsync(entityTypes);
            var plan = Diff(dbSnap, modelSnap);

            if (plan.Up.Count == 0)
            {
                Console.WriteLine("No changes - database is in sync with models\n");
                return;
            }

            var validation = ValidateMigration(plan, dbSnap, modelSnap);
            if (!validation.IsValid)
            {
                Console.WriteLine("MIGRATION CANNOT BE EXECUTED!\n");
                foreach (var error in validation.Errors)
                {
                    Console.WriteLine($"  ERROR: {error}");
                }
                Console.WriteLine();
                return;
            }

            if (validation.Warnings.Count > 0)
            {
                Console.WriteLine("WARNINGS:\n");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  WARNING: {warning}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Found {plan.Up.Count} changes:\n");

            var name = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_auto";
            var upSql = string.Join("\n", plan.Up);
            var downSql = string.Join("\n", plan.Down);
            var snapshotJson = JsonSerializer.Serialize(modelSnap);

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                var upStatements = SplitSqlStatements(upSql);

                foreach (var sql in upStatements)
                {
                    if (string.IsNullOrWhiteSpace(sql)) continue;

                    using var cmd = new NpgsqlCommand(sql, conn, tx);
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"  OK {sql.Substring(0, Math.Min(50, sql.Length))}...");
                }

                foreach (var entityType in entityTypes)
                {
                    foreach (var fkSql in GenerateForeignKeysSql(entityType))
                    {
                        if (string.IsNullOrWhiteSpace(fkSql)) continue;

                        using var cmd = new NpgsqlCommand(fkSql, conn, tx);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                using var cmdM = new NpgsqlCommand(
                    @"INSERT INTO ""__orm_migrations"" (name, snapshot_json, up_sql, down_sql, applied_at) 
                      VALUES (@n, @s, @u, @d, now());", conn, tx);

                cmdM.Parameters.AddWithValue("@n", name);
                cmdM.Parameters.AddWithValue("@s", snapshotJson);
                cmdM.Parameters.AddWithValue("@u", upSql);
                cmdM.Parameters.AddWithValue("@d", downSql);

                await cmdM.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                Console.WriteLine($"\nMigration applied: {name}\n");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"\nError: {ex.Message}\n");
                throw;
            }
        }

        public async Task RollbackLastAsync()
        {
            await EnsureMigrationsTableAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT id, name, down_sql FROM ""__orm_migrations"" 
                  WHERE applied_at IS NOT NULL AND rolled_back_at IS NULL 
                  ORDER BY id DESC LIMIT 1;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                Console.WriteLine("No migrations to rollback.\n");
                return;
            }

            int id = reader.GetInt32(0);
            string name = reader.GetString(1);
            string downSql = reader.GetString(2);
            await reader.CloseAsync();

            Console.WriteLine($"\nRollback: {name}\n");

            using var tx = await conn.BeginTransactionAsync();

            try
            {
                var statements = SplitSqlStatements(downSql);

                Console.WriteLine($"Executing {statements.Count} SQL statements:\n");

                foreach (var sql in statements)
                {
                    using var cmdExec = new NpgsqlCommand(sql, conn, tx);
                    await cmdExec.ExecuteNonQueryAsync();
                    Console.WriteLine($"  OK {sql.Substring(0, Math.Min(50, sql.Length))}...");
                }

                using var cmdUpdate = new NpgsqlCommand(
                    @"UPDATE ""__orm_migrations"" SET rolled_back_at = now() WHERE id = @id;", conn, tx);
                cmdUpdate.Parameters.AddWithValue("@id", id);
                await cmdUpdate.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                Console.WriteLine($"\nRollback completed: {name}\n");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"Error: {ex.Message}\n");
                throw;
            }
        }

        public async Task MigrateForwardAsync()
        {
            await EnsureMigrationsTableAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT id, name, up_sql FROM ""__orm_migrations"" 
                  WHERE rolled_back_at IS NOT NULL 
                  ORDER BY id DESC LIMIT 1;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                Console.WriteLine("No rolled back migrations to apply.\n");
                return;
            }

            int id = reader.GetInt32(0);
            string name = reader.GetString(1);
            string upSql = reader.GetString(2);
            await reader.CloseAsync();

            Console.WriteLine($"\nForward migration: {name}\n");

            using var tx = await conn.BeginTransactionAsync();

            try
            {
                var statements = SplitSqlStatements(upSql);

                Console.WriteLine($"Executing {statements.Count} SQL statements:\n");

                foreach (var sql in statements)
                {
                    try
                    {
                        using var cmdExec = new NpgsqlCommand(sql, conn, tx);
                        await cmdExec.ExecuteNonQueryAsync();
                        Console.WriteLine($"  OK {sql.Substring(0, Math.Min(50, sql.Length))}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERROR: {ex.Message}");
                    }
                }

                using var cmdUpdate = new NpgsqlCommand(
                    @"UPDATE ""__orm_migrations"" SET rolled_back_at = NULL WHERE id = @id;", conn, tx);
                cmdUpdate.Parameters.AddWithValue("@id", id);
                await cmdUpdate.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                Console.WriteLine($"\nForward migration completed: {name}\n");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"Error: {ex.Message}\n");
                throw;
            }
        }

        public async Task ShowMigrationsAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT id, name, applied_at, rolled_back_at FROM ""__orm_migrations"" ORDER BY id;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            Console.WriteLine("\n========================================");
            Console.WriteLine("   MIGRATIONS");
            Console.WriteLine("========================================\n");

            if (!reader.HasRows)
            {
                Console.WriteLine("No migrations.\n");
                return;
            }

            int count = 0;
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                object appliedAt = reader.GetValue(2);
                object rolledBackAt = reader.GetValue(3);

                if (rolledBackAt != DBNull.Value)
                {
                    Console.WriteLine($"  {id}. {name}");
                    Console.WriteLine($"     Status: ROLLED BACK");
                    Console.WriteLine($"     Rollback: {((DateTime)rolledBackAt):yyyy-MM-dd HH:mm:ss}");
                }
                else if (appliedAt != DBNull.Value)
                {
                    Console.WriteLine($"  {id}. {name}");
                    Console.WriteLine($"     Status: APPLIED");
                    Console.WriteLine($"     Applied: {((DateTime)appliedAt):yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    Console.WriteLine($"  {id}. {name}");
                    Console.WriteLine($"     Status: PENDING");
                }
                count++;
            }

            Console.WriteLine($"\nTotal: {count} migrations\n");
        }

        private List<string> SplitSqlStatements(string sql)
        {
            var statements = new List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var line in sql.Split('\n'))
            {
                current.AppendLine(line);

                if (line.TrimEnd().EndsWith(";"))
                {
                    var statement = current.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        statements.Add(statement);
                    }
                    current.Clear();
                }
            }

            var remaining = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(remaining) && !remaining.EndsWith(";"))
            {
                remaining += ";";
                statements.Add(remaining);
            }

            return statements.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private MigrationValidation ValidateMigration(
            MigrationAction plan,
            SchemaSnapshot dbSnap,
            SchemaSnapshot modelSnap)
        {
            var validation = new MigrationValidation { IsValid = true };

            foreach (var (tableName, dbTable) in dbSnap.Tables)
            {
                if (!modelSnap.Tables.ContainsKey(tableName))
                {
                    validation.Warnings.Add(
                        $"Table '{tableName}' will be deleted with all data. " +
                        $"This is a critical operation that cannot be auto-reversed.");
                    continue;
                }

                var modelTable = modelSnap.Tables[tableName];

                foreach (var (colName, col) in dbTable.Columns)
                {
                    if (!modelTable.Columns.ContainsKey(colName))
                    {
                        validation.Warnings.Add(
                            $"Column '{tableName}.{colName}' will be deleted with all data. " +
                            $"If the table contains data, that data will be permanently lost.");
                    }
                }
            }

            return validation;
        }

        private async Task EnsureMigrationsTableAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var checkTableCmd = new NpgsqlCommand(@"
                SELECT EXISTS(
                    SELECT FROM information_schema.tables 
                    WHERE table_name = '__orm_migrations'
                );", conn);

            bool tableExists = (bool)await checkTableCmd.ExecuteScalarAsync();

            if (!tableExists)
            {
                using var cmd = new NpgsqlCommand(@"
                    CREATE TABLE ""__orm_migrations"" (
                        ""id"" SERIAL PRIMARY KEY,
                        ""name"" TEXT NOT NULL UNIQUE,
                        ""applied_at"" TIMESTAMP,
                        ""rolled_back_at"" TIMESTAMP,
                        ""snapshot_json"" TEXT NOT NULL,
                        ""up_sql"" TEXT NOT NULL,
                        ""down_sql"" TEXT NOT NULL
                    );", conn);

                await cmd.ExecuteNonQueryAsync();
                return;
            }

            using var checkColCmd = new NpgsqlCommand(@"
                SELECT EXISTS(
                    SELECT FROM information_schema.columns 
                    WHERE table_name = '__orm_migrations' 
                    AND column_name = 'rolled_back_at'
                );", conn);

            bool hasRolledBackCol = (bool)await checkColCmd.ExecuteScalarAsync();

            if (!hasRolledBackCol)
            {
                using var alterCmd = new NpgsqlCommand(
                    "ALTER TABLE \"__orm_migrations\" ADD COLUMN \"rolled_back_at\" TIMESTAMP;", conn);
                await alterCmd.ExecuteNonQueryAsync();
            }
        }

        private SchemaSnapshot BuildSnapshotFromEntitiesAsync(Type[] entityTypes)
        {
            var snap = new SchemaSnapshot();

            foreach (var entityType in entityTypes)
            {
                var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
                if (tableAttr == null) continue;

                var table = new TableSnapshot();

                foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                    var idAttr = prop.GetCustomAttribute<IdAttribute>();

                    if (idAttr != null) continue;
                    if (colAttr == null) continue;

                    var colName = colAttr.Name ?? prop.Name.ToSnakeCase();
                    var sqlType = SqlTypeMap.GetSqlType(prop.PropertyType);

                    table.Columns[colName] = new ColumnSnapshot
                    {
                        Type = sqlType,
                        IsNullable = true,
                        Unique = false,
                        DefaultSql = null
                    };
                }

                snap.Tables[tableAttr.Name] = table;
            }

            return snap;
        }

        private async Task<SchemaSnapshot> BuildSnapshotFromDatabaseAsync()
        {
            var snap = new SchemaSnapshot();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using (var cmd = new NpgsqlCommand(
                @"SELECT table_name FROM information_schema.tables 
                  WHERE table_schema='public' AND table_type='BASE TABLE' 
                  AND table_name NOT LIKE '__orm%';", conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    snap.Tables[reader.GetString(0)] = new TableSnapshot();
                }
            }

            using (var cmd = new NpgsqlCommand(
                @"SELECT table_name, column_name, data_type, is_nullable, column_default 
                  FROM information_schema.columns 
                  WHERE table_schema='public' AND table_name NOT LIKE '__orm%'
                  ORDER BY table_name, ordinal_position;", conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var tname = reader.GetString(0);
                    var cname = reader.GetString(1);
                    var dtype = reader.GetString(2);
                    var nullable = reader.GetString(3) == "YES";
                    var defVal = reader.IsDBNull(4) ? null : reader.GetString(4);

                    if (!snap.Tables.TryGetValue(tname, out var table))
                        continue;

                    table.Columns[cname] = new ColumnSnapshot
                    {
                        Type = dtype,
                        IsNullable = nullable,
                        Unique = false,
                        DefaultSql = defVal
                    };
                }
            }

            return snap;
        }

        private MigrationAction Diff(SchemaSnapshot db, SchemaSnapshot model)
        {
            var plan = new MigrationAction();

            foreach (var (tname, mtable) in model.Tables)
            {
                if (!db.Tables.ContainsKey(tname))
                {
                    plan.Up.Add(GenerateCreateTableSql(tname, mtable));
                    plan.Down.Insert(0, $"DROP TABLE IF EXISTS \"{tname}\" CASCADE;");
                    continue;
                }

                var dbtable = db.Tables[tname];
                foreach (var (cname, col) in mtable.Columns)
                {
                    if (!dbtable.Columns.ContainsKey(cname))
                    {
                        plan.Up.Add(GenerateAddColumnSql(tname, cname, col));
                        plan.Down.Insert(0, $"ALTER TABLE \"{tname}\" DROP COLUMN IF EXISTS \"{cname}\" CASCADE;");
                    }
                }
            }

            return plan;
        }

        private string GenerateCreateTableSql(string tableName, TableSnapshot table)
        {
            var cols = new List<string>();
            cols.Add("  \"id\" integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY");

            foreach (var (colName, col) in table.Columns)
            {
                var nullPart = col.IsNullable ? "NULL" : "NOT NULL";
                var uniquePart = col.Unique ? "UNIQUE" : "";
                var defaultPart = col.DefaultSql != null ? $"DEFAULT {col.DefaultSql}" : "";
                cols.Add($"  \"{colName}\" {col.Type} {nullPart} {uniquePart} {defaultPart}".Trim());
            }

            return $"CREATE TABLE \"{tableName}\" (\n{string.Join(",\n", cols)}\n);";
        }

        private string GenerateAddColumnSql(string tableName, string colName, ColumnSnapshot col)
        {
            var nullPart = col.IsNullable ? "NULL" : "NOT NULL";
            var uniquePart = col.Unique ? "UNIQUE" : "";
            var defaultPart = col.DefaultSql != null ? $"DEFAULT {col.DefaultSql}" : "";
            return $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{colName}\" {col.Type} {nullPart} {uniquePart} {defaultPart};".Trim();
        }

        private IEnumerable<string> GenerateForeignKeysSql(Type entityType)
        {
            var sqlList = new List<string>();
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) return sqlList;

            var tableName = tableAttr.Name;

            foreach (var prop in entityType.GetProperties())
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var fkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
                if (colAttr == null || fkAttr == null) continue;

                var colName = colAttr.Name ?? prop.Name.ToSnakeCase();
                var cname = $"fk_{tableName}_{colName}_{fkAttr.ReferencedTable}";

                sqlList.Add($"ALTER TABLE \"{tableName}\" DROP CONSTRAINT IF EXISTS \"{cname}\";");
                sqlList.Add(
                    $"ALTER TABLE \"{tableName}\" " +
                    $"ADD CONSTRAINT \"{cname}\" " +
                    $"FOREIGN KEY (\"{colName}\") REFERENCES \"{fkAttr.ReferencedTable}\"(\"{fkAttr.ReferencedColumn}\");"
                );
            }

            return sqlList.Where(s => !string.IsNullOrWhiteSpace(s));
        }
    }

    public static class SqlTypeMap
    {
        public static string GetSqlType(Type clrType)
        {
            var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            return underlyingType.Name switch
            {
                "Int32" => "integer",
                "Int64" => "bigint",
                "String" => "varchar(255)",
                "DateTime" => "timestamp",
                "Boolean" => "boolean",
                "Decimal" => "numeric(18,2)",
                "Double" => "double precision",
                "Float" => "real",
                _ => "text"
            };
        }
    }
}