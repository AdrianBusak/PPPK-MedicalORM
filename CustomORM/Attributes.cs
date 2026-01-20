using System;

namespace CustomORM
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        public string Name { get; set; } = "";
    }  

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; set; } = "";
        public bool NotNull { get; set; } = false;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class IdAttribute : Attribute
    {
        public bool AutoIncrement { get; set; } = false;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute
    {
        public string ReferencedTable { get; }
        public string ReferencedColumn { get; }

        public ForeignKeyAttribute(string referencedTable, string referencedColumn = "id")
        {
            ReferencedTable = referencedTable;
            ReferencedColumn = referencedColumn;
        }
    }
}
