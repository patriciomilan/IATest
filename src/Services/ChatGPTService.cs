using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using System.IO;
using System.Data.SqlTypes;
using Microsoft.Extensions.Configuration;
using Updating.Ai.TestDB.Models;

namespace Updating.Ai.TestDB.Services
{
    public class ChatGPTService : IChatGPTService
    {
        private readonly IRepositoryDB _repository;
        private string apikey = ""; 
        private string initText = "Dado el siguiente modelo de base de datos, en SQL Server: \n\n";
        private string introQuery = "Me puedes ayudar con un query en SQL...\n";

        public string Database { get; set; }
        public ChatGPTService(IRepositoryDB repository, IConfiguration configuration)
        {
            _repository = repository;
            apikey = configuration["Configuracion:openAIapiKey"];
        }
        public async Task<string> ConsultarDB(ConsultaDB userPrompt)
        {
            string sql = "";
            if (string.IsNullOrEmpty(userPrompt.Database))
            {
                throw new ArgumentNullException("Debe enviar el nombre de la Base de datos");
            }
            if (string.IsNullOrEmpty(userPrompt.Consulta))
            {
                throw new ArgumentNullException("Debe enviar una consulta descrita en lenguaje natural");
            }

            string modelo = await GetModel(userPrompt.Database);
            string msgFinal = initText + modelo + "\n\n" + introQuery + userPrompt.Consulta;

            var openAiService = new OpenAIService(
                new OpenAiOptions
                {
                    ApiKey = apikey
                });

            var completionResult = await openAiService.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    ChatMessage.FromUser(msgFinal)
                },
                Model = OpenAI.GPT3.ObjectModels.Models.ChatGpt3_5Turbo
            });

            if (completionResult.Successful)
            {
                var result = completionResult.Choices.First().Message.Content;
                if (!CheckSql(result))
                {
                    //No se pudo obtener un String SQL
                    throw new Exception(result);
                }
                
                sql = ObtenerSql(result);

                //Validar que no se este intentando hacer algo malicioso
                if (!IsSecure(sql))
                {
                    throw new Exception("No se permiten modificaciones sobre la base de datos. Sólo se permiten consultas");
                }

            }
            else
            {
                if (completionResult.Error == null)
                {
                    throw new Exception("Unknown Error");
                }
                else
                {
                    throw new Exception($"{completionResult.Error.Code}: {completionResult.Error.Message}");
                }
            }

            return sql;
        }

        private string ObtenerSql(string result)
        {
            string init = "```sql";
            string fin = "```";
            string sql = result.Substring(result.IndexOf(init) + init.Length);
            int posicion = sql.IndexOf(fin);
            sql = sql.Substring(0, posicion);
            return sql;
        }
        private bool CheckSql(string result)
        {
            string init = "```sql";
            return (result.IndexOf(init) > 0);
        }

        private bool IsSecure(string result)
        {
            string palabras = "drop ,delete ,update ,insert ,alter ,create ";
            string query = result.ToLower();
            foreach (var palabra in palabras.Split(','))
            {
                if (query.IndexOf(palabra) > 0) return false;
            }
            return true;
        }
        public async Task<string> GetModel(string database)
        {
            await Task.Delay(0);
            return _repository.GetModel(database);
        }

    }
}
