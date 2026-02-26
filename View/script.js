const API_BASE = 'http://localhost:5208/api';
const RESERVAS_URL = `${API_BASE}/reservas`;
const CLIENTES_URL = `${API_BASE}/clientes`;

// Variáveis Globais de Paginação
let paginaAtualReservas = 1;
const TAMANHO_PAGINA = 10;
let totalPaginasGlobais = 1;

function notify(msg, type = 'success') {
    const container = document.getElementById('toastContainer');
    const id = `t-${Date.now()}`;
    container.insertAdjacentHTML('beforeend', `<div id="${id}" class="toast align-items-center text-white bg-${type === 'error' ? 'danger' : type} border-0" role="alert"><div class="d-flex"><div class="toast-body">${msg}</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div></div>`);
    const toast = new bootstrap.Toast(document.getElementById(id));
    toast.show();
}

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

// 🔹 FUNÇÃO ATUALIZADA COM PAGINAÇÃO
async function loadReservas(pagina = 1) {
    paginaAtualReservas = pagina;
    try {
        // Captura os valores dos filtros
        const status = document.getElementById('filtroStatus')?.value || '';
        const dataInicio = document.getElementById('filtroDataInicio')?.value || '';
        const dataFim = document.getElementById('filtroDataFim')?.value || '';

        // Monta a URL dinâmica
        let url = `${RESERVAS_URL}?pagina=${pagina}&tamanhoPagina=${TAMANHO_PAGINA}`;
        if (status) url += `&status=${encodeURIComponent(status)}`;
        if (dataInicio) url += `&dataInicio=${dataInicio}`;
        if (dataFim) url += `&dataFim=${dataFim}`;

        const r = await fetch(url);
        const result = await r.json();
        
        const dados = result.dados || result; 
        totalPaginasGlobais = result.totalPaginas || 1;

        const tbody = document.getElementById('tabelaReservas');
        tbody.innerHTML = dados.length ? '' : '<tr><td colspan="10" class="text-center">Nenhuma reserva encontrada</td></tr>';

        dados.forEach(res => {
            const statusClass = { "Em andamento": "table-em-andamento", "Futuras proximas": "table-futuras-proximas", "Futuras normais": "table-futuras-normais", "Encerradas": "table-encerradas" }[res.statusCalculado];
            const badgeClass = { "Em andamento": "bg-success", "Futuras proximas": "bg-warning text-dark", "Futuras normais": "bg-primary", "Encerradas": "bg-secondary" }[res.statusCalculado] || "bg-dark";
            
            const row = document.createElement('tr');
            row.className = statusClass || '';
            row.innerHTML = `<td>${res.id}</td><td>${res.tituloReserva}</td><td>${res.responsavel}</td><td>${new Date(res.dataInicio).toLocaleString('pt-BR')}</td><td>${new Date(res.dataFim).toLocaleString('pt-BR')}</td><td><span class="badge ${badgeClass}">${res.statusCalculado}</span></td><td>R$ ${res.valorHora.toFixed(2)}</td><td><input type="number" class="form-control form-control-sm input-inline" value="${res.desconto}" onblur="updateDiscount(${res.id}, this.value)" ${res.statusCalculado === 'Encerradas' ? 'disabled' : ''}></td><td>R$ ${res.valorTotal.toFixed(2)}</td><td><button class="btn btn-acao btn-outline-primary" onclick="editReserva(${res.id})">✏️</button> <button class="btn btn-acao btn-outline-danger" onclick="deleteReserva(${res.id})">🗑️</button></td>`;
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

// Adicione esta função em qualquer lugar do script.js
function limparFiltros() {
    document.getElementById('formFiltros').reset();
    loadReservas(1);
}

// 🔹 NOVA FUNÇÃO DE CONTROLE DE PÁGINA
function mudarPagina(direcao) {
    const novaPagina = paginaAtualReservas + direcao;
    if (novaPagina >= 1 && novaPagina <= totalPaginasGlobais) {
        loadReservas(novaPagina);
    }
}

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

function cancelarEdicaoCliente() {
    const form = document.getElementById('formCliente');
    delete form.dataset.editId;
    form.reset();
    form.closest('.card').querySelector('.card-header span').innerText = '👤 Cadastrar Novo Cliente';
    const btnSalvar = form.querySelector('button[type="submit"]');
    btnSalvar.innerText = '💾 Salvar Cliente';
    btnSalvar.className = 'btn btn-primary w-100';
}

document.getElementById('formReserva').addEventListener('submit', async function (e) {
    e.preventDefault();
    const editId = this.dataset.editId;
    const data = { id: editId ? parseInt(editId) : 0, clienteId: parseInt(document.getElementById('clienteId').value), salaId: 1, tituloReserva: document.getElementById('tituloReserva').value, responsavel: document.getElementById('responsavel').value, dataInicio: new Date(document.getElementById('dataInicio').value).toISOString(), dataFim: new Date(document.getElementById('dataFim').value).toISOString(), participantesPrevistos: parseInt(document.getElementById('participantes').value), valorHora: parseFloat(document.getElementById('valorHora').value) };
    try {
        const r = await fetch(editId ? `${RESERVAS_URL}/${editId}` : RESERVAS_URL, { method: editId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (r.ok) { notify(editId ? 'Atualizado!' : 'Salvo!'); cancelarEdicao(); loadReservas(paginaAtualReservas); }
        else { notify('Erro na operação', 'error'); }
    } catch (e) { notify('Erro de rede', 'error'); }
});

async function editReserva(id) {
    const r = await fetch(`${RESERVAS_URL}/${id}`);
    const res = await r.json();
    const form = document.getElementById('formReserva');
    form.dataset.editId = id;
    document.getElementById('formTitle').innerText = `✏️ Editando #${id}`;
    document.getElementById('btnSalvarReserva').className = 'btn btn-warning w-100';
    document.getElementById('clienteId').value = res.clienteId;
    document.getElementById('tituloReserva').value = res.tituloReserva;
    document.getElementById('responsavel').value = res.responsavel;
    document.getElementById('dataInicio').value = res.dataInicio.substring(0, 16);
    document.getElementById('dataFim').value = res.dataFim.substring(0, 16);
    document.getElementById('participantes').value = res.participantesPrevistos;
    document.getElementById('valorHora').value = res.valorHora;
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function deleteReserva(id) {
    if (confirm('Excluir?')) {
        await fetch(`${RESERVAS_URL}/${id}`, { method: 'DELETE' });
        loadReservas(paginaAtualReservas);
    }
}

async function updateDiscount(id, val) {
    await fetch(`${RESERVAS_URL}/${id}/desconto`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(parseFloat(val)) });
    loadReservas(paginaAtualReservas);
}

function cancelarEdicao() {
    const form = document.getElementById('formReserva');
    delete form.dataset.editId;
    form.reset();
    document.getElementById('formTitle').innerText = '✨ Registrar Nova Reserva';
    document.getElementById('btnSalvarReserva').className = 'btn btn-success w-100';
    document.getElementById('valorHora').value = "100.00";
}

let timeoutResumo = null;
async function loadResumo() {
    try {
        const r = await fetch(`${RESERVAS_URL}/resumo`);
        if (!r.ok) throw new Error('Falha ao buscar resumo');
        
        const resumo = await r.json();
        
        const cards = document.getElementById('cardsResumo');
        if (cards) cards.style.opacity = '0.5';
        
        const elAtivas = document.getElementById('resumoAtivas');
        const elHoras = document.getElementById('resumoHoras');
        const elFaturado = document.getElementById('resumoFaturamento');
        const elPrevisto = document.getElementById('resumoPrevisto');

        if (elAtivas) elAtivas.innerText = resumo.ativas;
        if (elHoras) elHoras.innerText = resumo.totalHoras + 'h';
        if (elFaturado) elFaturado.innerText = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(resumo.faturamentoRealizado);
        if (elPrevisto) elPrevisto.innerText = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(resumo.faturamentoPrevisto);
            
        if (cards) setTimeout(() => cards.style.opacity = '1', 200);
        
        // Evita múltiplos timeouts encavalados
        if (timeoutResumo) clearTimeout(timeoutResumo);
        timeoutResumo = setTimeout(loadResumo, 60000); 
        loadGrafico();
    } 
    catch (e) {
        console.error("Erro no resumo:", e);
    }
}
// ==========================================
// MÓDULO DO DASHBOARD E GRÁFICO
// ==========================================
let graficoOcupacao = null;

async function loadGrafico() {
    try {
        const r = await fetch(`${RESERVAS_URL}/grafico`);
        if (!r.ok) throw new Error('Falha ao buscar dados do gráfico');
        
        const dados = await r.json();
        
        const labels = dados.map(d => d.data);
        const volume = dados.map(d => d.quantidade);
        const faturamento = dados.map(d => d.faturamento);

        const canvas = document.getElementById('graficoOcupacao');
        if (!canvas) return;

        // ESTRATÉGICO: Força a div pai a ter uma altura mínima para o canvas não colapsar
        canvas.parentElement.style.height = '350px';
        canvas.parentElement.style.position = 'relative';

        const ctx = canvas.getContext('2d');
        
        if (graficoOcupacao) {
            graficoOcupacao.destroy();
        }

        // Criação de Gráfico Misto (Volume x Faturamento)
        graficoOcupacao = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Faturamento (R$)',
                        type: 'line',
                        data: faturamento,
                        borderColor: '#198754', // Verde Success Bootstrap
                        backgroundColor: '#198754',
                        borderWidth: 3,
                        tension: 0.3,
                        yAxisID: 'yFaturamento'
                    },
                    {
                        label: 'Volume de Reservas',
                        type: 'bar',
                        data: volume,
                        backgroundColor: 'rgba(13, 110, 253, 0.7)', // Azul Primary Bootstrap
                        borderColor: '#0d6efd',
                        borderWidth: 1,
                        borderRadius: 4,
                        yAxisID: 'yVolume'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                scales: {
                    yVolume: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        title: { display: true, text: 'Qtd Reservas' },
                        beginAtZero: true,
                        ticks: { stepSize: 1 }
                    },
                    yFaturamento: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        title: { display: true, text: 'Faturamento (R$)' },
                        beginAtZero: true,
                        grid: { drawOnChartArea: false } // Evita sobreposição de linhas de grade
                    }
                }
            }
        });

    } catch (e) {
        console.error("Erro ao carregar gráfico:", e);
    }
}

// ==========================================
// INICIALIZAÇÃO DA PÁGINA
// ==========================================
document.addEventListener('DOMContentLoaded', () => { 
    loadClients(); 
    loadReservas(1); 
    loadResumo();
    
    // Tratamento de Máscaras
    document.getElementById('cpfCnpjCliente')?.addEventListener('input', function(e) {
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

    document.getElementById('telefoneCliente')?.addEventListener('input', function(e) {
        let v = e.target.value.replace(/\D/g, ''); 
        v = v.replace(/^(\d{2})(\d)/g, '($1) $2'); 
        v = v.replace(/(\d)(\d{4})$/, '$1-$2');    
        e.target.value = v;
    });

    // Tratamento de Renderização do Gráfico no Bootstrap Lifecycle
    const tabDashboard = document.getElementById('dashboard-tab');
    if (tabDashboard) {
        tabDashboard.addEventListener('shown.bs.tab', function () {
            if (!graficoOcupacao) {
                loadGrafico(); 
            } else {
                graficoOcupacao.resize();
            }
        });
    }
});