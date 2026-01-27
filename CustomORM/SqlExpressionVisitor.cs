using System;
using System.Linq.Expressions;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

namespace CustomORM
{
    public class SqlExpressionVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _sql = new();
        private readonly List<(string name, object value)> _parameters = new();
        private int _paramCounter = 0;

        public (string whereClause, Dictionary<string, object> parameters) Translate(Expression expression)
        {
            Visit(expression);

            var paramDict = new Dictionary<string, object>();
            foreach (var (name, value) in _parameters)
            {
                paramDict[name] = value ?? DBNull.Value;
            }

            return (_sql.ToString(), paramDict);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sql.Append("(");
            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    _sql.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    _sql.Append(" <> ");
                    break;
                case ExpressionType.GreaterThan:
                    _sql.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _sql.Append(" >= ");
                    break;
                case ExpressionType.LessThan:
                    _sql.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    _sql.Append(" <= ");
                    break;
                case ExpressionType.AndAlso:
                    _sql.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    _sql.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"Operator {node.NodeType} nije podržan");
            }

            Visit(node.Right);
            _sql.Append(")");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Ignoriraj List<> svojstva
            if (node.Member.DeclaringType != null &&
                node.Member.MemberType == MemberTypes.Property)
            {
                var prop = (PropertyInfo)node.Member;
                if (prop.PropertyType.IsGenericType &&
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    throw new NotSupportedException($"Navigacijska svojstva nisu podržana u Where klauzuli");
                }
            }

            // Mapira property name na column name
            var columnAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? node.Member.Name.ToSnakeCase();
            _sql.Append(columnName);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value == null)
            {
                _sql.Append("NULL");
            }
            else
            {
                var paramName = $"@p{_paramCounter++}";
                _sql.Append(paramName);

                // Za sve tipove, uključujući DateTime
                _parameters.Add((paramName, node.Value));
            }
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // String.Contains("text") → LIKE '%text%'
            if (node.Method.Name == "Contains" && node.Object?.Type == typeof(string))
            {
                Visit(node.Object);
                _sql.Append(" LIKE ");

                if (node.Arguments[0] is ConstantExpression ce)
                {
                    var paramName = $"@p{_paramCounter++}";
                    _sql.Append(paramName);
                    _parameters.Add((paramName, $"%{ce.Value}%"));
                }
                return node;
            }

            // String.StartsWith("text") → LIKE 'text%'
            if (node.Method.Name == "StartsWith" && node.Object?.Type == typeof(string))
            {
                Visit(node.Object);
                _sql.Append(" LIKE ");

                if (node.Arguments[0] is ConstantExpression ce)
                {
                    var paramName = $"@p{_paramCounter++}";
                    _sql.Append(paramName);
                    _parameters.Add((paramName, $"{ce.Value}%"));
                }
                return node;
            }

            // String.EndsWith("text") → LIKE '%text'
            if (node.Method.Name == "EndsWith" && node.Object?.Type == typeof(string))
            {
                Visit(node.Object);
                _sql.Append(" LIKE ");

                if (node.Arguments[0] is ConstantExpression ce)
                {
                    var paramName = $"@p{_paramCounter++}";
                    _sql.Append(paramName);
                    _parameters.Add((paramName, $"%{ce.Value}"));
                }
                return node;
            }

            throw new NotSupportedException($"Metoda {node.Method.Name} nije podržana");
        }
    }
}
