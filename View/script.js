// Configurações de API e URLs base
const API_BASE = 'http://localhost:5208/api';
const RESERVAS_URL = `${API_BASE}/reservas`;
const CLIENTES_URL = `${API_BASE}/clientes`;
const SALAS_URL = `${API_BASE}/salas`;

// Variáveis globais usadas para paginação das reservas
let paginaAtualReservas = 1;
const TAMANHO_PAGINA = 10;
let totalPaginasGlobais = 1;

// Função utilitária responsável por exibir notificações tipo 'toast' no canto superior direito.
// Recebe uma mensagem e um tipo (success, error, warning, etc.).
function notify(msg, type = 'success') {
    const container = document.getElementById('toastContainer');
    const id = `t-${Date.now()}`;
    container.insertAdjacentHTML('beforeend', `<div id="${id}" class="toast align-items-center text-white bg-${type === 'error' ? 'danger' : type} border-0" role="alert"><div class="d-flex"><div class="toast-body">${msg}</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div></div>`);
    const toast = new bootstrap.Toast(document.getElementById(id));
    toast.show();
}

// Carrega lista de clientes da API e popula o <select> do formulário e a tabela de clientes.
// Executada ao iniciar a página ou após alterações no cadastro.
async function loadClients() {
    try {
        const r = await fetch(CLIENTES_URL);
        const data = await r.json();
        const select = document.getElementById('clienteId');
        const tbody = document.getElementById('tabelaClientes');

        select.innerHTML = '<option value="">Selecione...</option>';
        tbody.innerHTML = data.length ? '' : '<tr><td colspan="5" class="text-center">Nenhum cliente cadastrado</td></tr>';

        data.forEach(c => {
            select.innerHTML += `<option value="${c.id}">${c.nome}</option>`;
            tbody.innerHTML += `
                <tr>
                    <td>${c.id}</td>
                    <td>${c.nome}</td>
                    <td>${c.cpfCnpj || '-'}</td>
                    <td>${c.telefone || '-'}</td>
                    <td>
                        <button class="btn btn-acao btn-outline-primary" onclick="editCliente(${c.id})">✏️</button>
                        <button class="btn btn-acao btn-outline-danger" onclick="deleteCliente(${c.id})">🗑️</button>
                    </td>
                </tr>`;
        });
    } catch (e) { notify('Erro ao carregar clientes', 'error'); }
}

// Busca reservas no servidor levando em conta filtros e paginação.
// Atualiza a tabela de reservas e controla botões de paginação.
async function loadReservas(pagina = 1) {
    paginaAtualReservas = pagina;
    try {
        const status = document.getElementById('filtroStatus')?.value || '';
        const dataInicio = document.getElementById('filtroDataInicio')?.value || '';
        const dataFim = document.getElementById('filtroDataFim')?.value || '';
        const clienteNome = document.getElementById('filtroEmpresa')?.value || '';
        const responsavel = document.getElementById('filtroResponsavel')?.value || '';
        const ordenacao = document.getElementById('filtroOrdenacao')?.value || 'asc';

        let url = `${RESERVAS_URL}?pagina=${pagina}&tamanhoPagina=${TAMANHO_PAGINA}&ordenacao=${ordenacao}`;
        if (status) url += `&status=${encodeURIComponent(status)}`;
        if (dataInicio) url += `&dataInicio=${dataInicio}`;
        if (dataFim) url += `&dataFim=${dataFim}`;
        if (clienteNome) url += `&clienteNome=${encodeURIComponent(clienteNome)}`;
        if (responsavel) url += `&responsavel=${encodeURIComponent(responsavel)}`;

        const r = await fetch(url);
        const result = await r.json();

        const dados = result.dados || result;
        totalPaginasGlobais = result.totalPaginas || 1;

        const tbody = document.getElementById('tabelaReservas');
        tbody.innerHTML = dados.length ? '' : '<tr><td colspan="11" class="text-center">Nenhuma reserva encontrada</td></tr>';

       dados.forEach(res => {
            const statusClass = { "Em andamento": "table-em-andamento", "Futuras proximas": "table-futuras-proximas", "Futuras normais": "table-futuras-normais", "Encerradas": "table-encerradas" }[res.statusCalculado];
            const badgeClass = { "Em andamento": "bg-success", "Futuras proximas": "bg-warning text-dark", "Futuras normais": "bg-primary", "Encerradas": "bg-secondary" }[res.statusCalculado] || "bg-dark";

            const btnPagamento = res.statusPagamento === 'Pago' 
                ? `<button class="btn btn-sm btn-success w-100 py-0" onclick="togglePagamento(${res.id})">✅ Pago</button>`
                : `<button class="btn btn-sm btn-outline-warning text-dark w-100 py-0" onclick="togglePagamento(${res.id})">⏳ Pend.</button>`;

            // 🔹 Formatação de data blindada (sem o bug do substring)
            const opcoesData = { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' };
            const dataIn = new Date(res.dataInicio).toLocaleString('pt-BR', opcoesData);
            const dataFim = new Date(res.dataFim).toLocaleString('pt-BR', opcoesData);

            const row = document.createElement('tr');
            row.className = statusClass || '';
            row.innerHTML = `
                <td class="text-truncate" style="max-width: 120px;" title="${res.clienteNome}">${res.clienteNome}</td>
                <td class="text-truncate" style="max-width: 140px;" title="${res.tituloReserva}">${res.tituloReserva}</td>
                <td class="text-truncate" style="max-width: 100px;" title="${res.responsavel}">${res.responsavel}</td>
                <td class="fw-bold text-info" style="white-space: nowrap;">${res.salaNome || 'N/A'}</td> 
                <td class="text-center fw-bold text-primary">${res.participantesPrevistos}</td>
                <td style="white-space: nowrap; font-size: 0.85em;">${dataIn}</td>
                <td style="white-space: nowrap; font-size: 0.85em;">${dataFim}</td>
                <td style="white-space: nowrap;"><span class="badge ${badgeClass}">${res.statusCalculado}</span></td>
                <td style="white-space: nowrap;">R$ ${res.valorHora.toFixed(2)}</td>
                <td style="width: 70px;"><input type="number" class="form-control form-control-sm text-center px-1 py-0" value="${res.desconto}" onblur="updateDiscount(${res.id}, this.value)" ${res.statusCalculado === 'Encerradas' ? 'disabled' : ''}></td>
                <td style="white-space: nowrap;"><strong>R$ ${res.valorTotal.toFixed(2)}</strong></td>
                <td style="width: 95px;">${btnPagamento}</td>
                <td style="white-space: nowrap;">
                    <button class="btn btn-sm btn-outline-primary py-0 px-2" onclick="editReserva(${res.id})">✏️</button> 
                    <button class="btn btn-sm btn-outline-danger py-0 px-2" onclick="deleteReserva(${res.id})">🗑️</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        const elInfo = document.getElementById('infoPaginacao');
        const elPrev = document.getElementById('btnPagAnterior');
        const elNext = document.getElementById('btnPagProxima');

        if (elInfo) elInfo.innerText = `Página ${paginaAtualReservas} de ${totalPaginasGlobais}`;
        if (elPrev) elPrev.disabled = paginaAtualReservas <= 1;
        if (elNext) elNext.disabled = paginaAtualReservas >= totalPaginasGlobais;

    } catch (e) { notify('Erro ao listar reservas com filtros', 'error'); }
}

// Reseta os campos de filtro de reserva e recarrega a primeira página de resultados.
function limparFiltros() {
    document.getElementById('formFiltros').reset();
    loadReservas(1);
}

// Controla a navegação entre páginas de reservas (anterior/próxima).
function mudarPagina(direcao) {
    const novaPagina = paginaAtualReservas + direcao;
    if (novaPagina >= 1 && novaPagina <= totalPaginasGlobais) {
        loadReservas(novaPagina);
    }
}

// Gatilho do submit do formulário de cliente: decide entre criar ou atualizar e envia para a API.
document.getElementById('formCliente').addEventListener('submit', async function (e) {
    e.preventDefault();
    const editId = this.dataset.editId;
    const data = {
        id: editId ? parseInt(editId) : 0,
        nome: document.getElementById('nomeCliente').value,
        cpfCnpj: document.getElementById('cpfCnpjCliente').value,
        telefone: document.getElementById('telefoneCliente').value
    };

    const url = editId ? `${CLIENTES_URL}/${editId}` : CLIENTES_URL;
    const method = editId ? 'PUT' : 'POST';

    try {
        const r = await fetch(url, { method: method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (r.ok) {
            notify(editId ? 'Cliente Atualizado!' : 'Cliente Salvo!');
            cancelarEdicaoCliente();
            loadClients();
        } else {
            const err = await r.json();
            notify(err.message || 'Erro na operação', 'error');
        }
    } catch (e) { notify('Erro de rede', 'error'); }
});

// Preenche o formulário com os dados de um cliente existente para edição.
async function editCliente(id) {
    try {
        const r = await fetch(`${CLIENTES_URL}/${id}`);
        if (!r.ok) throw new Error(`Erro na API: ${r.status}`);

        const c = await r.json();
        const form = document.getElementById('formCliente');
        form.dataset.editId = id;

        const spanHeader = form.closest('.card').querySelector('.card-header span');
        if (spanHeader) {
            spanHeader.innerText = `✏️ Editando Cliente #${id}`;
        }

        const btnSalvar = form.querySelector('button[type="submit"]');
        if (btnSalvar) {
            btnSalvar.innerText = '💾 Atualizar Cliente';
            btnSalvar.className = 'btn btn-warning w-100';
        }

        document.getElementById('nomeCliente').value = c.nome || '';
        document.getElementById('cpfCnpjCliente').value = c.cpfCnpj || '';
        document.getElementById('telefoneCliente').value = c.telefone || '';

        window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (e) {
        console.error("🕵️ Erro real capturado:", e);
        notify(`Erro na interface: ${e.message}`, 'error');
    }
}

// Remove um cliente após confirmação; recarrega lista ao final.
async function deleteCliente(id) {
    if (!confirm('Excluir este cliente de forma permanente?')) return;
    try {
        const r = await fetch(`${CLIENTES_URL}/${id}`, { method: 'DELETE' });
        if (r.ok) {
            notify('Cliente removido!');
            loadClients();
        } else {
            const err = await r.json();
            notify(err.message || 'Falha ao remover', 'error');
        }
    } catch (e) { notify('Erro de conexão ao remover', 'error'); }
}

// Restaura o formulário de cliente ao estado de criação (limpa edição atual).
function cancelarEdicaoCliente() {
    const form = document.getElementById('formCliente');
    delete form.dataset.editId;
    form.reset();
    form.closest('.card').querySelector('.card-header span').innerText = '👤 Cadastrar Novo Cliente';
    const btnSalvar = form.querySelector('button[type="submit"]');
    btnSalvar.innerText = '💾 Salvar Cliente';
    btnSalvar.className = 'btn btn-primary w-100';
}

// Listener para submissão do formulário de reserva: valida campos e chama API (POST/PUT).
document.getElementById('formReserva').addEventListener('submit', async function (e) {
    e.preventDefault();
    //validacao basica
    const inicio = new Date(document.getElementById('dataInicio').value);
    const fim = new Date(document.getElementById('dataFim').value);
    const participantes = parseInt(document.getElementById('participantes').value);
    const valorHora = parseFloat(document.getElementById('valorHora').value);

    if (fim <= inicio) { notify('Data de fim deve ser após início', 'error'); return; }
    if (participantes <= 0) { notify('Participantes devem ser > 0', 'error'); return; }
    if (valorHora <= 0) { notify('Valor por hora deve ser > 0', 'error'); return; }

    const editId = this.dataset.editId;
    const data = { id: editId ? parseInt(editId) : 0, clienteId: parseInt(document.getElementById('clienteId').value), salaId: parseInt(document.getElementById('salaId').value),
         tituloReserva: document.getElementById('tituloReserva').value, responsavel: document.getElementById('responsavel').value, 
         dataInicio: new Date(document.getElementById('dataInicio').value).toISOString(), dataFim: new Date(document.getElementById('dataFim').value).toISOString(), 
         participantesPrevistos: parseInt(document.getElementById('participantes').value), valorHora: parseFloat(document.getElementById('valorHora').value) };
    try {
        const r = await fetch(editId ? `${RESERVAS_URL}/${editId}` : RESERVAS_URL, { method: editId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (r.ok) { notify(editId ? 'Atualizado!' : 'Salvo!'); cancelarEdicao(); loadReservas(paginaAtualReservas); loadResumo(); }
        else {
            const err = await r.json().catch(() => ({ message: 'Erro desconhecido' }));
            let msg = err.message || 'Dados inválidos';

            if (err.errors) {
                const detalhes = Object.values(err.errors).flat().join(' ');
                msg += ` Detalhes: ${detalhes}`;
            }

            notify(msg, 'error');
        }
    } catch (e) { notify('Erro de rede', 'error'); }
});

// Prepara o formulário de reserva com os dados existentes para edição de uma reserva específica.
async function editReserva(id) {
    const r = await fetch(`${RESERVAS_URL}/${id}`);
    const res = await r.json();
    const form = document.getElementById('formReserva');
    form.dataset.editId = id;
    document.getElementById('formTitle').innerText = `✏️ Editando #${id}`;
    document.getElementById('btnSalvarReserva').className = 'btn btn-warning w-100';
    document.getElementById('clienteId').value = res.clienteId;
    document.getElementById('salaId').value = res.salaId; // 🔹 CORREÇÃO: Faltava preencher a sala!
    document.getElementById('tituloReserva').value = res.tituloReserva;
    document.getElementById('responsavel').value = res.responsavel;
    document.getElementById('dataInicio').value = res.dataInicio.substring(0, 16);
    document.getElementById('dataFim').value = res.dataFim.substring(0, 16);
    document.getElementById('participantes').value = res.participantesPrevistos;
    document.getElementById('valorHora').value = res.valorHora;
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// Deleta uma reserva após confirmação simples e recarrega a página atual de resultados.
async function deleteReserva(id) {
    if (confirm('Excluir?')) {
        await fetch(`${RESERVAS_URL}/${id}`, { method: 'DELETE' });
        loadReservas(paginaAtualReservas);
        loadResumo();
    }
}

// Atualiza o desconto de uma reserva via PATCH e refaz a listagem de reservas.
async function updateDiscount(id, val) {
    await fetch(`${RESERVAS_URL}/${id}/desconto`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(parseFloat(val)) });
    loadReservas(paginaAtualReservas);
    loadResumo();
}

// Limpa e reseta o formulário de reserva, voltando ao estado de criação em vez de edição.
function cancelarEdicao() {
    const form = document.getElementById('formReserva');
    delete form.dataset.editId;
    form.reset();
    document.getElementById('formTitle').innerText = '✨ Registrar Nova Reserva';
    document.getElementById('btnSalvarReserva').className = 'btn btn-success w-100';
    document.getElementById('valorHora').value = "100.00";
}


// Executado ao submeter filtros do dashboard: atualiza resumo e gráfico de ocupação.
async function aplicarFiltrosDashboard() {
    await loadResumo();
    if (graficoOcupacao) loadGrafico();
}

// Reseta filtros do dashboard e reaplica para recarregar dados.
function limparFiltrosDashboard() {
    document.getElementById('formFiltrosDashboard').reset();
    aplicarFiltrosDashboard();
}

// Busca e atualiza os cartões de resumo financeiro/operacional do dashboard.
async function loadResumo() {
    try {
        const di = document.getElementById('dashDataInicio')?.value || '';
        const df = document.getElementById('dashDataFim')?.value || '';
        let url = `${RESERVAS_URL}/resumo?`;
        if (di) url += `dataInicio=${di}&`;
        if (df) url += `dataFim=${df}`;

        const r = await fetch(url);
        if (!r.ok) throw new Error('Falha ao buscar resumo');
        const resumo = await r.json();

        const cards = document.getElementById('cardsResumo');
        if (cards) cards.style.opacity = '0.5';

        document.getElementById('resumoAtivas').innerText = resumo.ativas;
        document.getElementById('resumoHoras').innerText = resumo.totalHoras + 'h';
        document.getElementById('resumoFaturamento').innerText = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(resumo.faturamentoRealizado);
        document.getElementById('resumoPrevisto').innerText = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(resumo.faturamentoPrevisto);

        if (cards) setTimeout(() => cards.style.opacity = '1', 200);

    } catch (e) { console.error("Erro no resumo:", e); }
}

// Alterna o status de pagamento de uma reserva (via POST) e atualiza a interface.
async function togglePagamento(id) {
    try {
        const r = await fetch(`${RESERVAS_URL}/${id}/pagamento`, { method: 'POST' });

        if (r.ok) {
            notify('Status Financeiro Atualizado!');
            loadReservas(paginaAtualReservas); // Atualiza a tabela
            loadResumo(); // 🔹 A SOLUÇÃO: Força o Dashboard a atualizar imediatamente
        } else {
            const err = await r.json().catch(() => ({ message: 'Erro na resposta do servidor' }));
            console.error("Erro do servidor:", err);
            notify(`Falha: ${err.message || 'Erro na API'}`, 'error');
        }
    } catch (e) {
        console.error("Falha de comunicação:", e);
        notify('Erro de conexão com a API', 'error');
    }
}
// ==========================================
// MÓDULO DO DASHBOARD E GRÁFICO
// ==========================================
// Solicita dados de reserva para geração de gráfico e renderiza com Chart.js,
// destruindo qualquer instância antiga para evitar sobreposição.
async function loadGrafico() {
    try {
        const di = document.getElementById('dashDataInicio')?.value || '';
        const df = document.getElementById('dashDataFim')?.value || '';
        let url = `${RESERVAS_URL}/grafico?`;
        if (di) url += `dataInicio=${di}&`;
        if (df) url += `dataFim=${df}`;

        const r = await fetch(url);
        if (!r.ok) throw new Error('Falha ao buscar dados do gráfico');

        const dados = await r.json();
        const labels = dados.map(d => d.data);
        const volume = dados.map(d => d.quantidade);
        const faturamento = dados.map(d => d.faturamento);

        const canvas = document.getElementById('graficoOcupacao');
        if (!canvas) return;

        canvas.parentElement.style.height = '350px';
        const ctx = canvas.getContext('2d');

        // 🔹 A CORREÇÃO: Pede para o próprio Chart.js localizar e destruir o gráfico antigo com segurança
        const chartExistente = Chart.getChart("graficoOcupacao");
        if (chartExistente) {
            chartExistente.destroy();
        }

        // Desenha o gráfico novo sem conflitos
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    { label: 'Faturamento (R$)', type: 'line', data: faturamento, borderColor: '#198754', backgroundColor: '#198754', borderWidth: 3, tension: 0.3, yAxisID: 'yFaturamento' },
                    { label: 'Volume de Reservas', type: 'bar', data: volume, backgroundColor: 'rgba(13, 110, 253, 0.7)', borderColor: '#0d6efd', borderWidth: 1, borderRadius: 4, yAxisID: 'yVolume' }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
                scales: {
                    yVolume: { type: 'linear', position: 'left', title: { display: true, text: 'Qtd Reservas' }, beginAtZero: true, ticks: { stepSize: 1 } },
                    yFaturamento: { type: 'linear', position: 'right', title: { display: true, text: 'Faturamento (R$)' }, beginAtZero: true, grid: { drawOnChartArea: false } }
                }
            }
        });
    } catch (e) { console.error("Erro ao carregar gráfico:", e); }
}
// ==========================================
// MÓDULO DE SALAS
// ==========================================
async function loadSalas() {
    try {
        const r = await fetch(SALAS_URL);
        const salas = await r.json();

        const tbody = document.getElementById('tabelaSalas');
        if (tbody) {
            tbody.innerHTML = salas.length ? '' : '<tr><td colspan="4" class="text-center">Nenhuma sala cadastrada</td></tr>';
            salas.forEach(s => {
                tbody.innerHTML += `
                    <tr>
                        <td>${s.id}</td>
                        <td>${s.nome}</td>
                        <td>R$ ${s.valorHoraPadrao.toFixed(2)}</td>
                        <td>
                            <button class="btn btn-acao btn-outline-primary" onclick="editSala(${s.id})">✏️</button>
                            <button class="btn btn-acao btn-outline-danger" onclick="deleteSala(${s.id})">🗑️</button>
                        </td>
                    </tr>`;
            });
        }

        const select = document.getElementById('salaId');
        if (select) {
            // Guarda o valor que estava selecionado antes de recarregar (se houver)
            const valorAtual = select.value; 
            select.innerHTML = '<option value="">Selecione a sala...</option>';
            salas.forEach(s => {
                select.innerHTML += `<option value="${s.id}" data-valor="${s.valorHoraPadrao}">${s.nome} (R$ ${s.valorHoraPadrao}/h)</option>`;
            });
            if (valorAtual) select.value = valorAtual;
        }
    } catch (e) { notify('Erro ao carregar salas', 'error'); }
}

document.getElementById('formSala')?.addEventListener('submit', async function (e) {
    e.preventDefault();
    const editId = this.dataset.editId;
    const data = {
        id: editId ? parseInt(editId) : 0,
        nome: document.getElementById('nomeSala').value,
        valorHoraPadrao: parseFloat(document.getElementById('valorHoraPadrao').value)
    };

    const url = editId ? `${SALAS_URL}/${editId}` : SALAS_URL;
    const method = editId ? 'PUT' : 'POST';

    try {
        const r = await fetch(url, { method: method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (r.ok) {
            notify(editId ? 'Sala Atualizada!' : 'Sala Cadastrada!');
            cancelarEdicaoSala();
            loadSalas();
        } else {
            const err = await r.json().catch(() => ({ message: 'Erro na operação' }));
            notify(`Falha: ${err.message || 'Dados inválidos'}`, 'error');
        }
    } catch (e) { notify('Erro de rede', 'error'); }
});

async function editSala(id) {
    try {
        const r = await fetch(`${SALAS_URL}/${id}`);
        if (!r.ok) throw new Error('Sala não encontrada');
        const s = await r.json();

        const form = document.getElementById('formSala');
        form.dataset.editId = id;
        document.getElementById('formSalaTitle').innerText = `✏️ Editando Sala #${id}`;
        document.getElementById('nomeSala').value = s.nome;
        document.getElementById('valorHoraPadrao').value = s.valorHoraPadrao;
        
        const btn = document.getElementById('btnSalvarSala');
        btn.innerText = '💾 Atualizar';
        btn.className = 'btn btn-warning w-100';
        
        window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (e) { notify(e.message, 'error'); }
}

async function deleteSala(id) {
    if (!confirm('Deseja realmente excluir esta sala?')) return;
    try {
        const r = await fetch(`${SALAS_URL}/${id}`, { method: 'DELETE' });
        if (r.ok) {
            notify('Sala removida!');
            loadSalas();
        } else {
            const err = await r.json().catch(() => ({ message: 'Falha ao excluir' }));
            notify(err.message, 'error');
        }
    } catch (e) { notify('Erro de rede', 'error'); }
}

function cancelarEdicaoSala() {
    const form = document.getElementById('formSala');
    delete form.dataset.editId;
    form.reset();
    document.getElementById('formSalaTitle').innerText = '🚪 Cadastrar Nova Sala';
    const btn = document.getElementById('btnSalvarSala');
    btn.innerText = '💾 Salvar';
    btn.className = 'btn btn-success w-100';
}
// INICIALIZAÇÃO DA PÁGINA
// ==========================================
// Inicialização geral: ao carregar a página, busca clientes, reservas e resumo,
// configura máscaras e eventos para elementos de formulário e abas.
document.addEventListener('DOMContentLoaded', () => {
    loadClients();
    loadReservas(1);
    loadResumo();
    loadSalas();
    // Atualiza o valor por hora automaticamente ao selecionar uma sala
    document.getElementById('salaId')?.addEventListener('change', function() {
        const option = this.options[this.selectedIndex];
        if (option && option.dataset.valor) {
            document.getElementById('valorHora').value = parseFloat(option.dataset.valor).toFixed(2);
        }
    });

    // Tratamento de Máscaras
    document.getElementById('cpfCnpjCliente')?.addEventListener('input', function (e) {
        let v = e.target.value.replace(/\D/g, '');
        if (v.length <= 11) {
            v = v.replace(/(\d{3})(\d)/, '$1.$2');
            v = v.replace(/(\d{3})(\d)/, '$1.$2');
            v = v.replace(/(\d{3})(\d{1,2})$/, '$1-$2');
        } else {
            v = v.replace(/^(\d{2})(\d)/, '$1.$2');
            v = v.replace(/^(\d{2})\.(\d{3})(\d)/, '$1.$2.$3');
            v = v.replace(/\.(\d{3})(\d)/, '.$1/$2');
            v = v.replace(/(\d{4})(\d)/, '$1-$2');
        }
        e.target.value = v;
    });

    document.getElementById('telefoneCliente')?.addEventListener('input', function (e) {
        let v = e.target.value.replace(/\D/g, '');
        v = v.replace(/^(\d{2})(\d)/g, '($1) $2');
        v = v.replace(/(\d)(\d{4})$/, '$1-$2');
        e.target.value = v;
    });

    // Tratamento de Renderização do Gráfico no Bootstrap Lifecycle
    const tabDashboard = document.getElementById('dashboard-tab');
    if (tabDashboard) {
        tabDashboard.addEventListener('shown.bs.tab', function () {
            // Pede para o Chart.js localizar o gráfico ativo
            const chartExistente = Chart.getChart("graficoOcupacao");

            if (!chartExistente) {
                loadGrafico();
            } else {
                chartExistente.resize();
            }
        });
    }
});