// Dashboard: busca /Dashboard/ObtemDados a cada 5 s e atualiza KPIs e 4 gráficos (Chart.js)
var graficos = {};
var COR = { ouro: '#c8a35a', vinho: '#b8405a', verde: '#7fb28f', marrom: '#b07a45', texto: '#a9b3a8', grade: 'rgba(255,255,255,.07)' };

Chart.defaults.color = COR.texto;
Chart.defaults.font.family = "'Inter', system-ui, sans-serif";

function criaLinha(idCanvas, rotulo, cor) {
    var ctx = document.getElementById(idCanvas);
    return new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [
                { label: 'Mínimo ideal', data: [], borderWidth: 0, pointRadius: 0, fill: false },
                { label: 'Máximo ideal', data: [], borderWidth: 0, pointRadius: 0, fill: '-1', backgroundColor: 'rgba(127,178,143,.14)' },
                { label: rotulo, data: [], borderColor: cor, backgroundColor: cor, borderWidth: 2.2, pointRadius: 0, pointHoverRadius: 4, tension: .3, spanGaps: true }
            ]
        },
        options: {
            responsive: true, maintainAspectRatio: false, animation: false,
            interaction: { mode: 'index', intersect: false },
            plugins: { legend: { display: false }, tooltip: { filter: function (i) { return i.datasetIndex === 2; } } },
            scales: {
                x: { grid: { display: false }, ticks: { maxTicksLimit: 4, maxRotation: 0 } },
                y: { grid: { color: COR.grade }, ticks: { maxTicksLimit: 6 } }
            }
        }
    });
}

function criaGraficos() {
    graficos.temp = criaLinha('grafTemp', 'Temperatura (°C)', COR.vinho);
    graficos.umid = criaLinha('grafUmid', 'Umidade (%)', COR.ouro);
    graficos.luz = new Chart(document.getElementById('grafLuz'), {
        type: 'bar',
        data: { labels: [], datasets: [{ label: 'Luminosidade (%)', data: [], backgroundColor: 'rgba(176,122,69,.75)', borderRadius: 3 }] },
        options: {
            responsive: true, maintainAspectRatio: false, animation: false,
            plugins: { legend: { display: false } },
            scales: { x: { grid: { display: false }, ticks: { maxTicksLimit: 4, maxRotation: 0 } },
                      y: { beginAtZero: true, suggestedMax: 50, grid: { color: COR.grade } } }
        }
    });
    graficos.anom = new Chart(document.getElementById('grafAnomalias'), {
        type: 'doughnut',
        data: { labels: ['Temperatura', 'Umidade', 'Luz', 'Sensor'], datasets: [{ data: [0, 0, 0, 0], backgroundColor: [COR.vinho, COR.ouro, COR.marrom, '#6f7f77'], borderColor: '#13231c', borderWidth: 3 }] },
        options: { responsive: true, maintainAspectRatio: false, animation: false, cutout: '62%', plugins: { legend: { position: 'bottom' } } }
    });
}

function classeFaixa(v, mn, mx) { return (v == null) ? '' : (v < mn || v > mx ? 'valor-perigo' : 'valor-ok'); }

function pct(v, mn, mx) { // posição do valor dentro de [mn - folga, mx + folga], para a barrinha
    if (v == null) return 0;
    var folga = (mx - mn) * 0.5, p = (v - (mn - folga)) / ((mx + folga) - (mn - folga));
    return Math.max(2, Math.min(100, p * 100));
}

function atualizaPainel() {
    $.getJSON('/Dashboard/ObtemDados', { quantidade: $('#selQuantidade').val() }, function (d) {
        if (d.erro) { $('#erroPainel').text(d.erro).show(); return; }
        $('#erroPainel').hide();

        var $o = $('#pillOnline');
        if (!d.cadastrado) $o.attr('class', 'pilula alerta').text('Dispositivo não cadastrado');
        else if (d.online) $o.attr('class', 'pilula ok').text('Online');
        else $o.attr('class', 'pilula alerta').text('Sem sinal');
        $('#pillGarrafas').text(d.garrafas + (d.garrafas === 1 ? ' garrafa' : ' garrafas'));

        var L = d.limites, a = d.atual;
        $('#faixaTemp').text('Ideal: ' + L.tempMin + ' – ' + L.tempMax + ' °C');
        $('#faixaUmid').text('Ideal: ' + L.umidMin + ' – ' + L.umidMax + ' %');
        $('#faixaLuz').text('Máximo: ' + L.luzMax + ' %');

        if (a) {
            $('#kpiTemp').html((a.temperatura == null ? '--' : a.temperatura.toFixed(1)) + '<small>°C</small>').attr('class', 'kpi-valor ' + classeFaixa(a.temperatura, L.tempMin, L.tempMax));
            $('#kpiUmid').html((a.umidade == null ? '--' : a.umidade.toFixed(1)) + '<small>%</small>').attr('class', 'kpi-valor ' + classeFaixa(a.umidade, L.umidMin, L.umidMax));
            $('#kpiLuz').html((a.luminosidade == null ? '--' : Math.round(a.luminosidade)) + '<small>%</small>').attr('class', 'kpi-valor ' + (a.luminosidade != null && a.luminosidade > L.luzMax ? 'valor-perigo' : 'valor-ok'));
            $('#barraTemp').css('width', pct(a.temperatura, L.tempMin, L.tempMax) + '%');
            $('#barraUmid').css('width', pct(a.umidade, L.umidMin, L.umidMax) + '%');
            $('#barraLuz').css('width', Math.min(100, a.luminosidade || 0) + '%');
        }
        var c = d.conformidade;
        $('#kpiConf').html(c.toFixed(0) + '<small>%</small>').attr('class', 'kpi-valor ' + (c >= 90 ? 'valor-ok' : c >= 70 ? 'valor-alerta' : 'valor-perigo'));
        $('#barraConf').css({ width: c + '%', background: c >= 90 ? '#7fb28f' : c >= 70 ? '#e0a84b' : '#d9566b' });

        var P = d.supervisao;
        if (P) {
            $('#supJanela').text('(média das últimas ' + P.janela + ' leituras)');
            function f(v, u) { return v == null ? '--' : v + ' ' + u; }
            $('#supTemp').text(f(P.mediaTemperatura, '°C')).attr('class', 'kpi-valor ' + classeFaixa(P.mediaTemperatura, L.tempMin, L.tempMax));
            $('#supUmid').text(f(P.mediaUmidade, '%')).attr('class', 'kpi-valor ' + classeFaixa(P.mediaUmidade, L.umidMin, L.umidMax));
            $('#supLuz').text(f(P.mediaLuminosidade, '%')).attr('class', 'kpi-valor ' + (P.mediaLuminosidade != null && P.mediaLuminosidade > L.luzMax ? 'valor-perigo' : 'valor-ok'));
            $('#supMsg').text(P.mensagem || '');
            $('#supCmd').text(P.ultimoComando ? 'Último comando enviado ao ESP32: ' + P.ultimoComando : 'Nenhum comando enviado ainda.');
            $('#pillSup').attr('class', 'pilula ' + (P.leituras === 0 ? 'neutra' : (P.mascara ? 'perigo' : 'ok')))
                         .text(P.leituras === 0 ? 'Sem dados' : (P.mascara ? 'Alerta: ' + P.causas.join(', ') : 'Normal'));
        }

        var S = d.serie, n = S.labels.length;
        function banda(g, mn, mx, valores) {
            g.data.labels = S.labels;
            g.data.datasets[0].data = Array(n).fill(mn);
            g.data.datasets[1].data = Array(n).fill(mx);
            g.data.datasets[2].data = valores;
            g.update();
        }
        banda(graficos.temp, L.tempMin, L.tempMax, S.temperatura);
        banda(graficos.umid, L.umidMin, L.umidMax, S.umidade);
        graficos.luz.data.labels = S.labels;
        graficos.luz.data.datasets[0].data = S.luminosidade;
        graficos.luz.update();

        var A = d.anomalias, tot = A.temperatura + A.umidade + A.luz + A.sensor;
        graficos.anom.data.datasets[0].data = [A.temperatura, A.umidade, A.luz, A.sensor];
        graficos.anom.update();
        $('#semAnomalias').toggle(tot === 0);
        $('#grafAnomalias').toggle(tot > 0);
    }).fail(function () { $('#erroPainel').text('Falha ao consultar o servidor.').show(); });
}

$(function () {
    criaGraficos();
    atualizaPainel();
    setInterval(atualizaPainel, 5000);
    $('#selQuantidade').on('change', atualizaPainel);
});
