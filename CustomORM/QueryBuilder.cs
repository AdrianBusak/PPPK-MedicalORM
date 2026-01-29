using System;
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
            var entityId = typeof(T).GetProperty("Id")?.GetValue(entity);
            if (entityId == null) return;

            foreach (var navPropName in _includeNavigations)
            {
                var navProp = typeof(T).GetProperty(navPropName);
                if (navProp == null) continue;

                var propType = navProp.PropertyType;

                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propType.GetGenericArguments()[0];
                    var fkPropertyName = typeof(T).Name + "Id";
                    var fkProperty = elementType.GetProperty(fkPropertyName);

                    if (fkProperty == null) continue;

                    var allItems = GetAllDynamic(elementType);
                    var filtered = allItems
                        .Cast<object>()
                        .Where(item => fkProperty.GetValue(item)?.Equals(entityId) ?? false)
                        .ToList();

                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (System.Collections.IList)Activator.CreateInstance(listType);

                    foreach (var item in filtered)
                        list.Add(item);

                    navProp.SetValue(entity, list);
                }
            }
        }

        // Helper - dohvati sve dinamički
        private System.Collections.IEnumerable GetAllDynamic(Type elementType)
        {
            var method = typeof(CustomDbContext)
                .GetMethod("GetAll", BindingFlags.Public | BindingFlags.Instance)
                .MakeGenericMethod(elementType);

            return (System.Collections.IEnumerable)method.Invoke(_db, null);
        }
    }
}
