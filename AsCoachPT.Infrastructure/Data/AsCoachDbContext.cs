using AsCoachPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AsCoachPT.Infrastructure.Data;

public class AsCoachDbContext : DbContext
{
    public AsCoachDbContext(DbContextOptions<AsCoachDbContext> options) : base(options) { }

    public DbSet<Utente> Utenti => Set<Utente>();
    public DbSet<Ruolo> Ruoli => Set<Ruolo>();
    public DbSet<Indirizzo> Indirizzi => Set<Indirizzo>();
    public DbSet<Servizio> Servizi => Set<Servizio>();
    public DbSet<Consulenza> Consulenze => Set<Consulenza>();
    public DbSet<Massaggio> Massaggi => Set<Massaggio>();
    public DbSet<Prenotazione> Prenotazioni => Set<Prenotazione>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Utente>(e =>
        {
            e.ToTable("utenti");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nome).HasColumnName("nome").IsRequired();
            e.Property(x => x.Cognome).HasColumnName("cognome").IsRequired();
            e.Property(x => x.DataDiNascita).HasColumnName("data_di_nascita");
            e.Property(x => x.Email).HasColumnName("email").IsRequired();
            e.Property(x => x.Username).HasColumnName("username").IsRequired();
            e.Property(x => x.Password).HasColumnName("password").IsRequired();
            e.Property(x => x.Ruolo).HasColumnName("ruolo").HasConversion<string>();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasMany(x => x.Prenotazioni).WithOne(p => p.Utente).HasForeignKey(p => p.UtenteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ruolo>(e =>
        {
            e.ToTable("ruoli");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasConversion<string>();
        });

        modelBuilder.Entity<Indirizzo>(e =>
        {
            e.ToTable("indirizzi");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Via).HasColumnName("via");
            e.Property(x => x.NumeroCivico).HasColumnName("numero_civico");
            e.Property(x => x.Citta).HasColumnName("citta");
            e.Property(x => x.Provincia).HasColumnName("provincia");
            e.Property(x => x.Latitudine).HasColumnName("latitudine");
            e.Property(x => x.Longitudine).HasColumnName("longitudine");
            e.Property(x => x.NomeStudio).HasColumnName("nome_studio");
            e.Property(x => x.GiorniDisponibili).HasColumnName("giorni_disponibili");
        });

        modelBuilder.Entity<Servizio>(e =>
        {
            e.ToTable("servizi");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Durata).HasColumnName("durata");
            e.HasDiscriminator<string>("servizio_type")
                .HasValue<Consulenza>("CONSULENZA")
                .HasValue<Massaggio>("MASSAGGIO");
        });

        modelBuilder.Entity<Consulenza>(e =>
        {
            e.Property(x => x.TipoConsulenza).HasColumnName("tipo_consulenza").HasConversion<string>();
        });

        modelBuilder.Entity<Massaggio>(e =>
        {
            e.Property(x => x.TipoMassaggio).HasColumnName("tipo_massaggio").HasConversion<string>();
        });

        modelBuilder.Entity<Prenotazione>(e =>
        {
            e.ToTable("prenotazioni");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.UtenteId).HasColumnName("utente_id");
            e.Property(x => x.DataOra).HasColumnName("data_ora").HasColumnType("timestamp without time zone");
            e.Property(x => x.ServizioId).HasColumnName("servizio_id");
            e.Property(x => x.IndirizzoId).HasColumnName("indirizzo_id");
            e.Property(x => x.Note).HasColumnName("note");
            e.HasOne(x => x.Servizio).WithMany().HasForeignKey(x => x.ServizioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Indirizzo).WithMany().HasForeignKey(x => x.IndirizzoId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
