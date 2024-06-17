using System.Data;

namespace Updating.Ai.TestDB.Services
{
    public interface IRepositoryDB
    {
        string GetModel(string database);

        string StringConection { get; set; }

        DataTable GetDatatable(string sql);

        List<object> GetData(string sql);

        public List<string> GetDatabases();
        public List<string> GetAllTables(string database);

    }
}
