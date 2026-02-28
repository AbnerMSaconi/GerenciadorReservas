# 🏢 Gerenciador de Reservas de Salas (API + Dashboard)

Sistema completo para gestão e controle de reservas de salas de reunião, desenvolvido com **.NET 8 (C#)** e **Microsoft SQL Server**, acompanhado de um painel gerencial interativo.

## 🎯 Arquitetura e Decisões Técnicas

Este projeto foi construído focando em escalabilidade, resiliência e facilidade de deploy.

* **Banco de Dados (SQL Server via Docker):** Atendendo ao requisito do projeto, o sistema utiliza o Microsoft SQL Server. Para elevar o nível da entrega e garantir um ambiente de avaliação sem atritos (sem necessidade de instalar serviços bare-metal ou rodar scripts manuais), a infraestrutura foi containerizada utilizando a imagem oficial `mcr.microsoft.com/mssql/server:2022-latest`.
* **Validação em Duas Camadas:** Travas de segurança implementadas tanto no Front-end (UX/Bloqueio rápido) quanto no Back-end (Data Annotations e lógicas de conflito no Entity Framework) para garantir a integridade dos dados.
* **Auto-Seeding Inteligente:** Ao subir a aplicação pela primeira vez, o sistema detecta o banco vazio e popula automaticamente tabelas de clientes, salas e gera **100 reservas dinâmicas** (distribuídas entre passado e futuro) para viabilizar testes reais de ocupação no Dashboard.
* **Motor Analítico (Dashboard):** O endpoint de gráficos processa as datas de forma dinâmica. Consultas curtas (< 60 dias) retornam volume diário, enquanto consultas longas agrupam o faturamento mensalmente de forma automática.
* **Performance:** Filtros complexos (status, cronologia, nome de cliente, responsável) e paginação são executados estritamente do lado do servidor via LINQ/Entity Framework, poupando a memória do cliente.

## 🚀 Como Executar o Projeto

A aplicação foi desenhada para subir com apenas um comando, contendo o Banco de Dados, a API e as políticas de CORS já configuradas para o ambiente de desenvolvimento.

### Pré-requisitos
* [Docker](https://www.docker.com/) e Docker Compose instalados.
* Porta `8080` (API) e `1433` (SQL Server) livres.

### Passo a Passo
1. Clone este repositório.
2. Na raiz do projeto, execute o comando:
   ```bash
   docker compose up --build -d
Aguarde alguns segundos para o SQL Server inicializar e realizar o auto-seeding.

Acessos:

Frontend (Painel): Abra o arquivo index.html no seu navegador (ou sirva via Live Server/Python HTTP Server na porta 3000).

Documentação da API: http://localhost:5208/swagger

🛠️ Tecnologias Utilizadas
Backend: C# .NET 8, ASP.NET Core Web API, Entity Framework Core.

Banco de Dados: Microsoft SQL Server 2022.

Frontend: HTML5, Bootstrap 5, Vanilla JavaScript (Fetch API).

Gráficos: Chart.js.

Infraestrutura: Docker & Docker Compose.
