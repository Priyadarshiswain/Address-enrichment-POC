using AddressEnrichment.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressEnrichment.Api.Controllers;

[ApiController]
[Route("api/address-validation")]
public class AddressValidationController(IGoogleApiService googleApiService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ValidationResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post([FromBody] AddressValidationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Address) ||
            string.IsNullOrWhiteSpace(request.City) ||
            string.IsNullOrWhiteSpace(request.Iso2))
        {
            return BadRequest(new ErrorResponse("Address, city, and iso2 are required."));
        }

        try
        {
            return Ok(await googleApiService.ValidateAddressAsync(request, cancellationToken));
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
