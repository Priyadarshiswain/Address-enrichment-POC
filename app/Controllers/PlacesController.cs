using AddressEnrichment.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressEnrichment.Api.Controllers;

[ApiController]
[Route("api/places")]
public class PlacesController(IGoogleApiService googleApiService) : ControllerBase
{
    [HttpPost("nearby-search")]
    [ProducesResponseType<List<PlaceResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> NearbySearch([FromBody] PlacesNearbySearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest(new ErrorResponse("type is required."));
        }

        try
        {
            return Ok(await googleApiService.NearbySearchAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: 500);
        }
        catch (UpstreamApiException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }

    [HttpPost("text-search")]
    [ProducesResponseType<List<PlaceResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TextSearch([FromBody] PlacesTextSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await googleApiService.TextSearchAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: 500);
        }
        catch (UpstreamApiException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }
}
