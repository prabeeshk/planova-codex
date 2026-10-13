using Microsoft.EntityFrameworkCore;
using Planova.Identity.Infrastructure;
using Planova.Projects.Application;

namespace Planova.Api.Projects;

public sealed class ProjectIdentityReferenceReader(IdentityDbContext db) : IProjectIdentityReader
{
    public Task<bool> IsActiveUserAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.OrganisationId == organisationId && x.Id == userId && x.Status == "active", cancellationToken);
    public Task<bool> IsOrganisationUnitAsync(Guid organisationId, Guid unitId, CancellationToken cancellationToken) =>
        db.OrganisationUnits.AnyAsync(x => x.OrganisationId == organisationId && x.Id == unitId && x.Status == "active", cancellationToken);
}
