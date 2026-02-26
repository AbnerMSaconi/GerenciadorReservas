using GerenciadorReservas.Data;
using GerenciadorReservas.Models; // Necessário para instanciar os dados iniciais
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1. Liberação de segurança para o HTML poder fazer requisições para cá
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. O CORS obrigatoriamente precisa ser ativado aqui, antes dos Controllers!
app.UseCors("PermitirTudo");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 🔹 BLOCO ÚNICO: Validação, Criação e Povoamento do Banco de Dados
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Teste de conexão puro
    try 
    {
        db.Database.CanConnect();
        Console.WriteLine("✅ Conexão com SistemaReservas OK!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro de conexão: {ex.Message}");
    }

    // Verifica se há migrations pendentes
    if (db.Database.GetPendingMigrations().Any())
    {
        Console.WriteLine("⚠️ Há migrations pendentes. Considere aplicar ou usar Database.EnsureCreated()");
    }

    // Cria o banco e as tabelas automaticamente se não existirem
    db.Database.EnsureCreated();

    // 1. Povoa Salas e Clientes base
    if (!db.Salas.Any())
    {
        db.Salas.Add(new Sala { Nome = "Sala Principal", ValorHoraPadrao = 100.00m });
        db.SaveChanges();
    }

    if (!db.Clientes.Any())
    {
        db.Clientes.AddRange(
            new Cliente { Nome = "Nexus AI Solutions", CpfCnpj = "11.111.111/0001-11", Telefone = "67999999999" },
            new Cliente { Nome = "UCDB Tech", CpfCnpj = "22.222.222/0001-22", Telefone = "67888888888" },
            new Cliente { Nome = "JBS Friboi", CpfCnpj = "33.333.333/0001-33", Telefone = "67777777777" }
        );
        db.SaveChanges();
    }

    // 2. Gera 100 Reservas Dinâmicas
    if (db.Reservas.Count() < 100)
    {
        var random = new Random();
        var clienteIds = db.Clientes.Select(c => c.Id).ToList();
        var salaId = db.Salas.First().Id;

        for (int i = 1; i <= 100; i++)
        {
            // Espalha as reservas entre 30 dias no passado e 30 dias no futuro
            int diasDeslocamento = random.Next(-30, 30);
            DateTime dataInicio = DateTime.UtcNow.AddDays(diasDeslocamento).AddHours(random.Next(8, 18));
            // Duração da reunião: 1 a 4 horas
            DateTime dataFim = dataInicio.AddHours(random.Next(1, 5));

            var reserva = new Reserva
            {
                ClienteId = clienteIds[random.Next(clienteIds.Count)],
                SalaId = salaId,
                TituloReserva = $"Reunião Estratégica {i}",
                Responsavel = $"Gestor {i}",
                DataInicio = dataInicio,
                DataFim = dataFim,
                ParticipantesPrevistos = random.Next(2, 20),
                ValorHora = 100.00m,
                // Desconto de 0, 10 ou 20% apenas para reservas futuras
                Desconto = dataInicio > DateTime.UtcNow ? random.Next(0, 3) * 10 : 0, 
                StatusPagamento = dataInicio < DateTime.UtcNow ? "Pago" : "Pendente"
            };

            // Aplica a regra de negócio considerando o seu método CalcularValores atual
            reserva.CalcularValores(DateTime.UtcNow); 

            db.Reservas.Add(reserva);
        }
        
        db.SaveChanges();
        Console.WriteLine("✅ 100 Reservas geradas com sucesso para testes do dashboard!");
    }
}

app.Run();