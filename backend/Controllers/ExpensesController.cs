using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Security;
using ExpenseManagement.Api.Services;
using ExpenseManagement.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize] // every action requires a valid token; role checks live in the service
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenses;
    private readonly IUserContext _userContext;
    private readonly DataStore _store;

    public ExpensesController(ExpenseService expenses, IUserContext userContext, DataStore store)
    {
        _expenses = expenses;
        _userContext = userContext;
        _store = store;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ExpenseResponse>> List(
        [FromQuery] ExpenseStatus? status,
        [FromQuery] ExpenseCategory? category,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var actor = _userContext.RequireUser();
        var results = _expenses.List(actor, new ExpenseFilter(status, category, from, to));
        return Ok(results.Select(e => e.ToResponse(_store, actor)));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ExpenseResponse> Get(Guid id)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.GetForActor(actor, id);
        return Ok(expense.ToResponse(_store, actor));
    }

    [HttpPost]
    public ActionResult<ExpenseResponse> Create(CreateExpenseRequest req)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.Create(actor, req);
        return CreatedAtAction(nameof(Get), new { id = expense.Id }, expense.ToResponse(_store, actor));
    }

    [HttpPut("{id:guid}")]
    public ActionResult<ExpenseResponse> Update(Guid id, UpdateExpenseRequest req)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.Update(actor, id, req);
        return Ok(expense.ToResponse(_store, actor));
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var actor = _userContext.RequireUser();
        _expenses.Delete(actor, id);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public ActionResult<ExpenseResponse> Submit(Guid id)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.Submit(actor, id);
        return Ok(expense.ToResponse(_store, actor));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Manager")]
    public ActionResult<ExpenseResponse> Approve(Guid id)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.Approve(actor, id);
        return Ok(expense.ToResponse(_store, actor));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager")]
    public ActionResult<ExpenseResponse> Reject(Guid id, RejectRequest req)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.Reject(actor, id, req.Reason);
        return Ok(expense.ToResponse(_store, actor));
    }

    [HttpPost("{id:guid}/reimburse")]
    [Authorize(Roles = "Finance")]
    public ActionResult<ExpenseResponse> Reimburse(Guid id)
    {
        var actor = _userContext.RequireUser();
        var expense = _expenses.StartReimbursement(actor, id);
        return Ok(expense.ToResponse(_store, actor));
    }
}
