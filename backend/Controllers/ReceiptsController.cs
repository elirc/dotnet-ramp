using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Security;
using ExpenseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers;

[ApiController]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly ReceiptService _receipts;
    private readonly IUserContext _userContext;

    public ReceiptsController(ReceiptService receipts, IUserContext userContext)
    {
        _receipts = receipts;
        _userContext = userContext;
    }

    /// <summary>Upload (or replace) the receipt for an expense.</summary>
    [HttpPost("api/expenses/{expenseId:guid}/receipt")]
    [RequestSizeLimit(6 * 1024 * 1024)] // a little above the configured per-file cap
    public async Task<ActionResult<ReceiptResponse>> Upload(Guid expenseId, IFormFile file, CancellationToken ct)
    {
        if (file is null)
            return BadRequest(new { message = "No file was provided." });

        var actor = _userContext.RequireUser();
        var receipt = await _receipts.UploadAsync(actor, expenseId, file, ct);
        return Ok(receipt.ToResponse());
    }

    /// <summary>Download the stored receipt file (authorized via expense visibility).</summary>
    [HttpGet("api/receipts/{receiptId:guid}/file")]
    public IActionResult Download(Guid receiptId)
    {
        var actor = _userContext.RequireUser();
        var receipt = _receipts.GetForActor(actor, receiptId);
        var (stream, contentType) = _receipts.OpenFile(receipt);
        return File(stream, contentType, receipt.OriginalFileName);
    }
}
