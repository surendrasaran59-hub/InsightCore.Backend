using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Shared
{
    public class DataModelExcelRow
    {
        public string SN { get; set; }
        public string EntityName { get; set; }
        public string EntityDisplayName { get; set; }
        public string Module { get; set; }
        public string SubjectArea { get; set; }
        public string AttributeName { get; set; }
        public string AttributeDisplayName { get; set; }
        public string DataType { get; set; }
        public int? Length { get; set; }
        public bool IsNullable { get; set; }
        public string PKType { get; set; }
        public string Definition { get; set; }
    }
}
