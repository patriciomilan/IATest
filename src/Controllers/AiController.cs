using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Updating.Ai.TestDB.Models;
using Updating.Ai.TestDB.Services;

namespace Updating.Ai.TestDB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IRepositoryDB _repository;
        private readonly IChatGPTService _chatGPTService;
        public AiController(IRepositoryDB repository, IChatGPTService chatGPT)
        {
            _repository = repository;
            _chatGPTService = chatGPT;
        }

        [HttpPost("ConsultarDB")]
        public async Task<ActionResult<Respuesta?>> ConsultarDB(ConsultaDB prompt) 
        {
            Respuesta respuesta = new Respuesta();
            try
            {
                string sql = await _chatGPTService.ConsultarDB(prompt);
                respuesta.Lista = _repository.GetData(sql);
                respuesta.Exito = true;
                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                respuesta.Exito = false;
                respuesta.Mensaje = ex.Message;
                return BadRequest(respuesta);
            }
        }



        [HttpGet("Databases")]
        public async Task<ActionResult<List<string>>> GetDatabases()
        {
            var result = _repository.GetDatabases();
            return Ok(result);
        }

        [HttpGet("Databases/{database}/Tablas")]
        public async Task<ActionResult<List<string>>> GetTablas(string database)
        {
            var result = _repository.GetAllTables(database);
            return Ok(result);
        }


    }
}
