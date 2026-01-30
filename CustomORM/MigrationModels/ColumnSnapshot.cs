using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomORM.MigrationModels
{
    public class ColumnSnapshot
    {
        public string Type { get; set; }
        public bool IsNullable { get; set; }
        public bool Unique { get; set; }
        public string DefaultSql { get; set; }
    }
}
