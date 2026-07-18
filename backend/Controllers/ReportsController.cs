using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Security;
using ExpenseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportingService _reporting;
    private readonly IUserContext _userContext;

    public ReportsController(ReportingService reporting, IUserContext userContext)
    {
        _reporting = reporting;
        _userContext = userContext;
    }

    /// <summary>Spend summary scoped to the caller's role (own / team / all).</summary>
    [HttpGet("summary")]
    public ActionResult<ReportSummaryResponse> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var actor = _userContext.RequireUser();
        return Ok(_reporting.Summarize(actor, from, to));
    }
}
