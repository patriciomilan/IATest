using Microsoft.AspNetCore.SignalR;
using Updating.Ai.TestDB.Models;

namespace Updating.Ai.TestDB.Services
{
    public interface IChatGPTService
    {
        Task<string> GetModel(string database);
        Task<string> ConsultarDB(ConsultaDB userPrompt);
    }
}
