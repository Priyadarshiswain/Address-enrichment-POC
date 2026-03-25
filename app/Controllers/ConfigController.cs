using AddressEnrichment.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressEnrichment.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController(IGoogleApiService googleApiService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClientConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<ClientConfigResponse> Get()
    {
        try
        {
            return Ok(googleApiService.GetClientConfig());
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: 500);
        }
    }
}
