using System.Security.Claims;
using API.Data;
using API.Entities;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/sourcing")]
[Authorize]
public class SourcingController(ISourcingService _svc, StoreContext _db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    // ── Upload + auto-save ────────────────────────────────────────────────

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        try
        {
            var result = await _svc.ParseSpreadsheetAsync(file);

            // Persist for future sessions
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            _db.SourcingDocuments.Add(new SourcingDocument
            {
                UserId       = UserId,
                FileName     = file.FileName,
                FileContent  = ms.ToArray(),
                UploadedAt   = DateTime.UtcNow,
                ProductCount = result.Parsed,
            });
            await _db.SaveChangesAsync();

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sourcing] Parse error: {ex}");
            return StatusCode(500, new { error = "Failed to parse file. Check format and try again." });
        }
    }

    // ── List saved documents ──────────────────────────────────────────────

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var docs = await _db.SourcingDocuments
            .Where(d => d.UserId == UserId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.UploadedAt,
                d.ProductCount,
            })
            .ToListAsync();

        return Ok(docs);
    }

    // ── Load + re-parse a saved document ─────────────────────────────────

    [HttpGet("documents/{id:int}")]
    public async Task<IActionResult> LoadDocument(int id)
    {
        var doc = await _db.SourcingDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId);

        if (doc is null) return NotFound();

        try
        {
            var ms       = new MemoryStream(doc.FileContent);
            var formFile = new FormFile(ms, 0, doc.FileContent.Length, "file", doc.FileName)
            {
                Headers     = new HeaderDictionary(),
                ContentType = "application/octet-stream",
            };
            var result = await _svc.ParseSpreadsheetAsync(formFile);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sourcing] Load error: {ex}");
            return StatusCode(500, new { error = "Failed to load document." });
        }
    }

    // ── Delete a saved document ───────────────────────────────────────────

    [HttpDelete("documents/{id:int}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var doc = await _db.SourcingDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId);

        if (doc is null) return NotFound();

        _db.SourcingDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}