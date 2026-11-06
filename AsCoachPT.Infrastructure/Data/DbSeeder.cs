using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

public static class DbSeeder
{
    public static void Seed(AsCoachDbContext db)
    {
        if (!db.Ruoli.Any())
        {
            db.Ruoli.AddRange(
                new Ruolo { Name = UserRole.ADMIN },
                new Ruolo { Name = UserRole.USER },
                new Ruolo { Name = UserRole.PERSONAL_TRAINER });
            db.SaveChanges();
        }

        if (!db.Massaggi.Any())
        {
            db.Massaggi.AddRange(
                new Massaggio { TipoMassaggio = TipoMassaggio.RILASSANTE, Durata = 60 },
                new Massaggio { TipoMassaggio = TipoMassaggio.DECONTRATTURANTE_SPORTIVO, Durata = 30 });
            db.SaveChanges();
        }

        if (!db.Massaggi.Any(m => m.TipoMassaggio == TipoMassaggio.APPLICAZIONE_TAPE))
        {
            db.Massaggi.Add(new Massaggio { TipoMassaggio = TipoMassaggio.APPLICAZIONE_TAPE, Durata = 15 });
            db.SaveChanges();
        }

        if (!db.Consulenze.Any())
        {
            db.Consulenze.AddRange(
                new Consulenza { TipoConsulenza = TipoConsulenza.VALUTAZIONE_INIZIALE, Durata = 30 },
                new Consulenza { TipoConsulenza = TipoConsulenza.MIGLIORAMENTO_SCHEDA, Durata = 15 });
            db.SaveChanges();
        }
    }
}
