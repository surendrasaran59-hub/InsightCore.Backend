using InsightCore.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Infrastructure.Implementations
{
    public class DBExecution: IDBExecution
    {
        private readonly IConfiguration _config;

        public DBExecution(IConfiguration config)
        {
            _config = config;
        }

        private string ConnectionString
        {
            get
            {
                return _config.GetConnectionString("DefaultConnection");
            }
        }

        public void ExecuteScript(string sqlScript)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                using (SqlCommand mergeCmd = new SqlCommand(sqlScript, conn))
                {
                    mergeCmd.CommandTimeout = 300;
                    mergeCmd.ExecuteNonQuery();
                }

                conn.Close();
            }
        }

        public void ExecuteProcWithParameter(int clientId, int userId, string fileName, string fileStatus)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.usp_InsertUpdateClientDataModel", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Add parameters
                    cmd.Parameters.Add(new SqlParameter("@ClientID", SqlDbType.Int) { Value = clientId });
                    cmd.Parameters.Add(new SqlParameter("@FileName", SqlDbType.VarChar) { Value = fileName });
                    cmd.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.Int) { Value = userId });
                    cmd.Parameters.Add(new SqlParameter("@UploadStatus", SqlDbType.VarChar) { Value = fileStatus });

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
