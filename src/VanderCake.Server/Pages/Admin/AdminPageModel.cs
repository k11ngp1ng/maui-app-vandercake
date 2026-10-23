using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
namespace VanderCake.Server.Pages.Admin;
public abstract class AdminPageModel : PageModel
{
    protected async Task<bool> AttemptAsync(Func<Task> action)
    {
        if (!ModelState.IsValid) return false;
        try { await action(); TempData["Success"] = "Changes saved successfully."; return true; }
        catch (Exception e) when (e is DomainRuleException or ApplicationFailure)
        { ModelState.AddModelError("", e.Message); return false; }
        catch (DbUpdateConcurrencyException)
        { ModelState.AddModelError("", "This record changed. Reload the page before editing again."); return false; }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        { ModelState.AddModelError("", "The record could not be saved. Check for a duplicate code, name or email, and reload before retrying."); return false; }
        catch (Exception e) when (e is SqliteException { SqliteErrorCode: 5 or 6 } || e.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
        { ModelState.AddModelError("", "The database is busy. Reload and review the latest values before retrying."); return false; }
    }
}
