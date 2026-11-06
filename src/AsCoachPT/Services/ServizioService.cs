using AsCoachPT.Data;
using AsCoachPT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Services;

public class ServizioService(AsCoachDbContext db)
{
    public List<Servizio> GetAll() => db.Servizi.AsNoTracking().OrderBy(s => s.Id).ToList();
}
