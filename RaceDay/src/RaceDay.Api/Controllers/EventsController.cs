using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs.Categories;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Browsing events (public) and managing them (Organiser, own events only).</summary>
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EventsController(RaceDayDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>Lists all events, optionally filtered by status, date or location.</summary>
    /// <response code="200">Matching events.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] string? status, [FromQuery] DateOnly? date, [FromQuery] string? location)
    {
        var query = _context.Events.Include(e => e.Organiser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }
        if (date.HasValue)
        {
            query = query.Where(e => e.EventDate == date.Value);
        }
        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(e => e.Location != null && e.Location.Contains(location));
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(ToDto));
    }

    /// <summary>Returns full details for a single event, including its categories.</summary>
    /// <response code="200">The event, with nested categories.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _context.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null)
        {
            return NotFound();
        }

        var dto = ToDetailDto(ev);
        return Ok(dto);
    }

    /// <summary>Creates a new event owned by the logged-in organiser.</summary>
    /// <response code="201">Created event.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost]
    [Authorize(Roles = UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvent(CreateEventDto dto)
    {
        if (!Models.EventType.IsValid(dto.Type))
        {
            return BadRequest(new { message = $"Type must be one of: {string.Join(", ", Models.EventType.All)}." });
        }

        var ev = new Event
        {
            OrganiserId = _currentUser.UserId!.Value,
            Name = dto.Name,
            EventDate = dto.EventDate,
            Location = dto.Location,
            Description = dto.Description,
            Distance = dto.Distance,
            Type = dto.Type,
            Status = EventStatus.Planned
        };

        _context.Events.Add(ev);
        await _context.SaveChangesAsync();
        await _context.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ToDto(ev));
    }

    /// <summary>Updates an event. Only the owning organiser may edit it.</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="403">The caller does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, UpdateEventDto dto)
    {
        var ev = await _context.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
        {
            return NotFound();
        }
        if (ev.OrganiserId != _currentUser.UserId)
        {
            return Forbid();
        }
        if (!Models.EventType.IsValid(dto.Type))
        {
            return BadRequest(new { message = $"Type must be one of: {string.Join(", ", Models.EventType.All)}." });
        }
        if (!EventStatus.IsValid(dto.Status))
        {
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", EventStatus.All)}." });
        }

        ev.Name = dto.Name;
        ev.EventDate = dto.EventDate;
        ev.Location = dto.Location;
        ev.Description = dto.Description;
        ev.Distance = dto.Distance;
        ev.Type = dto.Type;
        ev.Status = dto.Status;

        await _context.SaveChangesAsync();

        return Ok(ToDto(ev));
    }

    /// <summary>Cancels/removes an event. Only the owning organiser may delete it.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">The caller does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev is null)
        {
            return NotFound();
        }
        if (ev.OrganiserId != _currentUser.UserId)
        {
            return Forbid();
        }

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static EventDto ToDto(Event ev) => new()
    {
        EventId = ev.EventId,
        OrganiserId = ev.OrganiserId,
        OrganiserName = ev.Organiser?.FullName ?? string.Empty,
        Name = ev.Name,
        EventDate = ev.EventDate,
        Location = ev.Location,
        Description = ev.Description,
        Distance = ev.Distance,
        Type = ev.Type,
        Status = ev.Status,
        CreatedAt = ev.CreatedAt
    };

    private static EventDetailDto ToDetailDto(Event ev)
    {
        var dto = new EventDetailDto
        {
            EventId = ev.EventId,
            OrganiserId = ev.OrganiserId,
            OrganiserName = ev.Organiser?.FullName ?? string.Empty,
            Name = ev.Name,
            EventDate = ev.EventDate,
            Location = ev.Location,
            Description = ev.Description,
            Distance = ev.Distance,
            Type = ev.Type,
            Status = ev.Status,
            CreatedAt = ev.CreatedAt,
            Categories = ev.Categories.Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                EventId = c.EventId,
                Name = c.Name,
                DistanceKm = c.DistanceKm,
                MinAge = c.MinAge,
                MaxAge = c.MaxAge,
                EntryFee = c.EntryFee,
                MaxParticipants = c.MaxParticipants
            }).ToList()
        };
        return dto;
    }
}
