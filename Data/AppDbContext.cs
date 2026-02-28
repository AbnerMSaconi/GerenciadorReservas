using Microsoft.EntityFrameworkCore;
using GerenciadorReservas.Models;

namespace GerenciadorReservas.Data
{
    public class AppDbContext : DbContext
    {
        // Construtor: recebe as opções e repassa para o DbContext base.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets: representam as tabelas do banco que o contexto gerencia.
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Sala> Salas { get; set; }
        public DbSet<Reserva> Reservas { get; set; }

        // Configuração do modelo: mapeia propriedades, tamanhos, tipos e relacionamentos.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da entidade Cliente: tabela, chave e propriedades (tamanhos e obrigatoriedades).
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.ToTable("Clientes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CpfCnpj).HasMaxLength(20);
                entity.Property(e => e.Telefone).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
            });

            // Configuração da entidade Sala: tabela, chave e propriedades (nome e valor padrão por hora).
            modelBuilder.Entity<Sala>(entity =>
            {
                entity.ToTable("Salas");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ValorHoraPadrao).HasColumnType("decimal(18,2)");
            });

            // Configuração da entidade Reserva: propriedades, tipos e relacionamentos explícitos.
            modelBuilder.Entity<Reserva>(entity =>
            {
                entity.ToTable("Reservas");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.TituloReserva).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Responsavel).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ValorHora).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Desconto).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.ValorTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StatusPagamento).HasMaxLength(50).HasDefaultValue("Pendente");

                // Relacionamento com Cliente: define FK e evita exclusão em cascata.
                entity.HasOne(r => r.Cliente)
                      .WithMany()
                      .HasForeignKey(r => r.ClienteId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com Sala: define FK e evita exclusão em cascata.
                entity.HasOne(r => r.Sala)
                      .WithMany()
                      .HasForeignKey(r => r.SalaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}