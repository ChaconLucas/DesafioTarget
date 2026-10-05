"use strict";

const moeda = v => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const pct = v => (v * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 }) + '%';
const $ = id => document.getElementById(id);
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const iniciais = nome => nome.split(' ').filter(Boolean).map(p => p[0]).slice(0, 2).join('').toUpperCase();
const icoOk = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M20 6L9 17l-5-5"/></svg>';
const icoErr = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 8v4M12 16h.01"/></svg>';
let produtos = [];

// ---------- abas ----------
const ABAS = ['comissoes', 'estoque', 'juros'];

function abrirAba(id) {
  // O id pode vir do endereço (#estoque), então só aceita abas conhecidas.
  if (!ABAS.includes(id)) id = 'comissoes';
  document.querySelectorAll('nav button').forEach(b => b.setAttribute('aria-selected', b.dataset.tab === id));
  document.querySelectorAll('main section').forEach(s => s.hidden = s.id !== id);
}
document.querySelectorAll('nav button').forEach(btn => btn.addEventListener('click', () => {
  history.replaceState(null, '', '#' + btn.dataset.tab);
  abrirAba(btn.dataset.tab);
}));
abrirAba(location.hash.slice(1));

function mostrar(el, texto, tipo) {
  el.innerHTML = (tipo === 'ok' ? icoOk : icoErr) + `<span>${esc(texto)}</span>`;
  el.className = 'msg ' + tipo;
  el.hidden = false;
}

// ---------- comissões ----------
async function carregarComissoes() {
  const { vendedores, total } = await (await fetch('/api/comissoes')).json();
  const top = vendedores[0];

  $('kpis-comissoes').innerHTML = `
    <div class="kpi"><small>Total vendido</small><strong>${moeda(total.totalVendido)}</strong><span>${vendedores.length} vendedores</span></div>
    <div class="kpi hl"><small>Total de comissões</small><strong>${moeda(total.totalComissao)}</strong><span>${pct(total.totalComissao / total.totalVendido)} sobre as vendas</span></div>
    <div class="kpi"><small>Vendas</small><strong>${total.quantidadeVendas}</strong><span>ticket médio ${moeda(total.totalVendido / total.quantidadeVendas)}</span></div>
    <div class="kpi"><small>Maior comissão</small><strong>${esc(top.vendedor)}</strong><span>${moeda(top.totalComissao)}</span></div>`;

  const chev = '<svg class="chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M9 6l6 6-6 6"/></svg>';
  const faixa = p => `<span class="chip c${Math.round(p * 100)}"><i></i>${pct(p)}</span>`;
  $('tb-comissoes').innerHTML = vendedores.map((v, i) => {
    const part = v.totalComissao / total.totalComissao;
    const n = v.vendas.length;
    const linhas = v.vendas.map((x, j) => `
      <tr class="venda${j === 0 ? ' primeira' : ''}${j === n - 1 ? ' ultima' : ''}" data-v="${i}" hidden>
        <td class="rotulo">Venda ${j + 1}</td>
        <td class="num">${faixa(x.percentual)}</td>
        <td class="num valor">${moeda(x.valor)}</td>
        <td class="num valor">${moeda(x.comissao)}</td>
        <td></td>
      </tr>`).join('');
    return `<tr class="vendedor" data-i="${i}" tabindex="0" aria-expanded="false">
      <td><div class="who">${chev}<span class="rank">${i + 1}</span><span class="avatar">${iniciais(v.vendedor)}</span>${esc(v.vendedor)}</div></td>
      <td class="num">${v.quantidadeVendas}</td>
      <td class="num">${moeda(v.totalVendido)}</td>
      <td class="num"><strong>${moeda(v.totalComissao)}</strong></td>
      <td><div class="share"><div class="bar"><i style="width:${(part * 100).toFixed(1)}%"></i></div><small>${pct(part)}</small></div></td>
    </tr>${linhas}`;
  }).join('');
  document.querySelectorAll('tr.vendedor').forEach(tr => {
    const alternar = () => {
      const aberto = tr.classList.toggle('aberto');
      tr.setAttribute('aria-expanded', aberto);
      document.querySelectorAll(`tr.venda[data-v="${tr.dataset.i}"]`).forEach(l => l.hidden = !aberto);
    };
    tr.addEventListener('click', alternar);
    tr.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); alternar(); } });
  });
  $('tf-comissoes').innerHTML = `
    <tr><td>Total</td><td class="num">${total.quantidadeVendas}</td>
    <td class="num">${moeda(total.totalVendido)}</td><td class="num">${moeda(total.totalComissao)}</td><td></td></tr>`;
}

// ---------- estoque ----------
function nivel(qtd) {
  // Escala relativa ao maior saldo, só para visualização.
  const max = Math.max(...produtos.map(p => p.estoque), 1);
  const cls = qtd < 50 ? 'low' : qtd < 100 ? 'warn' : 'ok';
  return `<div class="share" style="min-width:0"><div class="bar ${cls}"><i style="width:${(qtd / max * 100).toFixed(1)}%"></i></div></div>`;
}

function atualizarSaldo() {
  const p = produtos.find(x => x.codigoProduto === Number($('sel-produto').value));
  $('saldo-atual').innerHTML = p ? `Saldo atual: <strong>${p.estoque}</strong> unidades` : '';
}

async function carregarEstoque() {
  const [p, h] = await Promise.all([
    fetch('/api/estoque/produtos').then(r => r.json()),
    fetch('/api/estoque/movimentacoes').then(r => r.json()),
  ]);
  produtos = p;

  const sel = $('sel-produto'), atual = sel.value;
  sel.innerHTML = produtos.map(x => `<option value="${x.codigoProduto}">${x.codigoProduto} · ${esc(x.descricaoProduto)}</option>`).join('');
  if (atual) sel.value = atual;
  atualizarSaldo();

  $('total-itens').textContent = `${produtos.reduce((s, x) => s + x.estoque, 0)} unidades`;
  $('tb-produtos').innerHTML = produtos.map(x => `
    <tr><td class="code">${x.codigoProduto}</td><td>${esc(x.descricaoProduto)}</td>
    <td class="num"><strong>${x.estoque}</strong></td><td>${nivel(x.estoque)}</td></tr>`).join('');

  const nome = cod => produtos.find(x => x.codigoProduto === cod)?.descricaoProduto ?? cod;
  $('total-movs').textContent = h.length === 1 ? '1 lançamento' : `${h.length} lançamentos`;
  $('tb-historico').innerHTML = h.length ? h.map(m => `
    <tr><td class="code">${m.id}</td>
    <td>${new Date(m.dataHora).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</td>
    <td>${esc(nome(m.codigoProduto))}</td>
    <td><span class="tag ${m.tipo}">${m.tipo === 'Saida' ? 'Saída' : 'Entrada'}</span></td>
    <td>${esc(m.descricao)}</td>
    <td class="num">${m.tipo === 'Saida' ? '−' : '+'}${m.quantidade}</td>
    <td class="num delta">${m.estoqueAnterior} → <strong>${m.estoqueFinal}</strong></td></tr>`).join('')
    : '<tr><td colspan="7" class="empty">Nenhuma movimentação ainda. Use o formulário acima para lançar a primeira.</td></tr>';
}

$('sel-produto').addEventListener('change', atualizarSaldo);

$('btn-reiniciar').addEventListener('click', async () => {
  if (!confirm('Apagar todo o histórico e voltar ao saldo inicial dos produtos?')) return;
  await fetch('/api/estoque/movimentacoes', { method: 'DELETE' });
  mostrar($('msg-estoque'), 'Histórico apagado. Os saldos voltaram ao valor inicial.', 'ok');
  carregarEstoque();
});

$('form-estoque').addEventListener('submit', async e => {
  e.preventDefault();
  const f = new FormData(e.target);
  const corpo = {
    codigoProduto: Number(f.get('codigoProduto')),
    tipo: f.get('tipo'),
    quantidade: Number(f.get('quantidade')),
    descricao: f.get('descricao'),
  };
  const r = await fetch('/api/estoque/movimentacoes', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo),
  });
  const dados = await r.json().catch(() => ({}));
  if (r.ok) {
    const prod = produtos.find(x => x.codigoProduto === dados.codigoProduto);
    mostrar($('msg-estoque'),
      `Movimentação #${dados.id} registrada. ${prod?.descricaoProduto}: ${dados.estoqueAnterior} → ${dados.estoqueFinal}. Estoque final: ${dados.estoqueFinal}.`, 'ok');
    e.target.quantidade.value = '';
    e.target.descricao.value = '';
  } else {
    mostrar($('msg-estoque'), dados.erro ?? 'Não foi possível registrar a movimentação.', 'err');
  }
  carregarEstoque();
});

// ---------- juros ----------
$('form-juros').addEventListener('submit', async e => {
  e.preventDefault();
  const f = new FormData(e.target);
  // Aceita "1.500,00" ou "1500.00".
  const bruto = String(f.get('valor')).trim();
  const valor = bruto.includes(',') ? Number(bruto.replace(/\./g, '').replace(',', '.')) : Number(bruto);
  $('msg-juros').hidden = true;
  if (!Number.isFinite(valor)) return mostrar($('msg-juros'), 'Valor inválido. Use o formato 1.500,00.', 'err');

  const r = await fetch(`/api/juros?valor=${valor}&vencimento=${f.get('vencimento')}`);
  const dados = await r.json().catch(() => ({}));
  if (!r.ok) return mostrar($('msg-juros'), dados.erro ?? 'Dados inválidos.', 'err');

  const { resultado: x, hoje } = dados;
  const data = iso => iso.split('-').reverse().join('/');
  $('res-juros').innerHTML = `
    <div class="line"><span>Valor original</span><strong>${moeda(x.valorOriginal)}</strong></div>
    <div class="line"><span>Vencimento → hoje</span><strong>${data(f.get('vencimento'))} → ${data(hoje)}</strong></div>
    <div class="line"><span>Dias de atraso</span><strong>${x.diasAtraso}</strong></div>
    <div class="line"><span>Juros (2,5% × ${x.diasAtraso} dias)</span><strong>${moeda(x.valorJuros)}</strong></div>
    <div class="total-box"><small>Valor atualizado</small><strong>${moeda(x.valorAtualizado)}</strong></div>`;
  $('res-vazio').hidden = true;
  $('res-juros').hidden = false;
});

carregarComissoes();
carregarEstoque();
