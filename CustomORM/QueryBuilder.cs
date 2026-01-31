using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CustomORM
{
    public class QueryBuilder<T> where T : new()
    {
        private readonly CustomDbContext _db;
        private readonly List<string> _includeNavigations = new();

        public QueryBuilder(CustomDbContext db)
        {
            _db = db;
        }

        public QueryBuilder<T> Include(Expression<Func<T, object>> navigationProperty)
        {
            var memberExpr = navigationProperty.Body as MemberExpression
                ?? (navigationProperty.Body as UnaryExpression)?.Operand as MemberExpression;

            if (memberExpr != null)
            {
                _includeNavigations.Add(memberExpr.Member.Name);
            }

            return this;
        }

        public T FirstOrDefault(Expression<Func<T, bool>> predicate = null)
        {
            var results = predicate != null ? _db.Where(predicate) : _db.GetAll<T>();

            if (results.Count == 0)
                return default;

            var entity = results[0];
            PopulateIncludes(entity);
            return entity;
        }

        public List<T> ToList(Expression<Func<T, bool>> predicate = null)
        {
            var results = predicate != null ? _db.Where(predicate) : _db.GetAll<T>();

            foreach (var entity in results)
                PopulateIncludes(entity);

            return results;
        }

        private void PopulateIncludes(T entity)
        {
            var entityType = typeof(T);
            var entityId = entityType.GetProperty("Id")?.GetValue(entity);
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
            if (entityId == null || tableAttr == null) return;


            foreach (var navPropName in _includeNavigations)
            {

                var navProp = entityType.GetProperty(navPropName);
                if (navProp == null) continue;

                var propType = navProp.PropertyType;

                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    PopulateCollection(entity, navProp, (int)entityId, tableAttr.Name);
                }
                else if (!propType.IsPrimitive && propType != typeof(string))
                {
                    PopulateSingle(entity, navProp);
                }
                else
                {
                    Console.WriteLine("Skip (primitive)");
                }
            }
        }

        private void PopulateCollection(T entity, PropertyInfo navProp, int parentId, string parentTable)
        {
            var elementType = navProp.PropertyType.GetGenericArguments()[0];
            var fkProp = FindForeignKeyProperty(elementType, parentTable);
            if (fkProp == null)
            {
                return;
            }


            var allItems = GetAllDynamic(elementType);
            var filtered = allItems.Cast<object>()
                .Where(item => fkProp.GetValue(item)?.Equals(parentId) ?? false)
                .ToList();


            var list = (IList)Activator.CreateInstance(navProp.PropertyType);
            foreach (var item in filtered) list.Add(item);
            navProp.SetValue(entity, list);
        }

        private void PopulateSingle(T entity, PropertyInfo navProp)
        {
            var entityType = entity.GetType();
            var fkName = navProp.Name + "Id";
            var fkProp = entityType.GetProperty(fkName);
            if (fkProp == null)
            {
                return;
            }

            var fkValue = fkProp.GetValue(entity);

            if (fkValue == null || (fkValue is int i && i == 0)) return;

            var relatedType = navProp.PropertyType;
            var allRelated = GetAllDynamic(relatedType);

            var related = allRelated.Cast<object>()
                .FirstOrDefault(item =>
                {
                    var idProp = relatedType.GetProperty("Id");
                    return Equals(idProp?.GetValue(item), fkValue);
                });

            if (related != null)
            {
                navProp.SetValue(entity, related);
            }
            else
            {
                Console.WriteLine("No matching entity!");
            }
        }

        private PropertyInfo FindForeignKeyProperty(Type childType, string parentTable)
        {
            foreach (var prop in childType.GetProperties())
            {
                var fkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
                if (fkAttr != null && fkAttr.ReferencedTable == parentTable)
                    return prop;
            }

            var conventionNames = new[] { parentTable + "_id", parentTable.ToSnakeCase() + "_id", "patient_id" };
            foreach (var name in conventionNames)
            {
                var prop = childType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) return prop;
            }

            return null;
        }


        // Helper - dohvati sve dinamički
        private IEnumerable GetAllDynamic(Type elementType)
        {
            var method = typeof(CustomDbContext)
                .GetMethod("GetAll", BindingFlags.Public | BindingFlags.Instance)
                .MakeGenericMethod(elementType);

            return (IEnumerable)method.Invoke(_db, null);
        }
    }
}
