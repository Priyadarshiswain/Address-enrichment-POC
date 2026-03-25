using AddressEnrichment.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressEnrichment.Api.Controllers;

[ApiController]
[Route("api/postal-boundary-target")]
public class PostalBoundaryController(IGoogleApiService googleApiService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PostalBoundaryTargetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<PostalBoundaryLookupFailureResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromQuery] string postalCode, [FromQuery] string iso2, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(iso2))
        {
            return BadRequest(new ErrorResponse("Both postalCode and iso2 are required."));
        }

        try
        {
            var result = await googleApiService.GetPostalBoundaryTargetAsync(postalCode, iso2, cancellationToken);
            return result.Target is not null
                ? Ok(result.Target)
                : NotFound(result.Failure);
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
