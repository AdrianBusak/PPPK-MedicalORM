using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomORM.MigrationModels
{
    public class MigrationAction
    {
        public List<string> Up { get; set; } = new();
        public List<string> Down { get; set; } = new();
    }
}
