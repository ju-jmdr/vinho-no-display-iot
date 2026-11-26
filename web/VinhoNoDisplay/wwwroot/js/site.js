// Aviso (toast) no canto da tela
function aviso(texto, ok) {
    var $a = $('#aviso');
    $a.text(texto).removeClass('ok erro').addClass(ok ? 'ok' : 'erro').addClass('visivel');
    clearTimeout(window._avisoTimer);
    window._avisoTimer = setTimeout(function () { $a.removeClass('visivel'); }, 4500);
}

// ---------- Mensagens IoT: AJAX devolvendo a partial view ----------
function aplicaFiltroMensagens() {
    $('#gridMensagens').html('<div class="aviso-vazio">Consultando o STH-Comet…</div>');
    $.ajax({
        url: '/Mensagens/ObtemDados',
        data: {
            maximo: $('#maximo').val(),
            dataInicial: $('#dataInicial').val(),
            dataFinal: $('#dataFinal').val(),
            somenteAnomalias: $('#somenteAnomalias').is(':checked')
        },
        success: function (html) { $('#gridMensagens').html(html); },
        error: function () { $('#gridMensagens').html('<div class="aviso-vazio erro">Falha ao consultar o servidor.</div>'); }
    });
}

// ---------- Perfis ----------
function aplicaPerfil(id) {
    $.post('/Perfil/AplicarNoDispositivo', { id: id }, function (r) { aviso(r.mensagem, r.ok); })
        .fail(function () { aviso('Falha ao enviar o perfil.', false); });
}

// ---------- Dispositivo ----------
function enviaConfig() {
    $.post('/Dispositivo/EnviaConfig', {
        tempMin: $('#tempMin').val(), tempMax: $('#tempMax').val(),
        umidMin: $('#umidMin').val(), umidMax: $('#umidMax').val(), luzMax: $('#luzMax').val()
    }, function (r) { aviso(r.mensagem, r.ok); }).fail(function () { aviso('Falha ao enviar a configuração.', false); });
}

function testaAlerta() {
    $.post('/Dispositivo/TestaAlerta', function (r) { aviso(r.mensagem, r.ok); })
        .fail(function () { aviso('Falha ao enviar o teste.', false); });
}

function atualizaStatusDispositivo() {
    $.getJSON('/Dispositivo/ObtemStatus', function (r) {
        var $p = $('#pillDisp'), $o = $('#stOnline');
        if (!r.ok) { $p.attr('class', 'pilula perigo').text('Sem FIWARE'); $o.attr('class', 'pilula perigo').text('Sem FIWARE'); return; }
        var cls = r.online ? 'pilula ok' : 'pilula alerta', txt = r.online ? 'Online' : 'Sem sinal';
        $p.attr('class', cls).text(txt); $o.attr('class', cls).text(txt);
        $('#stUltima').text(r.ultima || '–');
        if (r.statusConfig) $('#stConfig').text(r.statusConfig);
        if (r.statusAlerta) $('#stAlerta').text(r.statusAlerta);
    });
}

function salvaHost() {
    $.post('/Dispositivo/SalvaHost', { host: $('#hostFiware').val() }, function (r) {
        aviso(r.mensagem, r.ok);
        if (r.ok) setTimeout(function () { location.reload(); }, 1200);
    }).fail(function () { aviso('Falha ao salvar o IP.', false); });
}
