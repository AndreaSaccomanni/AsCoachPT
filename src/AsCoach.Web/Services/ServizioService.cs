using AsCoach.Web.Data;
using AsCoach.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoach.Web.Services;

public class ServizioService(AsCoachDbContext db)
{
    public List<Servizio> GetAll() => db.Servizi.AsNoTracking().OrderBy(s => s.Id).ToList();
}
