using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Application.Interfaces
{
    public interface IDBExecution
    {
        void ExecuteScript(string sqlScript);
    }
}
